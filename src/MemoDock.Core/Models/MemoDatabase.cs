namespace MemoDock.Core.Models;

public sealed class MemoDatabase
{
    public int Version { get; set; } = 1;

    public List<AppNotebook> Apps { get; set; } = [];
}
