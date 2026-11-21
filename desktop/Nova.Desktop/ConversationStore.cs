using System.Text.Json;

namespace Nova.Desktop;

internal static class ConversationStore
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NOVA",
        "conversations");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static IReadOnlyList<ConversationRecord> GetAll()
    {
        Directory.CreateDirectory(DirectoryPath);
        var records = new List<ConversationRecord>();
        foreach (var file in Directory.EnumerateFiles(DirectoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var record = JsonSerializer.Deserialize<ConversationRecord>(File.ReadAllText(file));
                if (record is { Messages.Count: > 0 }
                    && Guid.TryParseExact(record.Id, "N", out _))
                    records.Add(record);
            }
            catch (JsonException)
            {
                // Ignore malformed individual records so the remaining history remains available.
            }
            catch (IOException)
            {
                // A record can be in the middle of an atomic replacement.
            }
        }
        return records.OrderByDescending(record => record.UpdatedUtc).ToArray();
    }

    public static void Save(ConversationRecord conversation)
    {
        if (!Guid.TryParseExact(conversation.Id, "N", out _))
            throw new InvalidDataException("Die Unterhaltungs-ID ist ungültig.");
        Directory.CreateDirectory(DirectoryPath);
        var path = Path.Combine(DirectoryPath, $"{conversation.Id}.json");
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(conversation, JsonOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }

    public static bool Delete(string conversationId)
    {
        if (!Guid.TryParseExact(conversationId, "N", out _))
            throw new InvalidDataException("Die Unterhaltungs-ID ist ungültig.");

        var path = Path.Combine(DirectoryPath, $"{conversationId}.json");
        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }
}
