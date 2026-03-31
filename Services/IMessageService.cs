using WannaFill.API.DTOs;

namespace WannaFill.API.Services;

public interface IMessageService
{
    Task<MessageDto?> SendMessageAsync(Guid matchGroupId, SendMessageDto dto);
    Task<List<MessageDto>> GetMessagesAsync(Guid matchGroupId);
}
