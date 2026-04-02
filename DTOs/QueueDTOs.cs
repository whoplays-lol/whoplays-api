using System.ComponentModel.DataAnnotations;
using WannaFill.API.GameConfig;
using WannaFill.API.Models;

namespace WannaFill.API.DTOs;

public class CreateQueueRequestDto
{
    [Required, MaxLength(50)]
    public string Alias { get; set; } = null!;

    [Required, MaxLength(100)]
    public string SessionId { get; set; } = null!;

    [Range(1, 10)]
    public int GameId { get; set; }

    [Required, MaxLength(50)]
    public string Server { get; set; } = null!;

    [Required, MaxLength(50)]
    public string Mode { get; set; } = null!;

    [MaxLength(20)]
    public string? TeamFormat { get; set; }

    [MaxLength(50)]
    public string? Rank { get; set; }

    [Range(1, 4)]
    public int CurrentGroupSize { get; set; }

    public List<string>? ExcludedSessionIds { get; set; }
}

public class QueueRequestDto
{
    public string Id { get; set; } = null!;
    public string Alias { get; set; } = null!;
    public string SessionId { get; set; } = null!;
    public int GameId { get; set; }
    public string GameName { get; set; } = null!;
    public string Server { get; set; } = null!;
    public string Mode { get; set; } = null!;
    public string? TeamFormat { get; set; }
    public string? Rank { get; set; }
    public int CurrentGroupSize { get; set; }
    public int TotalRequired { get; set; }
    public int PlayersNeeded { get; set; }
    public string Status { get; set; } = null!;
    public string? MatchGroupId { get; set; }
    public DateTime CreatedAt { get; set; }

    public static QueueRequestDto From(QueueRequest r)
    {
        var game = GameDefinitions.GetById(r.GameId);
        return new QueueRequestDto
        {
            Id = r.Id.ToString(),
            Alias = r.Alias,
            SessionId = r.SessionId,
            GameId = r.GameId,
            GameName = game?.Name ?? "Unknown",
            Server = r.Server,
            Mode = r.Mode,
            TeamFormat = r.TeamFormat,
            Rank = r.Rank,
            CurrentGroupSize = r.CurrentGroupSize,
            TotalRequired = r.TotalRequired,
            PlayersNeeded = r.PlayersNeeded,
            Status = r.Status.ToString(),
            MatchGroupId = r.MatchGroupId?.ToString(),
            CreatedAt = r.CreatedAt
        };
    }
}
