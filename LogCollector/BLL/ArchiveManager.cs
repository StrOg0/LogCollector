using LogCollector.Interfaces;
using LogCollector.Models;
using SharpCompress.Archives;
using SharpCompress.Common;
using System.Diagnostics;
using System.IO.Compression;

namespace LogCollector.BLL
{
    public class ArchiveManager : IArchiveManager
    {
        public List<string> ExtractArchives(string sourceArchivePath, string targetDirectory, IProgress<ArchiveProgressInfo> progress = null)
        {
            if (!File.Exists(sourceArchivePath))
                throw new FileNotFoundException($"Архив не найден: {sourceArchivePath}");

            Directory.CreateDirectory(targetDirectory);
            var extractedLogFiles = new List<string>();
            var archivesToProcess = new Queue<string>();

            archivesToProcess.Enqueue(sourceArchivePath);
            int processedArchivesCount = 0;

            Debug.WriteLine($"[ArchiveManager] Начало распаковки: {sourceArchivePath}");

            progress?.Report(new ArchiveProgressInfo
            {
                Stage = "Распаковка",
                Message = $"Начало распаковки {Path.GetFileName(sourceArchivePath)}...",
                Percent = -1
            });

            while (archivesToProcess.Count > 0)
            {
                string currentArchive = archivesToProcess.Dequeue();
                processedArchivesCount++;

                Debug.WriteLine($"[ArchiveManager] [{processedArchivesCount}] Распаковка: {Path.GetFileName(currentArchive)}");
                
                progress?.Report(new ArchiveProgressInfo
                {
                    Stage = "Распаковка",
                    Message = $"[{processedArchivesCount}] Распаковка: {Path.GetFileName(currentArchive)}"
                });

                string currentExtractDir = Path.Combine(targetDirectory, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(currentExtractDir);

                try
                {
                    if (IsZipArchive(currentArchive))
                        ZipFile.ExtractToDirectory(currentArchive, currentExtractDir);
                    else if (IsTarGzArchive(currentArchive))
                        ExtractTarGz(currentArchive, currentExtractDir);
                    else
                        continue;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ArchiveManager] ОШИБКА распаковки {currentArchive}: {ex.Message}");

                    progress?.Report(new ArchiveProgressInfo
                    {
                        Stage = "Ошибка",
                        Message = $"Не удалось распаковать {Path.GetFileName(currentArchive)}: {ex.Message}"
                    });
                    continue;
                }

                foreach (var file in Directory.GetFiles(currentExtractDir, "*.*", SearchOption.AllDirectories))
                {
                    if (IsLogFile(file))
                    {
                        extractedLogFiles.Add(file);
                    }
                    else if (IsZipArchive(file) || IsTarGzArchive(file))
                    {
                        archivesToProcess.Enqueue(file);
                    }
                }

                progress?.Report(new ArchiveProgressInfo
                {
                    Stage = "Распаковка",
                    Message = $"Найдено лог-файлов: {extractedLogFiles.Count}. Архивов в очереди: {archivesToProcess.Count}"
                });
            }

            Debug.WriteLine($"[ArchiveManager] Распаковка завершена. Найдено логов: {extractedLogFiles.Count}");

            progress?.Report(new ArchiveProgressInfo
            {
                Stage = "Распаковка",
                Message = $"Распаковка завершена. Всего обработано архивов: {processedArchivesCount}, найдено логов: {extractedLogFiles.Count}.",
                Percent = 100
            });

            return extractedLogFiles;
        }

        public string CreateResultArchive(string destinationArchivePath, IEnumerable<ProcessedLogInfo> processedLogs, IProgress<ArchiveProgressInfo> progress = null)
        {
            var logsList = processedLogs.ToList();

            if (!logsList.Any())
                throw new ArgumentException("Нет данных для упаковки в итоговый архив.");

            if (File.Exists(destinationArchivePath))
                File.Delete(destinationArchivePath);

            int totalFiles = logsList.Count;
            int currentFileIndex = 0;

            Debug.WriteLine($"[ArchiveManager] Начало упаковки итогового архива. Файлов: {totalFiles}");

            progress?.Report(new ArchiveProgressInfo
            {
                Stage = "Упаковка",
                Message = $"Начало формирования итогового архива ({totalFiles} серверов)..."
            });

            var addedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (ZipArchive archive = ZipFile.Open(destinationArchivePath, ZipArchiveMode.Create))
            {
                foreach (var logInfo in logsList)
                {
                    currentFileIndex++;
                   
                    Debug.WriteLine($"[ArchiveManager] Упаковка: {logInfo.ServerIp} ({currentFileIndex}/{totalFiles})");

                    progress?.Report(new ArchiveProgressInfo
                    {
                        Stage = "Упаковка",
                        Message = $"Упаковка лога для {logInfo.ServerIp} ({logInfo.ServerName}) ({currentFileIndex}/{totalFiles})",
                        Percent = (currentFileIndex * 90) / totalFiles
                    });


                    if (File.Exists(logInfo.TempFilePath))
                    {
                        string entryName = $"{logInfo.ServerIp}.log";

                        if (!addedEntries.Add(entryName))
                        {
                            entryName = $"{logInfo.ServerIp}_part{currentFileIndex}.log";
                            addedEntries.Add(entryName);
                        }

                        archive.CreateEntryFromFile(logInfo.TempFilePath, entryName, CompressionLevel.Optimal);
                    }
                }

                Debug.WriteLine($"[ArchiveManager] Итоговый архив сформирован: {destinationArchivePath}");

                progress?.Report(new ArchiveProgressInfo
                {
                    Stage = "Упаковка",
                    Message = $"Итоговый архив успешно сформирован: {Path.GetFileName(destinationArchivePath)}",
                    Percent = 100
                });
            }

            return destinationArchivePath;
        }

        private bool IsZipArchive(string filePath) => filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        private bool IsTarGzArchive(string filePath)
        {
            string lowerPath = filePath.ToLowerInvariant();
            return lowerPath.EndsWith(".tar.gz") || lowerPath.EndsWith(".tgz") || lowerPath.EndsWith(".tar");
        }

        private bool IsLogFile(string filePath) => filePath.EndsWith(".log", StringComparison.OrdinalIgnoreCase);

        private void ExtractTarGz(string archivePath, string extractDir)
        {
            using (Stream stream = File.OpenRead(archivePath))
            using (var archive = ArchiveFactory.OpenArchive(stream))
            {
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    entry.WriteToDirectory(extractDir, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
        }
    }
}