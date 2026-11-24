namespace MomentumGroves.Mobile.Tests.Infrastructure;

internal sealed class MemorySecureStorage : ISecureStorage
{
    private readonly Dictionary<string, string> _entries = [];

    public int Count => _entries.Count;
    public Task<string?> GetAsync(string key) => Task.FromResult(_entries.GetValueOrDefault(key));
    public Task SetAsync(string key, string value)
    {
        _entries[key] = value;
        return Task.CompletedTask;
    }
    public bool Remove(string key) => _entries.Remove(key);
    public void RemoveAll() => _entries.Clear();
}
