namespace MemoDock.Core.Services;

/// <summary>提供"先写临时文件再原子替换"的安全写入方式，避免写入中断损坏正式文件。</summary>
public static class AtomicFile
{
    /// <summary>
    /// 原子写入文本文件：先写临时文件，再替换目标文件。
    /// </summary>
    /// <param name="path">目标文件完整路径。</param>
    /// <param name="contents">要写入的文本内容。</param>
    /// <param name="keepBackup">替换时是否保留上一版为 <c>.bak</c> 文件。</param>
    public static void WriteAllText(string path, string contents, bool keepBackup = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contents);

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("路径缺少目录。");
        Directory.CreateDirectory(directory);

        var temporaryPath = path + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, contents);
            if (File.Exists(path))
            {
                if (keepBackup)
                {
                    File.Replace(temporaryPath, path, path + ".bak");
                }
                else
                {
                    File.Move(temporaryPath, path, overwrite: true);
                }
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// 原子写入文本文件并保留多份滚动备份：最新的旧文件成为 <c>.bak</c>，
    /// 更早的依次后移为 <c>.bak.1</c>、<c>.bak.2</c>…（共 <paramref name="backupCount"/> 份）。
    /// </summary>
    /// <param name="path">目标文件完整路径。</param>
    /// <param name="contents">要写入的文本内容。</param>
    /// <param name="backupCount">保留的历史备份份数，至少为 1。</param>
    public static void WriteAllTextWithRollingBackup(string path, string contents, int backupCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contents);
        if (backupCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(backupCount));
        }

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("路径缺少目录。");
        Directory.CreateDirectory(directory);

        var temporaryPath = path + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, contents);

            if (File.Exists(path))
            {
                // 依次后移备份，为新的 .bak 腾出位置。
                for (var index = backupCount - 1; index >= 0; index--)
                {
                    var backupPath = BackupPath(path, index);
                    var sourcePath = index == 0 ? path : BackupPath(path, index - 1);
                    if (File.Exists(sourcePath))
                    {
                        if (File.Exists(backupPath))
                        {
                            File.Delete(backupPath);
                        }

                        File.Move(sourcePath, backupPath);
                    }
                }
            }

            File.Move(temporaryPath, path);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>第 <paramref name="index"/> 份滚动备份的路径；index=0 时是最新的 <c>.bak</c>。</summary>
    private static string BackupPath(string path, int index)
        => index == 0 ? path + ".bak" : $"{path}.bak.{index}";
}
