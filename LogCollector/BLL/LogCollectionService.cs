using LogCollector.Interfaces;
using LogCollector.Models;

namespace LogCollector.BLL;

public class LogCollectionService
{
    private readonly ISshFileHandler _sshHandler;
    private readonly IArchiveManager _archiveManager;
    private readonly ILogSearchModule _logSearchModule;

    public LogCollectionService(ISshFileHandler sshHandler, IArchiveManager archiveManager, ILogSearchModule logSearchModule)
    {
        _sshHandler = sshHandler;
        _archiveManager = archiveManager;
        _logSearchModule = logSearchModule;
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
            var files = await _sshHandler.GetFilesListAsync(
                server.IpAddress,
                server.Port,
                server.Login,
                server.Password,
                "/upload", // Путь к логам (пока хардкод)
                cancellationToken);

            progress?.Report($"Найдено файлов: {files.Count}");

            foreach (var remoteFile in files)
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

                progress?.Report($"Найдено {allLogFilesToProcess.Count} лог-файлов. Начинаем потоковую фильтрацию...");
                string filteredTempFile = Path.Combine(serverTempDir, $"filtered_{server.IpAddress}.log");

                // Определяем формат логов (в будущем будет браться из БД на основе группы серверов)
                LogFormatType logFormat = DetermineLogFormat(server);
                progress?.Report($"Определен формат логов: {logFormat}");

                long totalLinesFound = 0;
                bool isFirstFile = true;

                foreach (var logFile in allLogFilesToProcess)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report($"Фильтрация: {Path.GetFileName(logFile)}...");

                    // Вызываем модуль поиска. 
                    // isFirstFile: первый файл перезаписывает (append=false), остальные дописывают (append=true)
                    long linesInFile = await _logSearchModule.SearchLogsAsync(
                        inputFilePath: logFile,
                        outputFilePath: filteredTempFile,
                        startTime: startDate,
                        endTime: endDate,
                        searchMask: null, // Маску пока не передаем (будет из БД)
                        logFormat: logFormat,
                        append: !isFirstFile
                    );

                    totalLinesFound += linesInFile;
                    isFirstFile = false;

                    progress?.Report($"  -> Найдено строк: {linesInFile}");
                }

                if (totalLinesFound == 0)
                {
                    result.Status = CollectionStatus.NoData;
                    result.Message = "Записи за указанный период не найдены";
                    return result;
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

    // [ИНТЕГРАЦИЯ] Вспомогательный метод определения формата логов
    // В будущем тип формата будет храниться в БД в таблице групп серверов
    private LogFormatType DetermineLogFormat(Server server)
    {
        // Пока определяем по имени сервера (хардкод)
        if (server.HostName.Contains("web", StringComparison.OrdinalIgnoreCase) ||
            server.HostName.Contains("ddm", StringComparison.OrdinalIgnoreCase))
        {
            return LogFormatType.Web;
        }
        return LogFormatType.App;
    }

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