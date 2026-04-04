using WannaFill.API.DTOs;
using WannaFill.API.Models;

namespace WannaFill.API.Services;

public interface IMatchmakingService
{
    Task<QueueRequestDto> EnqueueAsync(CreateQueueRequestDto dto);
    Task<QueueRequestDto?> GetQueueRequestAsync(Guid id);
    Task<bool> CancelAsync(Guid id, string sessionId);
    Task<MatchGroupDto?> GetMatchGroupAsync(Guid matchGroupId);
    Task RunMatchmakingAsync();
    Dictionary<string, int> GetQueueStats();
    Dictionary<string, List<string>> GetQueueDescriptions();
}
