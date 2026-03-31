using System.Collections.Concurrent;
using WannaFill.API.Models;

namespace WannaFill.API.Stores;

/// <summary>
/// Thread-safe in-memory store for temporary chat messages. Registered as Singleton.
/// Messages are lost on restart — intentional for the MVP.
/// </summary>
public class InMemoryChatStore
{
    // Each match group has its own list, protected by a per-list lock.
    private readonly ConcurrentDictionary<Guid, List<TemporaryMessage>> _messages = new();

    public void Add(TemporaryMessage message)
    {
        var list = _messages.GetOrAdd(message.MatchGroupId, _ => new List<TemporaryMessage>());
        lock (list)
        {
            list.Add(message);
        }
    }

    public List<TemporaryMessage> GetByMatchGroup(Guid matchGroupId)
    {
        if (!_messages.TryGetValue(matchGroupId, out var list))
            return new List<TemporaryMessage>();

        lock (list)
        {
            return list.OrderBy(m => m.SentAt).ToList();
        }
    }
}
