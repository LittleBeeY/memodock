using System.Collections.ObjectModel;

namespace MemoDock.Core.Models;

public sealed class AppNotebook
{
    public string AppId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ExecutablePath { get; set; } = string.Empty;

    public ObservableCollection<MemoEntry> Entries { get; set; } = [];
}
