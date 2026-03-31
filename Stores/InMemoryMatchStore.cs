using System.Collections.Concurrent;
using WannaFill.API.Models;

namespace WannaFill.API.Stores;

/// <summary>
/// Thread-safe in-memory store for completed match groups. Registered as Singleton.
/// </summary>
public class InMemoryMatchStore
{
    private readonly ConcurrentDictionary<Guid, MatchGroup> _matches = new();

    public void Add(MatchGroup match) =>
        _matches[match.Id] = match;

    public MatchGroup? GetById(Guid id) =>
        _matches.GetValueOrDefault(id);
}
