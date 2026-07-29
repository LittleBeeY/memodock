using MemoDock.Core.Models;

namespace MemoDock.Core.Services;

public static class MemoQuery
{
    public static IEnumerable<MemoEntry> Filter(
        IEnumerable<MemoEntry> entries,
        MemoKind kind,
        string? query)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var normalizedQuery = query?.Trim();

        return entries
            .Where(entry => entry.Kind == kind)
            .Where(entry =>
                string.IsNullOrEmpty(normalizedQuery) ||
                entry.Title.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase) ||
                entry.Body.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(entry => entry.UpdatedAt);
    }
}
