using Microsoft.AspNetCore.SignalR;

namespace WannaFill.API.Hubs;

public class MatchmakingHub : Hub
{
    private readonly ILogger<MatchmakingHub> _logger;

    public MatchmakingHub(ILogger<MatchmakingHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Client calls this to subscribe to updates for their queue request.
    /// </summary>
    public async Task JoinQueueGroup(string requestId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, requestId);
        _logger.LogDebug("Connection {ConnectionId} joined queue group {RequestId}", Context.ConnectionId, requestId);
    }

    /// <summary>
    /// Client calls this to subscribe to match chat.
    /// </summary>
    public async Task JoinMatchGroup(string matchGroupId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"match-{matchGroupId}");
        _logger.LogDebug("Connection {ConnectionId} joined match group {MatchGroupId}", Context.ConnectionId, matchGroupId);
    }

    public async Task LeaveMatchGroup(string matchGroupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"match-{matchGroupId}");
    }

    /// <summary>
    /// Client calls this when intentionally leaving a match.
    /// Notifies other participants and removes self from the group.
    /// </summary>
    public async Task LeaveMatch(string matchGroupId, string alias)
    {
        // Notify all OTHER participants in the match group that this player left
        await Clients.OthersInGroup($"match-{matchGroupId}")
            .SendAsync("ParticipantLeft", alias);

        // Remove self from the group
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"match-{matchGroupId}");

        _logger.LogInformation("Player {Alias} left match {MatchGroupId}", alias, matchGroupId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Connection {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
