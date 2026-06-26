using LogCollector.Interfaces;
using LogCollector.Models;

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

            //string resultFileName = $"{server.HostName}_{DateTime.Now:yyyyMMdd_HHmmss}.log";
            //string resultFilePath = Path.Combine(outputDirectory, resultFileName);

            var downloadedFiles = Directory.GetFiles(serverTempDir);
            if (downloadedFiles.Length > 0)
            {
                //using var resultStream = File.Create(resultFilePath);
                //foreach (var file in downloadedFiles)
                //{
                //    using var fileStream = File.OpenRead(file);
                //    await fileStream.CopyToAsync(resultStream, cancellationToken);
                //}
                //result.ResultFilePath = resultFilePath;
                //result.Status = CollectionStatus.Success;
                //result.Message = $"Успешно собрано {downloadedFiles.Length} файл(ов)";

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

                // ФИЛЬТРАЦИЯ / СКЛЕЙКА (Заглушка)
                // Пока нет потокового чтения, мы просто физически склеиваем все найденные .log в один временный файл
                progress?.Report($"Найдено {allLogFilesToProcess.Count} лог-файлов. Подготовка к упаковке...");
                string filteredTempFile = Path.Combine(serverTempDir, $"filtered_{server.IpAddress}.log");

                using (var outStream = File.Create(filteredTempFile))
                {
                    foreach (var logFile in allLogFilesToProcess)
                    {
                        using var inStream = File.OpenRead(logFile);
                        await inStream.CopyToAsync(outStream, cancellationToken);
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