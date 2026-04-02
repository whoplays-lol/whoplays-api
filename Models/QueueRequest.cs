namespace WannaFill.API.Models;

public class QueueRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SessionId { get; set; } = null!;
    public string Alias { get; set; } = null!;
    public int GameId { get; set; }
    public string Server { get; set; } = null!;
    public string Mode { get; set; } = null!;
    public string? TeamFormat { get; set; }
    public string? Rank { get; set; }
    public int CurrentGroupSize { get; set; }
    public int TotalRequired { get; set; }
    public int PlayersNeeded { get; set; }
    public QueueStatus Status { get; set; } = QueueStatus.Pending;
    public Guid? MatchGroupId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? MatchedAt { get; set; }
    public List<string> ExcludedSessionIds { get; set; } = new();
}

public enum QueueStatus
{
    Pending,
    Matched,
    Cancelled
}
