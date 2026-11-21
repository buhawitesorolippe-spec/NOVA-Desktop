using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nova.Desktop;

internal sealed record ResearchIllustration(
    PdfRasterImage Image,
    string Title,
    string Creator,
    string License,
    string SourceUrl);

internal static class WikimediaCommonsService
{
    private const int MaxResults = 50;
    private const int MaxImageBytes = 3 * 1024 * 1024;
    private const int MaxTotalImageBytes = 30 * 1024 * 1024;
    private const int MaxApiResponseBytes = 2 * 1024 * 1024;
    private const int ThumbnailWidth = 1000;
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static WikimediaCommonsService() =>
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("NOVA-Research/1.0 (optional Wikimedia Commons PDF illustrations)");

    public static async Task<IReadOnlyList<ResearchIllustration>> SearchAsync(
        string searchTerm, int maximumImages, Action<string> progress, CancellationToken cancellationToken)
    {
        if (maximumImages is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(maximumImages), "Es sind höchstens 20 Webbilder pro PDF erlaubt.");
        if (string.IsNullOrWhiteSpace(searchTerm))
            throw new ArgumentException("Für die Commons-Bildsuche fehlt ein Thema.", nameof(searchTerm));

        var apiUrl = "https://commons.wikimedia.org/w/api.php?" + string.Join("&",
        [
            "action=query",
            "format=json",
            "formatversion=2",
            "generator=search",
            $"gsrsearch={Uri.EscapeDataString(searchTerm)}",
            "gsrnamespace=6",
            $"gsrlimit={MaxResults}",
            "prop=imageinfo",
            "iiprop=url%7Cextmetadata",
            $"iiurlwidth={ThumbnailWidth}"
        ]);
        using var apiResponse = await Http.GetAsync(apiUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!apiResponse.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Wikimedia Commons meldet HTTP {(int)apiResponse.StatusCode}.",
                null,
                apiResponse.StatusCode);

        var apiBytes = await ReadLimitedAsync(apiResponse, MaxApiResponseBytes, cancellationToken);
        using var json = JsonDocument.Parse(apiBytes);
        if (!json.RootElement.TryGetProperty("query", out var query)
            || !query.TryGetProperty("pages", out var pages)
            || pages.ValueKind != JsonValueKind.Array)
            return [];

        var illustrations = new List<ResearchIllustration>(maximumImages);
        long totalBytes = 0;
        var candidates = pages.EnumerateArray().ToArray();
        for (var index = 0; index < candidates.Length && illustrations.Count < maximumImages; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadCandidate(candidates[index], out var candidate))
                continue;

            progress($"Commons-Bild {illustrations.Count + 1}/{maximumImages} wird geladen …");
            using var imageResponse = await Http.GetAsync(
                candidate.ThumbnailUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!imageResponse.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Ein Wikimedia-Bild konnte nicht geladen werden (HTTP {(int)imageResponse.StatusCode}).",
                    null,
                    imageResponse.StatusCode);
            var mimeType = imageResponse.Content.Headers.ContentType?.MediaType;
            if (mimeType is not ("image/jpeg" or "image/png"))
                continue;

            byte[] imageBytes;
            try
            {
                imageBytes = await ReadLimitedAsync(imageResponse, MaxImageBytes, cancellationToken);
            }
            catch (InvalidDataException)
            {
                continue;
            }
            PdfRasterImage decodedImage;
            try
            {
                decodedImage = ResearchDocumentService.DecodeImage(imageBytes);
            }
            catch (Exception error) when (
                error is IOException or FormatException or NotSupportedException)
            {
                continue;
            }
            if (totalBytes + imageBytes.Length > MaxTotalImageBytes)
                break;

            illustrations.Add(new ResearchIllustration(
                decodedImage,
                candidate.Title,
                candidate.Creator,
                candidate.License,
                candidate.SourceUrl));
            totalBytes += imageBytes.Length;
        }

        return illustrations;
    }

    private static bool TryReadCandidate(JsonElement page, out ImageCandidate candidate)
    {
        candidate = default!;
        if (!page.TryGetProperty("imageinfo", out var imageInfo)
            || imageInfo.ValueKind != JsonValueKind.Array
            || imageInfo.GetArrayLength() == 0)
            return false;
        var info = imageInfo[0];
        var thumbnailUrl = GetString(info, "thumburl");
        var sourceUrl = GetString(info, "descriptionurl");
        if (!IsHttpsHost(thumbnailUrl, "upload.wikimedia.org")
            || !IsHttpsHost(sourceUrl, "commons.wikimedia.org")
            || !info.TryGetProperty("extmetadata", out var metadata))
            return false;

        var license = ReadMetadata(metadata, "LicenseShortName");
        if (!IsAllowedLicense(license))
            return false;
        var creator = ReadMetadata(metadata, "Artist");
        if (creator.Length == 0)
            creator = ReadMetadata(metadata, "Credit");
        if (creator.Length == 0 && !IsPublicDomainOrCc0(license))
            return false;

        var title = GetString(page, "title");
        title = title.StartsWith("File:", StringComparison.OrdinalIgnoreCase) ? title[5..] : title;
        title = WebUtility.HtmlDecode(title.Replace('_', ' ')).Trim();
        if (title.Length == 0)
            return false;

        candidate = new ImageCandidate(
            thumbnailUrl,
            title,
            creator.Length == 0 ? "Urheber nicht angegeben" : creator,
            license,
            sourceUrl);
        return true;
    }

    private static bool IsAllowedLicense(string license)
    {
        var normalized = license.ToUpperInvariant().Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal);
        if (normalized.Contains("NC", StringComparison.Ordinal)
            || normalized.Contains("ND", StringComparison.Ordinal))
            return false;
        return IsPublicDomainOrCc0(license)
            || normalized.Contains("CCBY", StringComparison.Ordinal)
            || normalized.Contains("CREATIVECOMMONSATTRIBUTION", StringComparison.Ordinal);
    }

    private static bool IsPublicDomainOrCc0(string license)
    {
        var normalized = license.ToUpperInvariant().Replace(" ", "", StringComparison.Ordinal);
        return normalized.Contains("CC0", StringComparison.Ordinal)
            || normalized.Contains("PUBLICDOMAIN", StringComparison.Ordinal);
    }

    private static bool IsHttpsHost(string value, string expectedHost) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, expectedHost, StringComparison.OrdinalIgnoreCase);

    private static string ReadMetadata(JsonElement metadata, string key)
    {
        if (!metadata.TryGetProperty(key, out var value)
            || !value.TryGetProperty("value", out var text)
            || text.ValueKind != JsonValueKind.String)
            return "";
        var html = text.GetString() ?? "";
        var plainText = Regex.Replace(html, "<[^>]+>", " ");
        return Regex.Replace(WebUtility.HtmlDecode(plainText), @"\s+", " ").Trim();
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";

    private static async Task<byte[]> ReadLimitedAsync(
        HttpResponseMessage response, int maximumBytes, CancellationToken cancellationToken)
    {
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > maximumBytes)
            throw new InvalidDataException($"Wikimedia-Inhalt überschreitet das Limit von {maximumBytes / 1024 / 1024} MB.");

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + read > maximumBytes)
                throw new InvalidDataException($"Wikimedia-Inhalt überschreitet das Limit von {maximumBytes / 1024 / 1024} MB.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return output.ToArray();
    }

    private sealed record ImageCandidate(
        string ThumbnailUrl, string Title, string Creator, string License, string SourceUrl);
}
