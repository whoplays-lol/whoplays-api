using System.Collections.Concurrent;
using WannaFill.API.Models;

namespace WannaFill.API.Stores;

/// <summary>
/// Thread-safe in-memory store for queue requests. Registered as Singleton.
/// </summary>
public class InMemoryQueueStore
{
    private readonly ConcurrentDictionary<Guid, QueueRequest> _requests = new();

    public void Add(QueueRequest request) =>
        _requests[request.Id] = request;

    public QueueRequest? GetById(Guid id) =>
        _requests.GetValueOrDefault(id);

    /// <summary>Returns all pending requests ordered by creation time (FIFO).</summary>
    public List<QueueRequest> GetPending() =>
        _requests.Values
            .Where(r => r.Status == QueueStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .ToList();

    /// <summary>Returns all requests that belong to a given match group.</summary>
    public List<QueueRequest> GetByMatchGroup(Guid matchGroupId) =>
        _requests.Values
            .Where(r => r.MatchGroupId == matchGroupId)
            .ToList();

    // ConcurrentDictionary updates are atomic for reference types stored as values,
    // but we mutate the object in place and then re-assign to ensure visibility.
    public void Update(QueueRequest request) =>
        _requests[request.Id] = request;
}
