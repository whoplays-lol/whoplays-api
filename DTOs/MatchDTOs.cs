using WannaFill.API.GameConfig;
using WannaFill.API.Models;

namespace WannaFill.API.DTOs;

public class MatchGroupDto
{
    public string Id { get; set; } = null!;
    public int GameId { get; set; }
    public string GameName { get; set; } = null!;
    public string Server { get; set; } = null!;
    public string Mode { get; set; } = null!;
    public string? TeamFormat { get; set; }
    public string? Rank { get; set; }
    public int TotalPlayers { get; set; }
    public string? MatchReason { get; set; }
    public List<ParticipantDto> Participants { get; set; } = new();
    public DateTime CreatedAt { get; set; }

    public static MatchGroupDto From(MatchGroup m, IEnumerable<QueueRequest> participants)
    {
        var game = GameDefinitions.GetById(m.GameId);
        return new MatchGroupDto
        {
            Id = m.Id.ToString(),
            GameId = m.GameId,
            GameName = game?.Name ?? "Unknown",
            Server = m.Server,
            Mode = m.Mode,
            TeamFormat = m.TeamFormat,
            Rank = m.Rank,
            TotalPlayers = m.TotalPlayers,
            MatchReason = m.MatchReason,
            Participants = participants.Select(p => new ParticipantDto
            {
                Alias = p.Alias,
                CurrentGroupSize = p.CurrentGroupSize,
                SessionId = p.SessionId
            }).ToList(),
            CreatedAt = m.CreatedAt
        };
    }
}

public class ParticipantDto
{
    public string Alias { get; set; } = null!;
    public int CurrentGroupSize { get; set; }
    public string SessionId { get; set; } = null!;
}
