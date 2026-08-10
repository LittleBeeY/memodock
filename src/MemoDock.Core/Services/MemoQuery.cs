using MemoDock.Core.Models;

namespace MemoDock.Core.Services;

/// <summary>备忘录记录的查询与过滤。</summary>
public static class MemoQuery
{
    /// <summary>
    /// 按类型和关键词过滤记录，并按更新时间倒序返回。软删除记录不会出现在结果中。
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
            .Where(entry => !entry.IsDeleted && entry.Kind == kind)
            .Where(entry =>
                string.IsNullOrEmpty(normalizedQuery) ||
                entry.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                entry.Body.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.UpdatedAt);
    }

    /// <summary>
    /// 返回已删除（回收站）的记录，可按关键词过滤并按更新时间倒序。
    /// </summary>
    /// <param name="entries">要过滤的记录集合。</param>
    /// <param name="query">搜索关键词；为 null 或空白时返回全部已删除记录。</param>
    public static IEnumerable<MemoEntry> FilterDeleted(
        IEnumerable<MemoEntry> entries,
        string? query)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var normalizedQuery = query?.Trim();

        return entries
            .Where(entry => entry.IsDeleted)
            .Where(entry =>
                string.IsNullOrEmpty(normalizedQuery) ||
                entry.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                entry.Body.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.UpdatedAt);
    }

    /// <summary>
    /// 跨全部软件搜索未删除的记录，结果携带所属软件，按更新时间倒序。
    /// </summary>
    /// <param name="notebooks">要搜索的全部记录本。</param>
    /// <param name="query">搜索关键词；为 null 或空白时返回全部记录。</param>
    public static IEnumerable<GlobalSearchResult> SearchAll(
        IEnumerable<AppNotebook> notebooks,
        string? query)
    {
        ArgumentNullException.ThrowIfNull(notebooks);

        var normalizedQuery = query?.Trim();

        return notebooks
            .Where(notebook => notebook.Entries.Count > 0)
            .SelectMany(notebook => notebook.Entries
                .Where(entry => !entry.IsDeleted)
                .Where(entry =>
                    string.IsNullOrEmpty(normalizedQuery) ||
                    entry.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                    entry.Body.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                .Select(entry => new GlobalSearchResult(notebook, entry)))
            .OrderByDescending(result => result.Entry.UpdatedAt);
    }
}
