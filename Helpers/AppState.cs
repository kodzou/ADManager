using System.Net;

namespace ADManager.Helpers;

public static class AppState
{
    public static List<object> LastResults { get; set; } = new();
}

public static class CredentialCache
{
    private static readonly Dictionary<string, (NetworkCredential Cred, DateTime Expiry)>
        _store = new(StringComparer.OrdinalIgnoreCase);

    public static NetworkCredential? Get(string domain)
    {
        if (!_store.TryGetValue(domain, out var entry)) return null;
        if (entry.Expiry <= DateTime.Now) { _store.Remove(domain); return null; }
        return entry.Cred;
    }

    public static void Store(string domain, NetworkCredential cred)
    {
        // до 23:59:59 текущего дня
        _store[domain] = (cred, DateTime.Today.AddDays(1).AddSeconds(-1));
    }

    public static void Invalidate(string domain) => _store.Remove(domain);
}

public record BulkUserEntry
{
    public string Domain      { get; init; } = "";   // без .local
    public string DomainFull  { get; init; } = "";   // с .local
    public string Login       { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Position    { get; init; } = "";
    public string Department  { get; init; } = "";
    public string Manager     { get; init; } = "";
    public string DN          { get; init; } = "";
}
