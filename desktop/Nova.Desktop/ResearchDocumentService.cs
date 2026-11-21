using System.Text;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UglyToad.PdfPig;
using WpfMath;
using WpfMath.Parsers;
using XamlMath.Exceptions;

namespace Nova.Desktop;

internal sealed record PdfRasterImage(int Width, int Height, byte[] RgbPixels);

internal static class ResearchDocumentService
{
    private const int MaxPdfPages = 300;

    public static PdfTextResult ReadPdfText(string path, int maxCharacters)
    {
        using var document = PdfDocument.Open(path);
        var text = new StringBuilder();
        var pagesRead = 0;
        var hasText = false;
        var truncated = false;
        foreach (var page in document.GetPages())
        {
            if (++pagesRead > MaxPdfPages)
                throw new InvalidDataException($"Das PDF hat mehr als {MaxPdfPages} Seiten. Bitte teile es vor der Analyse auf.");

            var pageText = page.Text;
            if (string.IsNullOrWhiteSpace(pageText))
                continue;
            hasText = true;
            if (truncated || text.Length >= maxCharacters)
            {
                truncated = true;
                continue;
            }

            var separatorLength = text.Length == 0 ? 0 : Environment.NewLine.Length;
            var available = Math.Max(0, maxCharacters - text.Length);
            if (separatorLength > 0 && available > 0)
            {
                var separator = Environment.NewLine.AsSpan(0, Math.Min(separatorLength, available));
                text.Append(separator);
                available -= separator.Length;
            }

            if (pageText.Length > available)
            {
                if (available > 0)
                    text.Append(pageText.AsSpan(0, available));
                truncated = true;
                continue;
            }

            text.Append(pageText);
        }

        var result = text.ToString();
        if (!hasText)
            throw new InvalidDataException(
                $"„{Path.GetFileName(path)}“ enthält keinen auslesbaren Text. Bildbasierte PDFs benötigen OCR, das NOVA nicht ausführt.");

        return new PdfTextResult(result, truncated);
    }

