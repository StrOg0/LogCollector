using LogCollector.Interfaces;
using LogCollector.Models;

namespace LogCollector.App.BLL;

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
            ServerName = server.HostName,
            StartTime = DateTime.Now
        };

        try
        {
            progress?.Report($"Начинаем сбор с сервера {server.HostName} ({server.IpAddress})...");

            string serverTempDir = Path.Combine(tempDirectory, $"server_{server.Id}_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(serverTempDir);

            progress?.Report($"Получение списка файлов с {server.HostName}...");
            var files = await _sshHandler.GetFilesListAsync(
                server.IpAddress,
                server.Port,
                server.Login,
                server.Password,
                "/test/logs", // Путь к логам (пока хардкод)
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

            string resultFileName = $"{server.HostName}_{DateTime.Now:yyyyMMdd_HHmmss}.log";
            string resultFilePath = Path.Combine(outputDirectory, resultFileName);

            var downloadedFiles = Directory.GetFiles(serverTempDir);
            if (downloadedFiles.Length > 0)
            {
                File.Copy(downloadedFiles[0], resultFilePath, overwrite: true);
                result.ResultFilePath = resultFilePath;
                result.Status = CollectionStatus.Success;
                result.Message = $"Успешно собрано {downloadedFiles.Length} файл(ов)";
            }
            else
            {
                result.Status = CollectionStatus.NoData;
                result.Message = "Данные не найдены";
            }

            if (Directory.Exists(serverTempDir))
            {
                Directory.Delete(serverTempDir, recursive: true);
                progress?.Report("Временные файлы удалены");
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