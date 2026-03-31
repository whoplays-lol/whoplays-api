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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Connection {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
