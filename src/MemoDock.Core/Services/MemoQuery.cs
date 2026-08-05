using MemoDock.Core.Models;

namespace MemoDock.Core.Services;

/// <summary>备忘录记录的查询与过滤。</summary>
public static class MemoQuery
{
    /// <summary>
    /// 按类型和关键词过滤记录，并按更新时间倒序返回。
    /// </summary>
    /// <param name="entries">要过滤的记录集合。</param>
    /// <param name="kind">目标记录类型。</param>
    /// <param name="query">搜索关键词；为 null 或空白时返回全部记录。</param>
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
                entry.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                entry.Body.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.UpdatedAt);
    }
}
