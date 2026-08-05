using System.Windows.Media;
using MemoDock.Core.Models;

namespace MemoDock.Services;

/// <summary>前台应用的一次识别快照。</summary>
/// <param name="Descriptor">软件身份描述。</param>
/// <param name="Icon">软件图标；无法提取时为 <c>null</c>。</param>
public sealed record ForegroundAppSnapshot(AppDescriptor Descriptor, ImageSource? Icon);
