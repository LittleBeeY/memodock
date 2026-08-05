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
}
