namespace MemoDock.Core.Services;

/// <summary>根据可执行文件路径计算稳定的软件身份标识。</summary>
public static class AppIdentity
{
    private const string WindowsAppsMarker = "\\WindowsApps\\";
    private const string PackagedAppPrefix = "windows-package:";

    /// <summary>
    /// 计算软件身份：普通程序用规范化后的完整路径，商店应用用不含版本号的稳定包身份。
    /// </summary>
    /// <param name="executablePath">可执行文件路径，可能为空字符串。</param>
    /// <param name="fallbackProcessName">无法取得路径时的进程名回退。</param>
    public static string Create(string executablePath, string fallbackProcessName)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return fallbackProcessName.Trim().ToLowerInvariant();
        }

        var fullPath = Path.GetFullPath(executablePath);
        return TryCreatePackagedAppId(fullPath, out var packagedAppId)
            ? packagedAppId
            : fullPath.ToLowerInvariant();
    }

    /// <summary>
    /// 尝试把 Windows 商店应用路径解析为不含版本号的稳定身份。
    /// 仅当路径位于 WindowsApps 目录且包名结构合法时成功。
    /// </summary>
    /// <param name="executablePath">商店应用可执行文件路径。</param>
    /// <param name="appId">解析出的稳定身份；失败时为空字符串。</param>
    public static bool TryCreatePackagedAppId(string executablePath, out string appId)
    {
        appId = string.Empty;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        var normalizedPath = executablePath.Replace('/', '\\');
        var markerIndex = normalizedPath.IndexOf(WindowsAppsMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return false;
        }

        var packagePath = normalizedPath[(markerIndex + WindowsAppsMarker.Length)..];
        var separatorIndex = packagePath.IndexOf('\\');
        if (separatorIndex <= 0 || separatorIndex == packagePath.Length - 1)
        {
            return false;
        }

        var packageFullName = packagePath[..separatorIndex];
        var relativeExecutablePath = packagePath[(separatorIndex + 1)..];
        var packageParts = packageFullName.Split('_');
        if (packageParts.Length < 5 ||
            !Version.TryParse(packageParts[1], out _) ||
            !IsPackageArchitecture(packageParts[2]))
        {
            return false;
        }

        var packageFamilyName = $"{packageParts[0]}_{packageParts[^1]}";
        appId = $"{PackagedAppPrefix}{packageFamilyName}!{relativeExecutablePath}".ToLowerInvariant();
        return true;
    }

    private static bool IsPackageArchitecture(string value)
    {
        return value.Equals("x86", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("x64", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("arm", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("arm64", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("neutral", StringComparison.OrdinalIgnoreCase);
    }
}
