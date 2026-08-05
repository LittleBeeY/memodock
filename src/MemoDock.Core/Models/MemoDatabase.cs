namespace MemoDock.Core.Models;

/// <summary>备忘录数据库：包含结构版本号与各软件的记录本集合。</summary>
public sealed class MemoDatabase
{
    /// <summary>数据库结构版本，由 <see cref="MemoDock.Core.Services.MemoMigrator"/> 维护。</summary>
    public int Version { get; set; } = 2;

    /// <summary>各软件的独立记录本。</summary>
    public List<AppNotebook> Apps { get; set; } = [];
}
