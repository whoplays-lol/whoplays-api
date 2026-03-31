namespace WannaFill.API.Models;

public class MatchGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int GameId { get; set; }
    public string Server { get; set; } = null!;
    public string Mode { get; set; } = null!;
    public string? TeamFormat { get; set; }
    public string? Rank { get; set; }
    public int TotalPlayers { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
