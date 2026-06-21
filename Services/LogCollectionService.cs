using LogCollectorApp.Interfaces;
using LogCollectorApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LogCollectorApp.Services;

public class LogCollectionService
{
    private readonly ISshFileHandler _sshHandler;
    private readonly IArchiveManager _archiveManager;

    public LogCollectionService(ISshFileHandler sshHandler, IArchiveManager archiveManager)
    {
        _sshHandler = sshHandler;
        _archiveManager = archiveManager;
    }

    public async Task<CollectionResult> CollectLogsAsync(
        Server server,
        DateTime startDate,
        DateTime endDate,
        string tempDirectory,
        string outputDirectory,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var result = new CollectionResult
        {
            ServerId = server.Id,
            ServerName = server.Name,
            StartTime = DateTime.Now
        };

        var archiveProgress = new Progress<ArchiveProgressInfo>(info =>
        {
            progress?.Report($"[Архив] {info.Message}");
        });

        try
        {
            progress?.Report($"Начинаем сбор с сервера {server.Name} ({server.IpAddress})...");

            string serverTempDir = Path.Combine(tempDirectory, $"server_{server.Id}_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(serverTempDir);
            Directory.CreateDirectory(outputDirectory);

            progress?.Report($"Получение списка файлов с {server.Name}...");
            
            string logPath = GetLogPathForGroup(server.GroupId);
            string groupName = GetGroupName(server.GroupId);
            
            var allFiles = await _sshHandler.GetFilesListAsync(
                server.IpAddress,
                server.SshPort,
                logPath,
                cancellationToken);

            progress?.Report($"Найдено файлов: {allFiles.Count}");

            var dateRange = GetDateRange(startDate, endDate);
            progress?.Report($"Диапазон дат: {dateRange.Count} дн(я/ей)");

            var allFoundEntries = new List<string>();
            var allLogFilesToProcess = new List<string>();

            foreach (var targetDate in dateRange)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report($"\nОбработка даты: {targetDate:dd.MM.yyyy}");

                var targetFiles = LogSearcher.FindLogFilesByDate(allFiles, targetDate, groupName);
                
                if (targetFiles.Count == 0)
                {
                    progress?.Report($"✗ Файлы за {targetDate:dd.MM.yyyy} не найдены (пропускаем)");
                    continue;
                }

                progress?.Report($"✓ Найдено файлов за {targetDate:dd.MM.yyyy}: {targetFiles.Count}");

                foreach (var targetFile in targetFiles)
                {
                    progress?.Report($"\n  Обработка файла: {Path.GetFileName(targetFile)}");

                    await _sshHandler.DownloadFileAsync(
                        server.IpAddress,
                        server.SshPort,
                        targetFile,
                        serverTempDir,
                        progress,
                        cancellationToken);

                    string downloadedFile = Path.Combine(serverTempDir, Path.GetFileName(targetFile));

                    string ext = Path.GetExtension(downloadedFile).ToLowerInvariant();
                    string fileName = Path.GetFileName(downloadedFile).ToLowerInvariant();

                    if (ext == ".zip" || fileName.EndsWith(".tar.gz") || fileName.EndsWith(".tgz"))
                    {
                        progress?.Report($"  Распаковка архива...");
                        var extractedLogs = _archiveManager.ExtractArchives(
                            downloadedFile, 
                            serverTempDir, 
                            archiveProgress);
                        
                        allLogFilesToProcess.AddRange(extractedLogs);
                        
                        if (File.Exists(downloadedFile))
                        {
                            File.Delete(downloadedFile);
                        }
                    }
                    else if (ext == ".log")
                    {
                        allLogFilesToProcess.Add(downloadedFile);
                    }
                }
            }

            if (allLogFilesToProcess.Count > 0)
            {
                progress?.Report($"\nВсего найдено лог-файлов для обработки: {allLogFilesToProcess.Count}");

                foreach (var logFile in allLogFilesToProcess)
                {
                    progress?.Report($"\n  Поиск в файле: {logFile}");  // ← Полный путь!

                    DateTime fileStartTime = startDate;
                    DateTime fileEndTime = endDate;

                    progress?.Report($"  Поиск записей с {fileStartTime:HH:mm} по {fileEndTime:HH:mm}...");

                    var foundLines = LogSearcher.SearchLogsByTimeRange(
                        logFile, 
                        fileStartTime, 
                        fileEndTime, 
                        groupName);

                    if (foundLines.Count > 0)
                    {
                        progress?.Report($"  Найдено строк: {foundLines.Count}");
                        
                        string[] allLines = File.ReadAllLines(logFile);
                        var fullEntries = LogSearcher.ExtractFullLogEntries(foundLines, allLines, groupName);
                        allFoundEntries.AddRange(fullEntries);
                        progress?.Report($"  Извлечено записей: {fullEntries.Count}");
                    }
                    else
                    {
                        progress?.Report($"  Записи не найдены в этом файле");
                        
                        // 🔥 ДОБАВЬ ЭТО ДЛЯ ОТЛАДКИ:
                        try
                        {
                            var firstLines = File.ReadAllLines(logFile).Take(20).ToArray();
                            progress?.Report($"  Первые строки файла:");
                            foreach (var fl in firstLines)
                            {
                                progress?.Report($"    {fl}");
                            }
                        }
                        catch { }
                    }
                }
            }

            if (allFoundEntries.Count > 0)
            {
                string resultFileName = $"{server.Name}_{startDate:yyyyMMdd_HHmmss}_{endDate:yyyyMMdd_HHmmss}.log";
                string resultFilePath = Path.Combine(outputDirectory, resultFileName);

                await File.WriteAllLinesAsync(resultFilePath, allFoundEntries, cancellationToken);
                
                result.ResultFilePath = resultFilePath;
                result.Status = CollectionStatus.Success;
                result.Message = $"Найдено {allFoundEntries.Count} записей";
                progress?.Report($"\n✓ Результат сохранен: {resultFilePath}");
            }
            else
            {
                result.Status = CollectionStatus.NoData;
                result.Message = "Записи не найдены в указанном диапазоне";
            }

            if (Directory.Exists(serverTempDir))
            {
                Directory.Delete(serverTempDir, true);
                progress?.Report("Временные файлы удалены");
            }

            progress?.Report($"\nСбор с {server.Name} завершен");
        }
        catch (OperationCanceledException)
        {
            result.Status = CollectionStatus.Cancelled;
            result.Message = "Операция отменена";
        }
        catch (Exception ex)
        {
            result.Status = CollectionStatus.Error;
            result.Message = $"Ошибка: {ex.Message}";
            progress?.Report($"ОШИБКА: {ex.Message}");
            progress?.Report($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            result.EndTime = DateTime.Now;
        }

        return result;
    }

    private List<DateTime> GetDateRange(DateTime start, DateTime end)
    {
        var dates = new List<DateTime>();
        DateTime current = start.Date;
        
        while (current <= end.Date)
        {
            dates.Add(current);
            current = current.AddDays(1);
        }
        
        return dates;
    }

    private string GetGroupName(long groupId) => groupId switch
    {
        1 => "web",
        2 => "app",
        _ => "app"
    };

    private string GetLogPathForGroup(long groupId) => groupId switch
    {
        1 => "/digdes/TK/dock/ddmwebapi_log",
        2 => "/var/log/digdes/sdu",
        _ => "/var/log/digdes/sdu"
    };
}

// 🔥 ВЫНЕСЕНЫ НА УРОВЕНЬ NAMESPACE
public class CollectionResult
{
    public long ServerId { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public CollectionStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ResultFilePath { get; set; } = string.Empty;
}

public enum CollectionStatus
{
    Success,
    NoData,
    Error,
    Cancelled
}