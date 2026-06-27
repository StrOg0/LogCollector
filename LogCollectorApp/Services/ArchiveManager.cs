using LogCollectorApp.Interfaces;
using LogCollectorApp.Models;
using SharpCompress.Archives;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace LogCollectorApp.Services;

public class ArchiveManager : IArchiveManager
{
    public List<string> ExtractArchives(string sourceArchivePath, string targetDirectory, IProgress<ArchiveProgressInfo>? progress = null)
    {
        if (!File.Exists(sourceArchivePath)) throw new FileNotFoundException($"Архив не найден: {sourceArchivePath}");

        Directory.CreateDirectory(targetDirectory);
        var result = new List<string>();
        var queue = new Queue<string>();
        queue.Enqueue(sourceArchivePath);

        while (queue.Count > 0)
        {
            string archivePath = queue.Dequeue();
            string extractDir = Path.Combine(targetDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractDir);
            progress?.Report(new ArchiveProgressInfo { Stage = "Распаковка", Message = Path.GetFileName(archivePath) });

            try { ExtractArchive(archivePath, extractDir); }
            catch (Exception ex)
            {
                progress?.Report(new ArchiveProgressInfo { Stage = "Ошибка", Message = $"{Path.GetFileName(archivePath)}: {ex.Message}" });
                continue;
            }

            foreach (var file in Directory.GetFiles(extractDir, "*.*", SearchOption.AllDirectories))
            {
                if (IsLog(file)) result.Add(RenameWithArchivePrefix(file, archivePath));
                else if (IsArchive(file)) queue.Enqueue(file);
            }
        }

        progress?.Report(new ArchiveProgressInfo { Stage = "Распаковка", Message = $"Найдено логов: {result.Count}", Percent = 100 });
        return result;
    }

    public string CreateResultArchive(string destinationArchivePath, IEnumerable<ProcessedLogInfo> processedLogs, IProgress<ArchiveProgressInfo>? progress = null)
    {
        var logs = processedLogs.Where(x => File.Exists(x.TempFilePath)).ToList();
        if (logs.Count == 0) throw new ArgumentException("Нет данных для упаковки в итоговый архив.");
        if (File.Exists(destinationArchivePath)) File.Delete(destinationArchivePath);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var archive = ZipFile.Open(destinationArchivePath, ZipArchiveMode.Create);

        for (int i = 0; i < logs.Count; i++)
        {
            var log = logs[i];
            string entryName = names.Add($"{log.ServerIp}.log") ? $"{log.ServerIp}.log" : $"{log.ServerIp}_part{i + 1}.log";
            archive.CreateEntryFromFile(log.TempFilePath, entryName, CompressionLevel.Optimal);
            progress?.Report(new ArchiveProgressInfo { Stage = "Упаковка", Message = entryName, Percent = ((i + 1) * 100) / logs.Count });
        }

        return destinationArchivePath;
    }

    private static void ExtractArchive(string archivePath, string extractDir)
    {
        if (IsZip(archivePath))
        {
            ZipFile.ExtractToDirectory(archivePath, extractDir, overwriteFiles: true);
            return;
        }

        using var stream = File.OpenRead(archivePath);
        using var archive = ArchiveFactory.OpenArchive(stream);
        foreach (var entry in archive.Entries.Where(x => !x.IsDirectory))
            entry.WriteToDirectory(extractDir, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
    }

    private static string RenameWithArchivePrefix(string filePath, string archivePath)
    {
        string target = Path.Combine(Path.GetDirectoryName(filePath)!, $"{Path.GetFileNameWithoutExtension(archivePath)}_{Path.GetFileName(filePath)}");
        if (File.Exists(target)) return filePath;

        File.Move(filePath, target);
        return target;
    }

    private static bool IsArchive(string path) => IsZip(path) || path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tar", StringComparison.OrdinalIgnoreCase);
    private static bool IsZip(string path) => path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    private static bool IsLog(string path) => path.EndsWith(".log", StringComparison.OrdinalIgnoreCase);
}
