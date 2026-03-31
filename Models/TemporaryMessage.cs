namespace WannaFill.API.Models;

public class TemporaryMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MatchGroupId { get; set; }
    public string SessionId { get; set; } = null!;
    public string Alias { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
