using Microsoft.AspNetCore.SignalR;
using WannaFill.API.DTOs;
using WannaFill.API.GameConfig;
using WannaFill.API.Hubs;
using WannaFill.API.Models;
using WannaFill.API.Stores;

namespace WannaFill.API.Services;

public class MatchmakingService : IMatchmakingService
{
    private readonly InMemoryQueueStore _queueStore;
    private readonly InMemoryMatchStore _matchStore;
    private readonly IHubContext<MatchmakingHub> _hub;
    private readonly ILogger<MatchmakingService> _logger;
    private readonly IGroqService _groq;

    // Prevents concurrent matchmaking runs from creating duplicate matches
    private static readonly SemaphoreSlim _matchmakingLock = new(1, 1);

    public MatchmakingService(
        InMemoryQueueStore queueStore,
        InMemoryMatchStore matchStore,
        IHubContext<MatchmakingHub> hub,
        ILogger<MatchmakingService> logger,
        IGroqService groq)
    {
        _queueStore = queueStore;
        _matchStore = matchStore;
        _hub = hub;
        _logger = logger;
        _groq = groq;
    }

    public async Task<QueueRequestDto> EnqueueAsync(CreateQueueRequestDto dto)
    {
        var game = GameDefinitions.GetById(dto.GameId)
            ?? throw new ArgumentException($"Invalid game ID: {dto.GameId}");

        if (!game.Servers.Contains(dto.Server))
            throw new ArgumentException($"Server '{dto.Server}' is not valid for {game.Name}");

        // Semantic search path — validates game + server, skips mode/rank validation
        if (!string.IsNullOrEmpty(dto.Descripcion))
        {
            var semanticReq = new QueueRequest
            {
                SessionId = dto.SessionId,
                Alias = dto.Alias,
                GameId = dto.GameId,
                Server = dto.Server,
                Mode = "Semantic",
                CurrentGroupSize = dto.CurrentGroupSize,
                TotalRequired = 2,
                PlayersNeeded = 1,
                ExcludedSessionIds = dto.ExcludedSessionIds ?? new List<string>(),
                IsSemanticSearch = true,
                Descripcion = dto.Descripcion.Trim()
            };

            _queueStore.Add(semanticReq);

            _logger.LogInformation("Enqueued semantic {Id} — alias={Alias} game={Game} server={Server}",
                semanticReq.Id, semanticReq.Alias, game.Name, dto.Server);

            await RunMatchmakingAsync();

            return QueueRequestDto.From(_queueStore.GetById(semanticReq.Id) ?? semanticReq);
        }

        var mode = game.Modes.FirstOrDefault(m => m.Key == dto.Mode)
            ?? throw new ArgumentException($"Mode '{dto.Mode}' is not valid for {game.Name}");

        if (mode.TeamSize == 0)
        {
            if (string.IsNullOrEmpty(dto.TeamFormat))
                throw new ArgumentException("TeamFormat is required for this game mode");
            if (game.TeamFormats == null || !game.TeamFormats.Any(f => f.Key == dto.TeamFormat))
                throw new ArgumentException($"TeamFormat '{dto.TeamFormat}' is not valid");
        }

        if (mode.RankRequired && string.IsNullOrEmpty(dto.Rank))
            throw new ArgumentException("Rank is required for this game mode");
        if (!mode.RankRequired)
            dto.Rank = null;

        if (!string.IsNullOrEmpty(dto.Rank) && !game.Ranks.Contains(dto.Rank))
            throw new ArgumentException($"Rank '{dto.Rank}' is not valid for {game.Name}");

        int totalRequired = GameDefinitions.GetTeamSize(dto.GameId, dto.Mode, dto.TeamFormat);

        if (dto.CurrentGroupSize < 1 || dto.CurrentGroupSize >= totalRequired)
            throw new ArgumentException($"CurrentGroupSize must be between 1 and {totalRequired - 1}");

        var request = new QueueRequest
        {
            SessionId = dto.SessionId,
            Alias = dto.Alias,
            GameId = dto.GameId,
            Server = dto.Server,
            Mode = dto.Mode,
            TeamFormat = dto.TeamFormat,
            Rank = dto.Rank,
            CurrentGroupSize = dto.CurrentGroupSize,
            TotalRequired = totalRequired,
            PlayersNeeded = totalRequired - dto.CurrentGroupSize
        };

        _queueStore.Add(request);

        _logger.LogInformation("Enqueued {Id} — {Game} {Mode} server={Server}",
            request.Id, game.Name, dto.Mode, dto.Server);

        // Try to match immediately after enqueueing
        await RunMatchmakingAsync();

        // Read back from store (status may have changed to Matched)
        return QueueRequestDto.From(_queueStore.GetById(request.Id) ?? request);
    }

