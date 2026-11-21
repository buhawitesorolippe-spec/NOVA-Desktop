namespace Nova.Desktop;

public sealed class ConversationRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Neuer Chat";
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public string? ContextConversationId { get; set; }
    public List<StoredMessage> Messages { get; set; } = [];
}

public sealed class StoredMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}
