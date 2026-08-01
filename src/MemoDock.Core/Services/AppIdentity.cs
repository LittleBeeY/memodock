namespace MemoDock.Core.Services;

public static class AppIdentity
{
    private const string WindowsAppsMarker = "\\WindowsApps\\";
    private const string PackagedAppPrefix = "windows-package:";

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