    public Task<QueueRequestDto?> GetQueueRequestAsync(Guid id)
    {
        var request = _queueStore.GetById(id);
        return Task.FromResult(request == null ? null : QueueRequestDto.From(request));
    }

    public Task<bool> CancelAsync(Guid id, string sessionId)
    {
        var request = _queueStore.GetById(id);
        if (request == null || request.SessionId != sessionId) return Task.FromResult(false);
        if (request.Status != QueueStatus.Pending) return Task.FromResult(false);

        request.Status = QueueStatus.Cancelled;
        _queueStore.Update(request);
        return Task.FromResult(true);
    }

    public Task<MatchGroupDto?> GetMatchGroupAsync(Guid matchGroupId)
    {
        var match = _matchStore.GetById(matchGroupId);
        if (match == null) return Task.FromResult<MatchGroupDto?>(null);

        var participants = _queueStore.GetByMatchGroup(matchGroupId);
        return Task.FromResult<MatchGroupDto?>(MatchGroupDto.From(match, participants));
    }

    public async Task RunMatchmakingAsync()
    {
        if (!await _matchmakingLock.WaitAsync(0))
            return; // Another run is already in progress

        try
        {
            await PerformMatchmakingAsync();
        }
        finally
        {
            _matchmakingLock.Release();
        }
    }

