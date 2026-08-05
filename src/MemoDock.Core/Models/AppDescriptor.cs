namespace MemoDock.Core.Models;

/// <summary>描述一个被记录软件的稳定身份。</summary>
/// <param name="AppId">稳定身份标识，用于隔离记录。</param>
/// <param name="DisplayName">界面展示名称。</param>
/// <param name="ExecutablePath">可执行文件路径；可能为空字符串。</param>
public sealed record AppDescriptor(string AppId, string DisplayName, string ExecutablePath);