    public static byte[] CreateSummaryPdf(
        string summary, DateTime createdAt, IReadOnlyList<ResearchIllustration>? illustrations = null)
    {
        var blocks = ParseMarkdown(summary).ToList();
        if (illustrations is { Count: > 0 })
        {
            blocks.Add(new DocumentBlock(BlockKind.Heading, "Beispielbilder aus Wikimedia Commons"));
            foreach (var illustration in illustrations)
            {
                blocks.Add(new DocumentBlock(
                    BlockKind.Image, "", Image: illustration.Image));
                blocks.Add(new DocumentBlock(
                    BlockKind.Quote,
                    $"{illustration.Title} — Urheber: {illustration.Creator} — Lizenz: {illustration.License} — " +
                    $"Quelle: {illustration.SourceUrl}"));
            }
        }
        var pages = LayoutPages(blocks);
        var pageContents = pages.Select((page, index) =>
            CreatePageContent(page, index + 1, pages.Count, createdAt)).ToArray();

        using var output = new MemoryStream();
        WriteAscii(output, "%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        var pageObjectIds = Enumerable.Range(0, pageContents.Length).Select(index => 6 + index * 2).ToArray();

        WriteObject(output, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
        WriteObject(output, offsets, 2,
            $"<< /Type /Pages /Kids [{string.Join(' ', pageObjectIds.Select(id => $"{id} 0 R"))}] /Count {pageContents.Length} >>");
        WriteObject(output, offsets, 3,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        WriteObject(output, offsets, 4,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        WriteObject(output, offsets, 5,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Oblique /Encoding /WinAnsiEncoding >>");

        for (var index = 0; index < pageContents.Length; index++)
        {
            var pageId = pageObjectIds[index];
            var contentId = pageId + 1;
            WriteObject(output, offsets, pageId,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R /F3 5 0 R >> >> /Contents {contentId} 0 R >>");
            var contentBytes = Encoding.ASCII.GetBytes(pageContents[index]);
            offsets.Add(output.Position);
            WriteAscii(output, $"{contentId} 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
            output.Write(contentBytes);
            WriteAscii(output, "\nendstream\nendobj\n");
        }

        var xrefOffset = output.Position;
        WriteAscii(output, $"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
            WriteAscii(output, $"{offset:0000000000} 00000 n \n");
        WriteAscii(output,
            $"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return output.ToArray();
    }

    private const double PageWidth = 595;
    private const double LeftMargin = 52;
    private const double RightMargin = 52;
    private const double BottomMargin = 55;
    private const double ContentWidth = PageWidth - LeftMargin - RightMargin;

    private enum BlockKind
    {
        Title,
        Heading,
        Subheading,
        MinorHeading,
        Paragraph,
        Bullet,
        Numbered,
        Quote,
        Rule,
        Table,
        Formula,
        Image,
        FormulaFallback
    }

    private sealed record DocumentBlock(
        BlockKind Kind,
        string Text,
        IReadOnlyList<IReadOnlyList<string>>? TableRows = null,
        PdfRasterImage? Image = null);

    private static IReadOnlyList<DocumentBlock> ParseMarkdown(string markdown)
    {
        var blocks = new List<DocumentBlock>();
        var paragraph = new StringBuilder();

        void FlushParagraph()
        {
            if (paragraph.Length == 0)
                return;
            blocks.Add(new DocumentBlock(BlockKind.Paragraph, paragraph.ToString().Trim()));
            paragraph.Clear();
        }

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            if (TryReadDisplayMath(lines, ref index, out var formula))
            {
                FlushParagraph();
                blocks.Add(CreateFormulaBlock(formula));
                continue;
            }

            if (IsMarkdownTableStart(lines, index))
            {
                FlushParagraph();
                var rows = new List<IReadOnlyList<string>>();
                while (index < lines.Length && IsMarkdownTableRow(lines[index]))
                {
                    var cells = ParseMarkdownTableRow(lines[index]);
                    if (!IsMarkdownTableSeparator(cells))
                        rows.Add(cells);
                    index++;
                }
                index--;
                if (rows.Count > 0)
                    blocks.Add(new DocumentBlock(BlockKind.Table, "", rows));
                continue;
            }

            var heading = Regex.Match(line, @"^(#{1,4})\s+(.+?)\s*#*$");
            if (heading.Success)
            {
                FlushParagraph();
                var level = heading.Groups[1].Length;
                var kind = level switch
                {
                    1 => BlockKind.Title,
                    2 => BlockKind.Heading,
                    3 => BlockKind.Subheading,
                    _ => BlockKind.MinorHeading
                };
                blocks.Add(new DocumentBlock(kind, CleanInlineMarkdown(heading.Groups[2].Value)));
                continue;
            }

            var numberedHeading = Regex.Match(line, @"^(\d+(?:\.\d+){0,3})\.\s+(.+)$");
            if (numberedHeading.Success && numberedHeading.Groups[2].Value.Length <= 120)
            {
                FlushParagraph();
                var level = numberedHeading.Groups[1].Value.Count(character => character == '.') + 2;
                var kind = level switch
                {
                    2 => BlockKind.Heading,
                    3 => BlockKind.Subheading,
                    _ => BlockKind.MinorHeading
                };
                blocks.Add(new DocumentBlock(
                    kind,
                    CleanInlineMarkdown($"{numberedHeading.Groups[1].Value}. {numberedHeading.Groups[2].Value}")));
                continue;
            }

            if (Regex.IsMatch(line, @"^(---+|___+|\*\*\*+)$"))
            {
                FlushParagraph();
                blocks.Add(new DocumentBlock(BlockKind.Rule, ""));
                continue;
            }

            var bullet = Regex.Match(line, @"^[-*+]\s+(.+)$");
            if (bullet.Success)
            {
                FlushParagraph();
                blocks.Add(new DocumentBlock(BlockKind.Bullet, CleanInlineMarkdown(bullet.Groups[1].Value)));
                continue;
            }

            var numbered = Regex.Match(line, @"^\d+[.)]\s+(.+)$");
            if (numbered.Success)
            {
                FlushParagraph();
                blocks.Add(new DocumentBlock(BlockKind.Numbered, CleanInlineMarkdown(numbered.Groups[1].Value)));
                continue;
            }

            if (line.StartsWith('>'))
            {
                FlushParagraph();
                blocks.Add(new DocumentBlock(BlockKind.Quote, CleanInlineMarkdown(line.TrimStart('>', ' '))));
                continue;
            }

            if (paragraph.Length > 0)
                paragraph.Append(' ');
            paragraph.Append(CleanInlineMarkdown(line));
        }

        FlushParagraph();
        if (blocks.Count == 0)
            blocks.Add(new DocumentBlock(BlockKind.Paragraph, "Keine Zusammenfassung vorhanden."));
        return blocks;
    }

    private static bool IsMarkdownTableStart(IReadOnlyList<string> lines, int index) =>
        index + 1 < lines.Count
        && IsMarkdownTableRow(lines[index])
        && IsMarkdownTableRow(lines[index + 1])
        && IsMarkdownTableSeparator(ParseMarkdownTableRow(lines[index + 1]));

    private static bool IsMarkdownTableRow(string line) =>
        line.Contains('|') && line.Count(character => character == '|') >= 2;

    private static IReadOnlyList<string> ParseMarkdownTableRow(string line)
    {
        var cells = line.Trim().Trim('|').Split('|')
            .Select(cell => CleanInlineMarkdown(cell.Trim()))
            .ToArray();
        return cells;
    }

    private static bool IsMarkdownTableSeparator(IReadOnlyList<string> cells) =>
        cells.Count > 0 && cells.All(cell => Regex.IsMatch(cell, @"^:?-{3,}:?$"));

    private static bool TryReadDisplayMath(string[] lines, ref int index, out string formula)
    {
        formula = "";
        var line = lines[index].Trim();
        var opening = line.StartsWith(@"\[", StringComparison.Ordinal)
            ? @"\["
            : line.StartsWith("$$", StringComparison.Ordinal) ? "$$" : "";
        if (opening.Length == 0)
            return false;

        var closing = opening == @"\[" ? @"\]" : "$$";
        var contentStart = opening.Length;
        var closeIndex = line.IndexOf(closing, contentStart, StringComparison.Ordinal);
        if (closeIndex >= 0)
        {
            formula = line[contentStart..closeIndex].Trim();
            return true;
        }

        var content = new StringBuilder(line[contentStart..]);
        while (++index < lines.Length)
        {
            var candidate = lines[index].Trim();
            closeIndex = candidate.IndexOf(closing, StringComparison.Ordinal);
            if (content.Length > 0)
                content.Append(' ');
            content.Append(closeIndex >= 0 ? candidate[..closeIndex].Trim() : candidate);
            if (closeIndex >= 0)
                break;
        }
        formula = content.ToString().Trim();
        return true;
    }

    private static DocumentBlock CreateFormulaBlock(string formula)
    {
        try
        {
            var parsed = WpfTeXFormulaParser.Instance.Parse(formula);
            var png = parsed.RenderToPng(20d, 0d, 0d, "Arial");
            return new DocumentBlock(BlockKind.Formula, "", Image: DecodeImage(png));
        }
        catch (TexException)
        {
            return new DocumentBlock(BlockKind.FormulaFallback, formula);
        }
    }

    internal static PdfRasterImage DecodeImage(byte[] imageData)
    {
        using var stream = new MemoryStream(imageData);
        var decoder = BitmapDecoder.Create(
            stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var bitmap = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var stride = checked(width * 4);
        var bgra = new byte[checked(stride * height)];
        bitmap.CopyPixels(bgra, stride, 0);
        var rgb = new byte[checked(width * height * 3)];
        for (var pixel = 0; pixel < width * height; pixel++)
        {
            var sourceOffset = pixel * 4;
            var targetOffset = pixel * 3;
            var alpha = bgra[sourceOffset + 3];
            rgb[targetOffset] = CompositeOverWhite(bgra[sourceOffset + 2], alpha);
            rgb[targetOffset + 1] = CompositeOverWhite(bgra[sourceOffset + 1], alpha);
            rgb[targetOffset + 2] = CompositeOverWhite(bgra[sourceOffset], alpha);
        }
        return new PdfRasterImage(width, height, rgb);
    }

    private static byte CompositeOverWhite(byte channel, byte alpha) =>
        (byte)((channel * alpha + 255 * (255 - alpha) + 127) / 255);

    private static string CleanInlineMarkdown(string text)
    {
        var value = Regex.Replace(text, @"\[([^\]]+)\]\([^)]+\)", "$1");
        value = value.Replace("**", "").Replace("__", "").Replace("~~", "").Replace("`", "");
        return value;
    }

    private static List<List<string>> LayoutPages(IReadOnlyList<DocumentBlock> blocks)
    {
        var pages = new List<List<string>> { new() };
        var y = 770d;
        var page = pages[^1];

        void NewPage()
        {
            pages.Add(new List<string>());
            page = pages[^1];
            y = 770;
        }

        void EnsureSpace(double height)
        {
            if (y - height < BottomMargin)
                NewPage();
        }

        void DrawTableRow(IReadOnlyList<string> cells, IReadOnlyList<double> columnWidths, bool header, bool alternate)
        {
            var fontSize = header ? 9.5d : 9d;
            var lineHeight = 11d;
            var wrappedCells = new List<List<string>>(columnWidths.Count);
            for (var column = 0; column < columnWidths.Count; column++)
            {
                var cell = column < cells.Count ? cells[column] : "";
                wrappedCells.Add(WrapText(cell, columnWidths[column] - 8d, fontSize));
            }

            var rowHeight = Math.Max(18d, wrappedCells.Max(lines => lines.Count) * lineHeight + 8d);
            EnsureSpace(rowHeight);
            var rowBottom = y - rowHeight;
            var fill = header
                ? "0.91 0.94 0.97"
                : alternate ? "0.97 0.98 0.99" : "1 1 1";
            page.Add(FormattableString.Invariant(
                $"q {fill} rg {LeftMargin:0.##} {rowBottom:0.##} {ContentWidth:0.##} {rowHeight:0.##} re f Q"));
            page.Add(FormattableString.Invariant(
                $"q 0.78 0.83 0.89 RG {LeftMargin:0.##} {rowBottom:0.##} {ContentWidth:0.##} {rowHeight:0.##} re S Q"));

            var x = LeftMargin;
            for (var column = 0; column < columnWidths.Count; column++)
            {
                if (column > 0)
                    page.Add(FormattableString.Invariant(
                        $"q 0.84 0.87 0.91 RG {x:0.##} {rowBottom:0.##} m {x:0.##} {y:0.##} l S Q"));
                var cellLines = wrappedCells[column];
                for (var line = 0; line < cellLines.Count; line++)
                    AppendText(page, header ? "/F2" : "/F1", fontSize,
                        "0.15 0.18 0.22", x + 4d, y - 12d - line * lineHeight, cellLines[line]);
                x += columnWidths[column];
            }
            y = rowBottom - 2d;
        }

        void AddTable(IReadOnlyList<IReadOnlyList<string>> rows)
        {
            if (rows.Count == 0)
                return;
            var columnCount = rows.Max(row => row.Count);
            if (columnCount == 0)
                return;

            var weights = Enumerable.Range(0, columnCount)
                .Select(column => Math.Max(8, rows.Max(row =>
                    column < row.Count ? Math.Min(row[column].Length, 36) : 0)))
                .ToArray();
            var totalWeight = weights.Sum();
            var columnWidths = weights
                .Select(weight => ContentWidth * weight / totalWeight)
                .ToArray();

            DrawTableRow(rows[0], columnWidths, header: true, alternate: false);
            for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                var cells = rows[rowIndex];
                var wrapped = Enumerable.Range(0, columnCount)
                    .Select(column => WrapText(
                        column < cells.Count ? cells[column] : "",
                        columnWidths[column] - 8d,
                        9d))
                    .ToArray();
                var rowHeight = Math.Max(18d, wrapped.Max(lines => lines.Count) * 11d + 8d);
                var headerHeight = Math.Max(18d,
                    Enumerable.Range(0, columnCount)
                        .Select(column => WrapText(
                            column < rows[0].Count ? rows[0][column] : "",
                            columnWidths[column] - 8d,
                            9.5d).Count)
                        .Max() * 11d + 8d);
                if (y - rowHeight - headerHeight < BottomMargin)
                {
                    NewPage();
                    DrawTableRow(rows[0], columnWidths, header: true, alternate: false);
                }
                DrawTableRow(cells, columnWidths, header: false, alternate: rowIndex % 2 == 0);
            }
            y -= 8d;
        }

        for (var index = 0; index < blocks.Count; index++)
        {
            var block = blocks[index];
            if (block.Kind == BlockKind.Rule)
            {
                EnsureSpace(24);
                page.Add(FormattableString.Invariant(
                    $"q 0.78 0.83 0.89 RG {LeftMargin:0.##} {y - 4:0.##} m {PageWidth - RightMargin:0.##} {y - 4:0.##} l S Q"));
                y -= 18;
                continue;
            }
            if (block.Kind == BlockKind.Table)
            {
                AddTable(block.TableRows ?? []);
                continue;
            }
            if ((block.Kind is BlockKind.Formula or BlockKind.Image) && block.Image is { } image)
            {
                var width = image.Width * 0.75d;
                var height = image.Height * 0.75d;
                var scale = Math.Min(1d, ContentWidth / width);
                var maximumHeight = block.Kind == BlockKind.Formula ? 90d : 250d;
                if (height * scale > maximumHeight)
                    scale = maximumHeight / height;
                width *= scale;
                height *= scale;
                EnsureSpace(height + 14d + (block.Kind == BlockKind.Image ? 32d : 0d));
                y -= 5d;
                page.Add(CreateInlineImageCommand(
                    image, LeftMargin + (ContentWidth - width) / 2d, y - height, width, height));
                y -= height + 9d;
                continue;
            }

            var (font, size, color, indent, lineHeight, before, after) = block.Kind switch
            {
                BlockKind.Title => ("/F2", 25d, "0.10 0.20 0.35", 0d, 32d, 0d, 18d),
                BlockKind.Heading => ("/F2", 18d, "0.12 0.32 0.55", 0d, 24d, 20d, 8d),
                BlockKind.Subheading => ("/F2", 14d, "0.16 0.27 0.39", 0d, 19d, 13d, 6d),
                BlockKind.MinorHeading => ("/F2", 12d, "0.20 0.27 0.34", 0d, 17d, 9d, 4d),
                BlockKind.Bullet => ("/F1", 10.5d, "0.15 0.18 0.22", 16d, 16d, 1d, 4d),
                BlockKind.Numbered => ("/F1", 10.5d, "0.15 0.18 0.22", 16d, 16d, 1d, 4d),
                BlockKind.Quote => ("/F3", 10.5d, "0.27 0.34 0.42", 14d, 16d, 3d, 6d),
                BlockKind.FormulaFallback => ("/F3", 9.5d, "0.27 0.34 0.42", 14d, 15d, 5d, 8d),
                _ => ("/F1", 10.5d, "0.15 0.18 0.22", 0d, 16d, 2d, 7d)
            };

            var text = block.Kind is BlockKind.Bullet or BlockKind.Numbered
                ? (block.Kind == BlockKind.Bullet ? "•  " : "–  ") + block.Text
                : block.Kind == BlockKind.FormulaFallback
                    ? $"Formel (LaTeX-Quelltext, nicht grafisch setzbar): {block.Text}"
                    : block.Text;
            var lines = WrapText(text, ContentWidth - indent, size);
            var keepsNextBlock = block.Kind is BlockKind.Title or BlockKind.Heading
                or BlockKind.Subheading or BlockKind.MinorHeading;
            var nextBlockSpace = keepsNextBlock && index + 1 < blocks.Count ? 32d : 0d;
            EnsureSpace(before + lineHeight * Math.Min(lines.Count, 2) + after + nextBlockSpace);
            y -= before;
            foreach (var line in lines)
            {
                EnsureSpace(lineHeight);
                AppendText(page, font, size, color, LeftMargin + indent, y, line);
                y -= lineHeight;
            }
            y -= after;
        }

        return pages;
    }

    private static string CreateInlineImageCommand(
        PdfRasterImage image, double x, double y, double width, double height)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(image.RgbPixels);
        var imageData = Convert.ToHexString(compressed.ToArray());
        return FormattableString.Invariant(
            $"q {width:0.##} 0 0 {height:0.##} {x:0.##} {y:0.##} cm BI /W {image.Width} /H {image.Height} /CS /RGB /BPC 8 /F [/AHx /Fl] ID\n") +
            imageData + "\n> EI Q";
    }

    private static List<string> WrapText(string text, double width, double fontSize)
    {
        var maxCharacters = Math.Max(12, (int)(width / (fontSize * 0.50)));
        var lines = new List<string>();
        var remaining = text.Trim();
        while (remaining.Length > maxCharacters)
        {
            var breakAt = remaining.LastIndexOf(' ', maxCharacters);
            if (breakAt <= 0)
                breakAt = maxCharacters;
            lines.Add(remaining[..breakAt].TrimEnd());
            remaining = remaining[breakAt..].TrimStart();
        }
        if (remaining.Length > 0 || lines.Count == 0)
            lines.Add(remaining);
        return lines;
    }

    private static string CreatePageContent(
        IEnumerable<string> commands, int pageNumber, int pageCount, DateTime createdAt)
    {
        var content = new StringBuilder();
        AppendText(content, "/F2", 8, "0.34 0.40 0.48", LeftMargin, 812, "NOVA  |  RESEARCH-DOSSIER");
        content.Append("q 0.82 0.85 0.89 RG ")
            .Append(LeftMargin).Append(" 800 m ")
            .Append(PageWidth - RightMargin).AppendLine(" 800 l S Q");
        foreach (var command in commands)
            content.AppendLine(command);
        content.Append("q 0.82 0.85 0.89 RG ")
            .Append(LeftMargin).Append(" 43 m ")
            .Append(PageWidth - RightMargin).AppendLine(" 43 l S Q");
        AppendText(content, "/F1", 8, "0.40 0.45 0.52", LeftMargin, 28,
            $"Erstellt am {createdAt:dd.MM.yyyy}  |  NOVA Research");
        AppendText(content, "/F1", 8, "0.40 0.45 0.52", PageWidth - RightMargin - 58, 28,
            $"Seite {pageNumber} von {pageCount}");
        return content.ToString();
    }

    private static void AppendText(
        ICollection<string> commands, string font, double size, string color,
        double x, double y, string text) =>
        commands.Add(CreateTextCommand(font, size, color, x, y, text));

    private static void AppendText(
        StringBuilder commands, string font, double size, string color,
        double x, double y, string text) =>
        commands.AppendLine(CreateTextCommand(font, size, color, x, y, text));

    private static string CreateTextCommand(
        string font, double size, string color, double x, double y, string text) =>
        FormattableString.Invariant(
            $"BT {font} {size:0.##} Tf {color} rg {x:0.##} {y:0.##} Td <{Convert.ToHexString(ToWinAnsi(text))}> Tj ET");

    private static byte[] ToWinAnsi(string text)
    {
        var bytes = new byte[text.Length];
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            bytes[index] = character switch
            {
                <= '\u007f' => (byte)character,
                >= '\u00a0' and <= '\u00ff' => (byte)character,
                '\u20ac' => 0x80,
                '\u201a' => 0x82,
                '\u0192' => 0x83,
                '\u201e' => 0x84,
                '\u2026' => 0x85,
                '\u2020' => 0x86,
                '\u2021' => 0x87,
                '\u02c6' => 0x88,
                '\u2030' => 0x89,
                '\u0160' => 0x8a,
                '\u2039' => 0x8b,
                '\u0152' => 0x8c,
                '\u017d' => 0x8e,
                '\u2018' => 0x91,
                '\u2019' => 0x92,
                '\u201c' => 0x93,
                '\u201d' => 0x94,
                '\u2022' => 0x95,
                '\u2013' => 0x96,
                '\u2014' => 0x97,
                '\u2122' => 0x99,
                '\u0161' => 0x9a,
                '\u203a' => 0x9b,
                '\u0153' => 0x9c,
                '\u017e' => 0x9e,
                '\u0178' => 0x9f,
                _ => (byte)'?'
            };
        }
        return bytes;
    }

    private static void WriteObject(Stream output, ICollection<long> offsets, int id, string body)
    {
        offsets.Add(output.Position);
        WriteAscii(output, $"{id} 0 obj\n{body}\nendobj\n");
    }

    private static void WriteAscii(Stream output, string text) =>
        output.Write(Encoding.ASCII.GetBytes(text));

    internal sealed record PdfTextResult(string Text, bool Truncated);
}