    private async Task PerformMatchmakingAsync()
    {
        var pending = _queueStore.GetPending();
        if (pending.Count < 2) return;

        var groups = pending
            .Where(r => !r.IsSemanticSearch)
            .GroupBy(r => new
            {
                r.GameId,
                r.Server,
                r.Mode,
                TeamFormat = r.TeamFormat ?? ""
            });

        var matchedIds = new HashSet<Guid>();

        foreach (var group in groups)
        {
            var candidates = group
                .Where(r => !matchedIds.Contains(r.Id))
                .OrderBy(r => r.CreatedAt)
                .ToList();

            if (candidates.Count < 2) continue;

            bool rankRequired = GameDefinitions.IsRankRequired(group.Key.GameId, group.Key.Mode);

            List<QueueRequest>? combination = rankRequired
                ? FindMatchWithRankTolerance(candidates, group.Key.GameId)
                : FindCombination(candidates, candidates.First().TotalRequired);

            if (combination == null) continue;

            var matchGroup = new MatchGroup
            {
                GameId = group.Key.GameId,
                Server = group.Key.Server,
                Mode = group.Key.Mode,
                TeamFormat = string.IsNullOrEmpty(group.Key.TeamFormat) ? null : group.Key.TeamFormat,
                Rank = combination.First().Rank,
                TotalPlayers = combination.Sum(r => r.CurrentGroupSize)
            };

            _matchStore.Add(matchGroup);

            foreach (var req in combination)
            {
                req.Status = QueueStatus.Matched;
                req.MatchGroupId = matchGroup.Id;
                req.MatchedAt = DateTime.UtcNow;
                _queueStore.Update(req);
                matchedIds.Add(req.Id);
            }

            _logger.LogInformation("Match created {MatchId} — {Count} groups, game={GameId} mode={Mode}",
                matchGroup.Id, combination.Count, group.Key.GameId, group.Key.Mode);

            // Notify all matched participants via SignalR
            foreach (var req in combination)
            {
                await _hub.Clients.Group(req.Id.ToString())
                    .SendAsync("MatchFound", matchGroup.Id.ToString());
            }
        }

        // Semantic matching — grouped by gameId + server
        var semanticPending = _queueStore.GetPending()
            .Where(r => r.IsSemanticSearch
                     && !string.IsNullOrEmpty(r.Descripcion)
                     && !matchedIds.Contains(r.Id))
            .ToList();

        if (semanticPending.Count < 2) return;

        var semanticGroups = semanticPending.GroupBy(r => new { r.GameId, r.Server });

        foreach (var group in semanticGroups)
        {
            var candidates = group
                .Where(r => !matchedIds.Contains(r.Id))
                .OrderBy(r => r.CreatedAt)
                .ToList();

            if (candidates.Count < 2) continue;

            QueueRequest? bestA = null, bestB = null;
            int bestScore = 0;
            string bestReason = "";

            for (int i = 0; i < candidates.Count; i++)
            {
                for (int j = i + 1; j < candidates.Count; j++)
                {
                    var a = candidates[i];
                    var b = candidates[j];

                    try
                    {
                        var gameName = GameDefinitions.GetById(a.GameId)?.Name ?? "Unknown";
                        var result = await _groq.CheckCompatibilityAsync(
                            a.Descripcion!,
                            b.Descripcion!,
                            gameName);

                        _logger.LogInformation(
                            "Semantic check [{AliasA}] vs [{AliasB}]: compatible={C} score={S} reason={R}",
                            a.Alias, b.Alias, result.Compatible, result.Score, result.Reason);

                        if (result.Compatible && result.Score >= 7 && result.Score > bestScore)
                        {
                            bestScore = result.Score;
                            bestA = a;
                            bestB = b;
                            bestReason = result.Reason;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Groq check failed: {Error}", ex.Message);
                    }
                }
            }

            if (bestA != null && bestB != null)
            {
                var matchGroup = new MatchGroup
                {
                    GameId = bestA.GameId,
                    Server = bestA.Server,
                    Mode = bestA.Mode,
                    TotalPlayers = bestA.CurrentGroupSize + bestB.CurrentGroupSize,
                    MatchReason = bestReason
                };

                _matchStore.Add(matchGroup);

                foreach (var req in new[] { bestA, bestB })
                {
                    req.Status = QueueStatus.Matched;
                    req.MatchGroupId = matchGroup.Id;
                    req.MatchedAt = DateTime.UtcNow;
                    _queueStore.Update(req);
                    matchedIds.Add(req.Id);

                    await _hub.Clients.Group(req.Id.ToString())
                        .SendAsync("MatchFound", matchGroup.Id.ToString());
                }

                _logger.LogInformation(
                    "Semantic match {MatchId} score={Score} [{AliasA}]+[{AliasB}] — {Reason}",
                    matchGroup.Id, bestScore, bestA.Alias, bestB.Alias, bestReason);
            }
        }
    }

    public Dictionary<string, List<string>> GetQueueDescriptions()
    {
        var pending = _queueStore.GetPending();
        return pending
            .Where(r => r.IsSemanticSearch && !string.IsNullOrEmpty(r.Descripcion))
            .GroupBy(r => GameDefinitions.GetById(r.GameId)?.Name ?? "Other")
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => r.Descripcion!).ToList()
            );
    }

    public Dictionary<string, int> GetQueueStats()
    {
        var pending = _queueStore.GetPending();
        return pending
            .GroupBy(r => GameDefinitions.GetById(r.GameId)?.Name ?? "Other")
            .ToDictionary(g => g.Key, g => g.Sum(r => r.CurrentGroupSize));
    }

    private List<QueueRequest>? FindMatchWithRankTolerance(List<QueueRequest> candidates, int gameId)
    {
        var ranks = candidates.Select(r => r.Rank).Distinct().ToList();

        foreach (var anchorRank in ranks)
        {
            var compatible = candidates
                .Where(r => GameDefinitions.IsRankCompatible(gameId, anchorRank, r.Rank))
                .ToList();

            if (compatible.Count < 2) continue;

            int totalRequired = compatible.First().TotalRequired;
            var result = FindCombination(compatible, totalRequired);
            if (result != null) return result;
        }

        return null;
    }

    /// <summary>
    /// Finds a subset of requests whose CurrentGroupSize sums exactly to targetSize.
    /// Recursive backtracking — safe for small team sizes (2–5).
    /// </summary>
    private List<QueueRequest>? FindCombination(List<QueueRequest> candidates, int targetSize) =>
        FindSubset(candidates, targetSize, 0, new List<QueueRequest>());

    private List<QueueRequest>? FindSubset(
        List<QueueRequest> candidates, int remaining, int startIndex, List<QueueRequest> current)
    {
        if (remaining == 0) return new List<QueueRequest>(current);
        if (remaining < 0 || startIndex >= candidates.Count) return null;

        for (int i = startIndex; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate.CurrentGroupSize <= remaining)
            {
                current.Add(candidate);
                var result = FindSubset(candidates, remaining - candidate.CurrentGroupSize, i + 1, current);
                if (result != null) return result;
                current.RemoveAt(current.Count - 1);
            }
        }

        return null;
    }
}
