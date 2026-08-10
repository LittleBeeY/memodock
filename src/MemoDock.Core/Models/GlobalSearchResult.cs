namespace MemoDock.Core.Models;

/// <summary>一条跨软件搜索结果：所属软件与命中的记录。</summary>
/// <param name="Notebook">记录所属软件。</param>
/// <param name="Entry">命中的记录。</param>
public sealed record GlobalSearchResult(AppNotebook Notebook, MemoEntry Entry);
