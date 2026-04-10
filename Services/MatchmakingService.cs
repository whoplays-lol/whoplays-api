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
                Mode = "Casual", // will be overwritten after Groq parse
                CurrentGroupSize = dto.CurrentGroupSize,
                TotalRequired = 2,
                PlayersNeeded = 1,
                ExcludedSessionIds = dto.ExcludedSessionIds ?? new List<string>(),
                IsSemanticSearch = true,
                Descripcion = dto.Descripcion.Trim()
            };

            // Use pre-parsed profile if provided, otherwise call Groq
            if (dto.PerfilParseado != null)
            {
                semanticReq.PerfilParseado = dto.PerfilParseado;
            }
            else
            {
                try
                {
                    semanticReq.PerfilParseado = await _groq.ParseDescripcionAsync(dto.Descripcion.Trim(), game.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Groq parse failed: {Error}", ex.Message);
                }
            }

            // Post-parse: recalculate group sizes using resolved mode teamSize
            var parsedProfile = semanticReq.PerfilParseado;
            if (parsedProfile != null)
            {
                string? parsedModeKey = parsedProfile.Modo;
                int? modeTeamSize = null;
                if (!string.IsNullOrEmpty(parsedModeKey))
                {
                    var modeDef = game.Modes.FirstOrDefault(m => m.Key.Equals(parsedModeKey, StringComparison.OrdinalIgnoreCase));
                    if (modeDef != null && modeDef.TeamSize > 0)
                        modeTeamSize = modeDef.TeamSize;
                }
                if (!modeTeamSize.HasValue)
                    modeTeamSize = game.Modes.Where(m => m.TeamSize > 0).Max(m => (int?)m.TeamSize);

                if (modeTeamSize.HasValue)
                {
                    var total = parsedProfile.TamañoEquipoBuscado;
                    var current = parsedProfile.TamañoGrupoActual;

                    if (total.HasValue && total.Value < modeTeamSize.Value && !current.HasValue)
                    {
                        int seeking = total.Value;
                        int realCurrent = modeTeamSize.Value - seeking;
                        if (realCurrent >= 1)
                        {
                            semanticReq.PerfilParseado = parsedProfile with
                            {
                                TamañoEquipoBuscado = modeTeamSize.Value,
                                TamañoGrupoActual = realCurrent
                            };
                        }
                    }
                    else if (total.HasValue && current.HasValue && total.Value != modeTeamSize.Value)
                    {
                        if (current.Value + total.Value == modeTeamSize.Value)
                        {
                            semanticReq.PerfilParseado = parsedProfile with
                            {
                                TamañoEquipoBuscado = modeTeamSize.Value
                            };
                        }
                    }
                }
            }

            // Resolve mode, total size and rank from parsed profile
            {
                var parsedMode = semanticReq.PerfilParseado?.Modo;
                var parsedSize = semanticReq.PerfilParseado?.TamañoEquipoBuscado;
                var parsedEstilo = semanticReq.PerfilParseado?.Estilo ?? "any";

                string resolvedMode = "Casual";
                if (!string.IsNullOrEmpty(parsedMode))
                {
                    var validMode = game.Modes.FirstOrDefault(m => m.Key.Equals(parsedMode, StringComparison.OrdinalIgnoreCase));
                    if (validMode != null) resolvedMode = validMode.Key;
                }
                else if (parsedEstilo == "ranked")
                {
                    var rankedMode = game.Modes.FirstOrDefault(m => m.RankRequired);
                    if (rankedMode != null) resolvedMode = rankedMode.Key;
                }
                else
                {
                    var casualMode = game.Modes.FirstOrDefault(m => !m.RankRequired);
                    if (casualMode != null) resolvedMode = casualMode.Key;
                }

                int resolvedTotal = 2;
                if (parsedSize.HasValue && parsedSize.Value >= 2)
                    resolvedTotal = parsedSize.Value;
                else
                {
                    var modeDef = game.Modes.FirstOrDefault(m => m.Key == resolvedMode);
                    if (modeDef != null && modeDef.TeamSize > 0) resolvedTotal = modeDef.TeamSize;
                }

                int resolvedCurrentGroupSize = dto.CurrentGroupSize;
                if (semanticReq.PerfilParseado?.TamañoGrupoActual.HasValue == true)
                    resolvedCurrentGroupSize = semanticReq.PerfilParseado.TamañoGrupoActual.Value;

                semanticReq.Mode = resolvedMode;
                semanticReq.TotalRequired = resolvedTotal;
                semanticReq.CurrentGroupSize = resolvedCurrentGroupSize;
                semanticReq.PlayersNeeded = resolvedTotal - resolvedCurrentGroupSize;

                if (!string.IsNullOrEmpty(semanticReq.PerfilParseado?.Rango))
                    semanticReq.Rank = NormalizeRank(semanticReq.PerfilParseado.Rango);
            }

            _queueStore.Add(semanticReq);

            _logger.LogInformation(
                "Enqueued semantic {Id} — alias={Alias} game={Game} server={Server} resolvedMode={Mode} total={Total}",
                semanticReq.Id, semanticReq.Alias, game.Name, dto.Server, semanticReq.Mode, semanticReq.TotalRequired);

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
            PlayersNeeded = totalRequired - dto.CurrentGroupSize,
            ExcludedSessionIds = dto.ExcludedSessionIds ?? new List<string>()
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

        // All pending requests (classic and IA-resolved) participate in the same pool,
        // grouped by GameId + Server + Mode + TeamFormat
        var groups = pending
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
            while (true)
            {
                var candidates = group
                    .Where(r => !matchedIds.Contains(r.Id))
                    .OrderBy(r => r.CreatedAt)
                    .ToList();

                if (candidates.Count < 2) break;

                bool rankRequired = GameDefinitions.IsRankRequired(group.Key.GameId, group.Key.Mode);

                List<QueueRequest>? combination = rankRequired
                    ? FindMatchWithRankTolerance(candidates, group.Key.GameId)
                    : FindCombination(candidates, candidates.First().TotalRequired);

                if (combination == null) break;

                string? matchReason = combination.Count == 2 && combination.Any(r => r.PerfilParseado != null)
                    ? BuildMatchReason(combination[0], combination[1])
                    : null;

                var matchGroup = new MatchGroup
                {
                    GameId = group.Key.GameId,
                    Server = group.Key.Server,
                    Mode = group.Key.Mode,
                    TeamFormat = string.IsNullOrEmpty(group.Key.TeamFormat) ? null : group.Key.TeamFormat,
                    Rank = combination.First().Rank,
                    TotalPlayers = combination.Sum(r => r.CurrentGroupSize),
                    MatchReason = matchReason
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

                _logger.LogInformation("Match created {MatchId} — {Count} groups, game={GameId} mode={Mode}{Reason}",
                    matchGroup.Id, combination.Count, group.Key.GameId, group.Key.Mode,
                    matchReason != null ? $" — {matchReason}" : "");

                foreach (var req in combination)
                {
                    await _hub.Clients.Group(req.Id.ToString())
                        .SendAsync("MatchFound", matchGroup.Id.ToString());
                }
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

    private int ComputeSemanticScore(QueueRequest a, QueueRequest b)
    {
        var pa = a.PerfilParseado;
        var pb = b.PerfilParseado;
        int score = 0;

        // Team size compatibility — hard filter
        int combined = a.CurrentGroupSize + b.CurrentGroupSize;
        if (pa?.TamañoEquipoBuscado != null && pa.TamañoEquipoBuscado != combined) return 0;
        if (pb?.TamañoEquipoBuscado != null && pb.TamañoEquipoBuscado != combined) return 0;

        // If no profiles parsed, allow match with base score 1
        if (pa == null && pb == null) return 1;

        // Role compatibility: A offers what B needs and vice versa
        bool roleMatch =
            (pa?.Rol != null && pb?.BuscaRol != null && pa.Rol.Equals(pb.BuscaRol, StringComparison.OrdinalIgnoreCase)) ||
            (pb?.Rol != null && pa?.BuscaRol != null && pb.Rol.Equals(pa.BuscaRol, StringComparison.OrdinalIgnoreCase));
        if (roleMatch) score += 3;

        // Rank compatibility: ±1 tier
        if (pa?.Rango != null && pb?.Rango != null)
        {
            if (RanksCompatible(a.GameId, pa.Rango, pb.Rango)) score += 3;
            else return 0; // rank mismatch is a hard disqualifier
        }

        if (pa?.Rol != null && pb?.Rol != null &&
            pa.Rol.Equals(pb.Rol, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        // Estilo compatibility
        bool estiloOk =
            pa?.Estilo == "any" || pb?.Estilo == "any" ||
            pa?.Estilo == pb?.Estilo;
        if (estiloOk) score += 2;
        else return 0;

        // Language compatibility
        if (pa?.Idioma != null && pb?.Idioma != null)
        {
            if (pa.Idioma.Equals(pb.Idioma, StringComparison.OrdinalIgnoreCase)) score += 1;
            else score -= 1;
        }

        return score;
    }

    private bool RanksCompatible(int gameId, string rankA, string rankB)
    {
        var game = GameDefinitions.GetById(gameId);
        if (game?.Ranks == null) return true;
        var ranks = game.Ranks;
        var normA = NormalizeRank(rankA);
        var normB = NormalizeRank(rankB);
        int idxA = Array.FindIndex(ranks, r => r.Equals(normA, StringComparison.OrdinalIgnoreCase));
        int idxB = Array.FindIndex(ranks, r => r.Equals(normB, StringComparison.OrdinalIgnoreCase));
        _logger.LogInformation("RanksCompatible: rankA={RankA} idxA={IdxA} rankB={RankB} idxB={IdxB} diff={Diff}",
            normA, idxA, normB, idxB, idxA >= 0 && idxB >= 0 ? Math.Abs(idxA - idxB) : -1);
        if (idxA < 0 || idxB < 0) return false;
        return Math.Abs(idxA - idxB) <= 1;
    }

    private static string NormalizeRank(string rank)
    {
        if (string.IsNullOrEmpty(rank)) return rank;
        return rank.Trim().ToLowerInvariant() switch
        {
            "hierro" => "Iron",
            "bronce" or "bronze" => "Bronze",
            "plata" => "Silver",
            "oro" or "gold" => "Gold",
            "platino" or "platinum" => "Platinum",
            "esmeralda" or "emerald" => "Emerald",
            "diamante" or "diamond" => "Diamond",
            "maestro" or "master" => "Master",
            "gran maestro" or "grandmaster" => "Grandmaster",
            "retador" or "challenger" => "Challenger",
            _ => rank
        };
    }

    private string BuildMatchReason(QueueRequest a, QueueRequest b)
    {
        var parts = new List<string>();
        var pa = a.PerfilParseado;
        var pb = b.PerfilParseado;

        if (pa?.Rol != null && pb?.Rol != null)
            parts.Add($"{pa.Rol} + {pb.Rol}");
        if (pa?.Rango != null && pb?.Rango != null)
            parts.Add($"{pa.Rango} / {pb.Rango}");
        if (pa?.Estilo != null && pa.Estilo != "any")
            parts.Add(pa.Estilo);

        return parts.Count > 0 ? string.Join(" — ", parts) : "Compatible profiles";
    }

    private List<QueueRequest>? FindMatchWithRankTolerance(List<QueueRequest> candidates, int gameId)
    {
        var ranks = candidates.Select(r => r.Rank).Distinct().ToList();
        var allCombinations = new List<(List<QueueRequest> Combo, int Score)>();

        foreach (var anchorRank in ranks)
        {
            var compatible = candidates
                .Where(r => GameDefinitions.IsRankCompatible(gameId, anchorRank, r.Rank))
                .ToList();

            if (compatible.Count < 2) continue;

            int totalRequired = compatible.First().TotalRequired;
            var result = FindCombination(compatible, totalRequired);
            if (result == null) continue;

            // Use semantic score as tiebreaker when profiles are available
            int score = result.Count == 2 ? ComputeSemanticScore(result[0], result[1]) : 0;
            allCombinations.Add((result, score));
        }

        if (allCombinations.Count == 0) return null;

        // Return the highest-scoring combination (semantic score as tiebreaker)
        return allCombinations.OrderByDescending(c => c.Score).First().Combo;
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
            if (candidate.CurrentGroupSize > remaining) continue;

            bool excluded = current.Any(c =>
                c.ExcludedSessionIds.Contains(candidate.SessionId) ||
                candidate.ExcludedSessionIds.Contains(c.SessionId));
            if (excluded) continue;

            current.Add(candidate);
            var result = FindSubset(candidates, remaining - candidate.CurrentGroupSize, i + 1, current);
            if (result != null) return result;
            current.RemoveAt(current.Count - 1);
        }

        return null;
    }
}
