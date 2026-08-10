using MemoDock.Core.Models;

namespace MemoDock.Core.Services;

/// <summary>备忘录记录的查询与过滤。</summary>
public static class MemoQuery
{
    /// <summary>
    /// 按类型和关键词过滤记录。支持多个以空白分隔的关键词（全部命中）。
    /// 待办未完成项排在已完成项之前，其余按更新时间倒序。软删除记录不会出现。
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

        var keywords = SplitKeywords(query);

        return entries
            .Where(entry => !entry.IsDeleted && entry.Kind == kind)
            .Where(entry => Matches(entry, keywords))
            .OrderBy(entry => entry.Kind == MemoKind.Todo && entry.IsCompleted)
            .ThenByDescending(entry => entry.UpdatedAt);
    }

    /// <summary>
    /// 返回已删除（回收站）的记录，支持多关键词过滤并按更新时间倒序。
    /// </summary>
    /// <param name="entries">要过滤的记录集合。</param>
    /// <param name="query">搜索关键词；为 null 或空白时返回全部已删除记录。</param>
    public static IEnumerable<MemoEntry> FilterDeleted(
        IEnumerable<MemoEntry> entries,
        string? query)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var keywords = SplitKeywords(query);

        return entries
            .Where(entry => entry.IsDeleted)
            .Where(entry => Matches(entry, keywords))
            .OrderByDescending(entry => entry.UpdatedAt);
    }

    /// <summary>
    /// 跨全部软件搜索未删除的记录，支持多关键词，结果携带所属软件，按更新时间倒序。
    /// </summary>
    /// <param name="notebooks">要搜索的全部记录本。</param>
    /// <param name="query">搜索关键词；为 null 或空白时返回全部记录。</param>
    public static IEnumerable<GlobalSearchResult> SearchAll(
        IEnumerable<AppNotebook> notebooks,
        string? query)
    {
        ArgumentNullException.ThrowIfNull(notebooks);

        var keywords = SplitKeywords(query);

        return notebooks
            .Where(notebook => notebook.Entries.Count > 0)
            .SelectMany(notebook => notebook.Entries
                .Where(entry => !entry.IsDeleted && Matches(entry, keywords))
                .Select(entry => new GlobalSearchResult(notebook, entry)))
            .OrderByDescending(result => result.Entry.UpdatedAt);
    }

    /// <summary>把查询文本拆成关键词；空白查询返回空数组（匹配全部）。</summary>
    private static string[] SplitKeywords(string? query)
    {
        return query?.Split(
            [' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
    }

    /// <summary>记录是否命中全部关键词（标题或正文任一匹配）。</summary>
    private static bool Matches(MemoEntry entry, IReadOnlyList<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            if (!entry.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) &&
                !entry.Body.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
