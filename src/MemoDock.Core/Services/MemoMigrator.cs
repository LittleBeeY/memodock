using MemoDock.Core.Models;

namespace MemoDock.Core.Services;

/// <summary>负责备忘录数据库的版本迁移，保证旧数据能被安全升级到最新结构。</summary>
public static class MemoMigrator
{
    /// <summary>当前数据库结构版本。</summary>
    public const int CurrentVersion = 3;

    /// <summary>
    /// 将数据库迁移到最新版本。
    /// </summary>
    /// <param name="database">要迁移的数据库；会就地修改。</param>
    /// <returns>是否发生了数据改动（需要调用方持久化）。</returns>
    public static bool Migrate(MemoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        var changed = database.Version < CurrentVersion;
        database.Version = CurrentVersion;

        foreach (var notebook in database.Apps)
        {
            if (!AppIdentity.TryCreatePackagedAppId(notebook.ExecutablePath, out var stableAppId) ||
                string.Equals(notebook.AppId, stableAppId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            notebook.AppId = stableAppId;
            changed = true;
        }

        foreach (var group in database.Apps
                     .GroupBy(notebook => notebook.AppId, StringComparer.OrdinalIgnoreCase)
                     .Where(group =>
                         group.Key.StartsWith("windows-package:", StringComparison.OrdinalIgnoreCase) &&
                         group.Count() > 1)
                     .ToList())
        {
            var target = group.First();
            foreach (var source in group.Skip(1))
            {
                MergeEntries(target, source);
                database.Apps.Remove(source);
            }

            changed = true;
        }

        return changed;
    }

    /// <summary>把源软件中的记录并入目标软件，同一记录按更新时间取新。</summary>
    private static void MergeEntries(AppNotebook target, AppNotebook source)
    {
        foreach (var sourceEntry in source.Entries)
        {
            var existing = target.Entries.FirstOrDefault(entry => entry.Id == sourceEntry.Id);
            if (existing is null)
            {
                target.Entries.Add(sourceEntry);
                continue;
            }

            if (sourceEntry.UpdatedAt <= existing.UpdatedAt)
            {
                continue;
            }

            var index = target.Entries.IndexOf(existing);
            target.Entries[index] = sourceEntry;
        }
    }
}
