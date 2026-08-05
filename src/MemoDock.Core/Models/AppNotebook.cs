using System.Collections.ObjectModel;

namespace MemoDock.Core.Models;

/// <summary>某个软件的记录本，包含软件身份与属于它的全部记录。</summary>
public sealed class AppNotebook
{
    /// <summary>软件身份标识（详见 <c>AppIdentity</c>）。</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>界面展示的软件名称。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>软件可执行文件路径。</summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>该软件下的笔记与待办记录。</summary>
    public ObservableCollection<MemoEntry> Entries { get; set; } = [];
}
