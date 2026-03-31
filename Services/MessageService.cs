using Microsoft.AspNetCore.SignalR;
using WannaFill.API.DTOs;
using WannaFill.API.Hubs;
using WannaFill.API.Models;
using WannaFill.API.Stores;

namespace WannaFill.API.Services;

public class MessageService : IMessageService
{
    private readonly InMemoryQueueStore _queueStore;
    private readonly InMemoryChatStore _chatStore;
    private readonly IHubContext<MatchmakingHub> _hub;

    public MessageService(
        InMemoryQueueStore queueStore,
        InMemoryChatStore chatStore,
        IHubContext<MatchmakingHub> hub)
    {
        _queueStore = queueStore;
        _chatStore = chatStore;
        _hub = hub;
    }

    public async Task<MessageDto?> SendMessageAsync(Guid matchGroupId, SendMessageDto dto)
    {
        // Verify the sender is a participant of this match
        var participant = _queueStore.GetByMatchGroup(matchGroupId)
            .FirstOrDefault(r => r.SessionId == dto.SessionId);

        if (participant == null) return null;

        var message = new TemporaryMessage
        {
            MatchGroupId = matchGroupId,
            SessionId = dto.SessionId,
            Alias = participant.Alias,
            Content = dto.Content.Trim()
        };

        _chatStore.Add(message);

        var messageDto = MessageDto.From(message);

        // Broadcast to all participants in this match via SignalR
        await _hub.Clients.Group($"match-{matchGroupId}")
            .SendAsync("NewMessage", messageDto);

        return messageDto;
    }

    public Task<List<MessageDto>> GetMessagesAsync(Guid matchGroupId)
    {
        var messages = _chatStore.GetByMatchGroup(matchGroupId)
            .Select(MessageDto.From)
            .ToList();

        return Task.FromResult(messages);
    }
}
