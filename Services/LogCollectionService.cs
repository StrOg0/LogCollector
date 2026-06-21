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

    public LogCollectionService(ISshFileHandler sshHandler)
    {
        _sshHandler = sshHandler;
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

        try
        {
            progress?.Report($"Начинаем сбор с сервера {server.Name} ({server.IpAddress})...");

            string serverTempDir = Path.Combine(tempDirectory, $"server_{server.Id}_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(serverTempDir);

            progress?.Report($"Получение списка файлов с {server.Name}...");
            
            string logPath = GetLogPathForGroup(server.GroupId);
            string groupName = GetGroupName(server.GroupId);
            
            var allFiles = await _sshHandler.GetFilesListAsync(
                server.IpAddress,
                server.SshPort,
                logPath,
                cancellationToken);

            progress?.Report($"Найдено файлов: {allFiles.Count}");

            // 🔥 Получаем все даты в диапазоне
            var dateRange = GetDateRange(startDate, endDate);
            progress?.Report($"Диапазон дат: {dateRange.Count} дн(я/ей)");

            var allFoundEntries = new List<string>();

            // 🔥 Для каждой даты ищем ВСЕ файлы
            foreach (var targetDate in dateRange)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report($"\nОбработка даты: {targetDate:dd.MM.yyyy}");

                // Ищем ВСЕ файлы за эту дату
                var targetFiles = LogSearcher.FindLogFilesByDate(allFiles, targetDate, groupName);
                
                if (targetFiles.Count == 0)
                {
                    progress?.Report($"✗ Файлы за {targetDate:dd.MM.yyyy} не найдены (пропускаем)");
                    continue;
                }

                progress?.Report($"✓ Найдено файлов за {targetDate:dd.MM.yyyy}: {targetFiles.Count}");

                // Для каждого файла
                foreach (var targetFile in targetFiles)
                {
                    progress?.Report($"\n  Обработка файла: {Path.GetFileName(targetFile)}");

                    // Скачиваем файл
                    await _sshHandler.DownloadFileAsync(
                        server.IpAddress,
                        server.SshPort,
                        targetFile,
                        serverTempDir,
                        progress,
                        cancellationToken);

                    string downloadedFile = Path.Combine(serverTempDir, Path.GetFileName(targetFile));

                    // 🔥 Определяем временной диапазон для этого файла
                    DateTime fileStartTime = (targetDate == startDate.Date) ? startDate : targetDate;
                    DateTime fileEndTime = (targetDate == endDate.Date) ? endDate : targetDate.Date.AddHours(23).AddMinutes(59).AddSeconds(59);

                    progress?.Report($"  Поиск записей с {fileStartTime:HH:mm} по {fileEndTime:HH:mm}...");

                    // Ищем записи по времени
                    var foundLines = LogSearcher.SearchLogsByTimeRange(
                        downloadedFile, 
                        fileStartTime, 
                        fileEndTime, 
                        groupName);

                    if (foundLines.Count > 0)
                    {
                        progress?.Report($"  Найдено строк: {foundLines.Count}");
                        
                        string[] allLines = File.ReadAllLines(downloadedFile);
                        var fullEntries = LogSearcher.ExtractFullLogEntries(foundLines, allLines);
                        allFoundEntries.AddRange(fullEntries);
                        progress?.Report($"  Извлечено записей: {fullEntries.Count}");
                    }
                    else
                    {
                        progress?.Report($"  Записи не найдены за {targetDate:dd.MM.yyyy} в этом файле");
                    }

                    // Удаляем скачанный файл перед следующим
                    if (File.Exists(downloadedFile))
                    {
                        File.Delete(downloadedFile);
                    }
                }
            }

            // 🔥 Сохраняем результат
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