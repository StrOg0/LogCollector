using LogCollectorApp.Interfaces;
using LogCollectorApp.Models;
using System.IO;

namespace LogCollectorApp.Services;

public class LogCollectionService
{
    private readonly ISshFileHandler _ssh;
    private readonly IArchiveManager _archive;

    public LogCollectionService(ISshFileHandler ssh, IArchiveManager archive)
    {
        _ssh = ssh;
        _archive = archive;
    }

    public async Task<CollectionResult> CollectLogsAsync(Server server, DateTime start, DateTime end, string tempDir, string outputDir, IProgress<string> progress, CancellationToken ct)
    {
        var result = new CollectionResult { ServerId = server.Id, ServerName = server.Name, StartTime = DateTime.Now };

        try
        {
            string workDir = Path.Combine(tempDir, $"server_{server.Id}_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(workDir);
            Directory.CreateDirectory(outputDir);

            string group = GetGroupName(server);
            var files = group == "web"
                ? await CollectWeb(server, start, end, workDir, progress, ct)
                : await CollectApp(server, start, end, workDir, progress, ct);

            string resultPath = Path.Combine(outputDir, $"{server.Name}_{start:yyyyMMdd_HHmmss}_{end:yyyyMMdd_HHmmss}.log");
            int entriesCount = 0;

            await using (var writer = new StreamWriter(resultPath, append: false))
            {
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    entriesCount += await LogSearcher.WriteMatchingEntriesAsync(file, writer, start, end, group, ct);
                }
            }

            if (entriesCount == 0)
            {
                TryDelete(resultPath);
                result.Status = CollectionStatus.NoData;
                result.Message = "Записи не найдены";
                return result;
            }

            result.Status = CollectionStatus.Success;
            result.ResultFilePath = resultPath;
            result.Message = $"Найдено {entriesCount} записей";
            progress?.Report($"✓ {server.Name}: {result.Message}");
        }
        catch (OperationCanceledException)
        {
            result.Status = CollectionStatus.Cancelled;
            result.Message = "Отменено";
        }
        catch (Exception ex)
        {
            result.Status = CollectionStatus.Error;
            result.Message = ex.Message;
        }
        finally
        {
            result.EndTime = DateTime.Now;
            TryDelete(tempDir);
        }

        return result;
    }

    private async Task<List<string>> CollectApp(Server server, DateTime start, DateTime end, string workDir, IProgress<string> progress, CancellationToken ct)
    {
        const string path = "/var/log/digdes/sdu";
        var remoteFiles = await _ssh.GetFilesListAsync(server.IpAddress, server.SshPort, server.Login, server.Password, path, ct);
        var files = new List<string>();

        foreach (var date in GetDateRange(start, end))
            foreach (var file in LogSearcher.FindLogFilesByDate(remoteFiles, date, "app"))
                files.Add(await Download(file, server, workDir, progress, ct));

        return files;
    }

    private async Task<List<string>> CollectWeb(Server server, DateTime start, DateTime end, string workDir, IProgress<string> progress, CancellationToken ct)
    {
        const string mainPath = "/upload/ddmwebapi_log";
        const string archivePath = "/upload/ddmwebapi_log/archive";
        var files = new List<string>();

        try
        {
            var mainFiles = await _ssh.GetFilesListAsync(server.IpAddress, server.SshPort, server.Login, server.Password, mainPath, ct);
            var currentLog = mainFiles.FirstOrDefault(f => Path.GetFileName(f).Equals("DDM_Web.log", StringComparison.OrdinalIgnoreCase));
            if (currentLog != null) files.Add(await Download(currentLog, server, workDir, progress, ct));
        }
        catch (Exception ex) { progress?.Report($"Основной лог недоступен: {ex.Message}"); }

        try
        {
            var archives = await _ssh.GetFilesListAsync(server.IpAddress, server.SshPort, server.Login, server.Password, archivePath, ct);
            foreach (var date in GetDateRange(start, end))
            {
                string pattern = $"DDM_Web_plain_{date:yyyyMMdd}";
                foreach (var archive in archives.Where(f => Path.GetFileName(f).Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                {
                    string downloaded = await Download(archive, server, workDir, progress, ct);
                    files.AddRange(_archive.ExtractArchives(downloaded, workDir));
                    TryDelete(downloaded);
                }
            }
        }
        catch (Exception ex) { progress?.Report($"Архивы недоступны: {ex.Message}"); }

        return files;
    }

    private async Task<string> Download(string remoteFile, Server server, string workDir, IProgress<string> progress, CancellationToken ct)
    {
        progress?.Report($"Скачивание: {Path.GetFileName(remoteFile)}");
        await _ssh.DownloadFileAsync(server.IpAddress, server.SshPort, server.Login, server.Password, remoteFile, workDir, progress, ct);
        return Path.Combine(workDir, Path.GetFileName(remoteFile));
    }

    private static IEnumerable<DateTime> GetDateRange(DateTime start, DateTime end)
    {
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1)) yield return date;
    }

    private static string GetGroupName(Server server)
    {
        string name = server.Group?.Name?.ToLowerInvariant() ?? string.Empty;

        if (name.Contains("web") || name.Contains("веб")) return "web";
        if (name.Contains("app") || name.Contains("прилож")) return "app";

        return server.GroupId == 2 ? "web" : "app";
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            else if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch { }
    }
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

public enum CollectionStatus { Success, NoData, Error, Cancelled }
