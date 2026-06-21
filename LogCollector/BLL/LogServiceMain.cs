using LogCollector.Interfaces;
using LogCollector.Models;
using LogCollectorApp.Services;
using static System.Net.WebRequestMethods;

namespace LogCollector.BLL;

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
            ServerName = server.HostName,
            StartTime = DateTime.Now
        };

        // Адаптер прогресса: переводим ArchiveProgressInfo в строку для UI
        var archiveProgress = new Progress<ArchiveProgressInfo>(info =>
        {
            progress?.Report($"[Архив] {info.Message}");
        });

        string serverTempDir = Path.Combine(tempDirectory, $"server_{server.Id}_{DateTime.Now:yyyyMMdd_HHmmss}");


        try
        {
            progress?.Report($"Начинаем сбор с сервера {server.HostName} ({server.IpAddress})...");

            Directory.CreateDirectory(serverTempDir);
            Directory.CreateDirectory(outputDirectory);

            progress?.Report($"Получение списка файлов с {server.HostName}...");
            var allFiles = await _sshHandler.GetFilesListAsync(
                server.IpAddress,
                server.Port,
                server.Login,
                server.Password,
                "/upload", // Путь к логам (пока хардкод)
                cancellationToken);

            progress?.Report($"Найдено файлов: {allFiles.Count}");

            string logPath = GetLogPathForGroup(server.GroupId);
            string groupName = GetGroupName(server.GroupId);

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

                allFoundEntries.AddRange(targetFiles);
                
                if (targetFiles.Count == 0)
                {
                    progress?.Report($"✗ Файлы за {targetDate:dd.MM.yyyy} не найдены (пропускаем)");
                    continue;
                }

                progress?.Report($"✓ Найдено файлов за {targetDate:dd.MM.yyyy}: {targetFiles.Count}");
            }

            foreach (var remoteFile in allFoundEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report($"Скачивание: {Path.GetFileName(remoteFile)}...");

                await _sshHandler.DownloadFileAsync(
                    server.IpAddress,
                    server.Port,
                    server.Login,
                    server.Password,
                    remoteFile,
                    serverTempDir,
                    progress,
                    cancellationToken);
            }
            var downloadedFiles = Directory.GetFiles(serverTempDir);
            if (downloadedFiles.Length > 0)
            {
                //Unpack archives
                progress?.Report("Анализ скачанных файлов и распаковка архивов...");
                var allLogFilesToProcess = new List<string>();

                foreach (var file in downloadedFiles)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    string fileName = Path.GetFileName(file).ToLowerInvariant();

                    if (ext == ".zip" || fileName.EndsWith(".tar.gz") || fileName.EndsWith(".tgz"))
                    {
                        var extractedLogs = _archiveManager.ExtractArchives(file, serverTempDir, archiveProgress);
                        allLogFilesToProcess.AddRange(extractedLogs);
                    }
                    else if (ext == ".log")
                    {
                        allLogFilesToProcess.Add(file);
                    }
                }

                if (allLogFilesToProcess.Count == 0)
                {
                    result.Status = CollectionStatus.NoData;
                    result.Message = "Логи не найдены (в том числе внутри архивов)";
                    return result;
                }

                // ФИЛЬТРАЦИЯ
                foreach(var file in allLogFilesToProcess)
                {
                    // 🔥 Определяем временной диапазон для этого файла
                    DateTime fileStartTime = (targetDate == startDate.Date) ? startDate : targetDate;
                    DateTime fileEndTime = (targetDate == endDate.Date) ? endDate : targetDate.Date.AddHours(23).AddMinutes(59).AddSeconds(59);

                    progress?.Report($"  Поиск записей с {fileStartTime:HH:mm} по {fileEndTime:HH:mm}...");

                    // Ищем записи по времени
                    var foundLines = LogSearcher.SearchLogsByTimeRange(
                        file,
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
                }

                //Create result archive
                string resultZipPath = Path.Combine(outputDirectory, $"{server.IpAddress}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

                var processedLogs = new List<ProcessedLogInfo>
            {
                new ProcessedLogInfo
                {
                    ServerIp = server.IpAddress,
                    ServerName = server.HostName,
                    TempFilePath = filteredTempFile,
                    LogDate = startDate
                }
            };

                progress?.Report("Формирование итогового ZIP-архива...");
                _archiveManager.CreateResultArchive(resultZipPath, processedLogs, archiveProgress);

                result.ResultFilePath = resultZipPath;
                result.Status = CollectionStatus.Success;
                result.Message = $"Успешно собрано и упаковано {allLogFilesToProcess.Count} файл(ов)";

                progress?.Report($"Сбор с {server.HostName} завершен. Архив: {Path.GetFileName(resultZipPath)}");
            }
            else
            {
                result.Status = CollectionStatus.NoData;
                result.Message = "Данные не найдены";
            }

            progress?.Report($"Сбор с {server.HostName} завершен");
        }
        catch (OperationCanceledException)
        {
            result.Status = CollectionStatus.Cancelled;
            result.Message = "Операция отменена пользователем";
            progress?.Report("Операция отменена");
        }
        catch (Exception ex)
        {
            result.Status = CollectionStatus.Error;
            result.Message = $"Ошибка: {ex.Message}";
            progress?.Report($"ОШИБКА: {ex.Message}");
        }
        finally
        {
            // ОЧИСТКА
            if (Directory.Exists(serverTempDir))
            {
                try
                {
                    Directory.Delete(serverTempDir, recursive: true);
                    progress?.Report("Временные файлы удалены");
                }
                catch { /* Игнорируем ошибки очистки */ }
            }
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

    public class CollectionResult
    {
        public int ServerId { get; set; }
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
        Partial,
        NoData,
        Error,
        Cancelled
    }
}