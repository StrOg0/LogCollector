using LogCollectorApp.Interfaces;
using LogCollectorApp.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LogCollectorApp.Services;

public class LogCollectionService
{
    private const string WebGroupKey = "web";
    private const string AppGroupKey = "app";

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
            LogSource logSource = GetLogSource(server);
            Encoding sourceEncoding = ResolveEncoding(logSource.Encoding);

            progress?.Report($"Источник логов: {logSource.LogPath}");

            var files = group == WebGroupKey
                ? await CollectWeb(server, start, end, workDir, logSource, progress, ct)
                : await CollectApp(server, start, end, workDir, logSource, progress, ct);

            string safeServerName = MakeSafeFileName(server.Name);
            string resultPath = Path.Combine(outputDir, $"{safeServerName}_{start:yyyyMMdd_HHmmss}_{end:yyyyMMdd_HHmmss}.log");
            int entriesCount = 0;

            await using (var writer = new StreamWriter(resultPath, append: false, Encoding.UTF8, bufferSize: 1024 * 1024))
            {
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    entriesCount += await LogSearcher.WriteMatchingEntriesAsync(file, writer, start, end, group, ct, sourceEncoding);
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

    private async Task<List<string>> CollectApp(Server server, DateTime start, DateTime end, string workDir, LogSource logSource, IProgress<string> progress, CancellationToken ct)
    {
        string path = RequirePath(logSource.LogPath, "путь к текущим логам");
        var remoteFiles = await _ssh.GetFilesListAsync(server.IpAddress, server.SshPort, server.Login, server.Password, path, ct);
        var files = new List<string>();

        foreach (var date in GetDateRange(start, end))
        {
            string pattern = BuildDateFilePattern(logSource.FileMask, date, AppGroupKey);
            foreach (var file in FindFilesByPattern(remoteFiles, pattern))
                files.Add(await Download(file, server, workDir, progress, ct));
        }

        return files;
    }

    private async Task<List<string>> CollectWeb(Server server, DateTime start, DateTime end, string workDir, LogSource logSource, IProgress<string> progress, CancellationToken ct)
    {
        string mainPath = RequirePath(logSource.LogPath, "путь к текущим web-логам");
        string? archivePath = string.IsNullOrWhiteSpace(logSource.ArchivePath) ? null : logSource.ArchivePath.Trim();
        string currentLogMask = string.IsNullOrWhiteSpace(logSource.FileMask) ? "DDM_Web.log" : logSource.FileMask.Trim();
        var files = new List<string>();
        var sourceErrors = new List<string>();

        try
        {
            var mainFiles = await _ssh.GetFilesListAsync(server.IpAddress, server.SshPort, server.Login, server.Password, mainPath, ct);
            foreach (var currentLog in FindFilesByPattern(mainFiles, currentLogMask))
                files.Add(await Download(currentLog, server, workDir, progress, ct));
        }
        catch (Exception ex)
        {
            string message = $"Основной лог недоступен: {ex.Message}";
            sourceErrors.Add(message);
            progress?.Report(message);
        }

        if (!string.IsNullOrWhiteSpace(archivePath))
        {
            try
            {
                var archives = await _ssh.GetFilesListAsync(server.IpAddress, server.SshPort, server.Login, server.Password, archivePath, ct);
                foreach (var date in GetDateRange(start, end))
                {
                    string pattern = BuildArchiveDatePattern(currentLogMask, date);
                    foreach (var archive in FindFilesByPattern(archives, pattern))
                    {
                        string downloaded = await Download(archive, server, workDir, progress, ct);
                        files.AddRange(_archive.ExtractArchives(downloaded, workDir));
                        TryDelete(downloaded);
                    }
                }
            }
            catch (Exception ex)
            {
                string message = $"Архивы недоступны: {ex.Message}";
                sourceErrors.Add(message);
                progress?.Report(message);
            }
        }

        if (files.Count == 0 && sourceErrors.Count > 0)
            throw new InvalidOperationException(string.Join("; ", sourceErrors));

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

    private static LogSource GetLogSource(Server server)
    {
        var source = server.Group?.LogSource;

        if (source == null)
            throw new InvalidOperationException($"Для группы сервера '{server.Group?.Name ?? server.GroupId.ToString(CultureInfo.InvariantCulture)}' не настроен источник логов в БД.");

        if (string.IsNullOrWhiteSpace(source.LogPath))
            throw new InvalidOperationException($"Для группы '{server.Group?.Name ?? server.GroupId.ToString(CultureInfo.InvariantCulture)}' не заполнен путь к логам в БД.");

        return source;
    }

    private static string GetGroupName(Server server)
    {
        string name = server.Group?.Name?.ToLowerInvariant() ?? string.Empty;

        if (name.Contains("web") || name.Contains("веб")) return WebGroupKey;
        if (name.Contains("app") || name.Contains("прилож")) return AppGroupKey;

        return AppGroupKey;
    }

    private static IEnumerable<string> FindFilesByPattern(IEnumerable<string> files, string pattern)
    {
        pattern = pattern.Trim();
        if (string.IsNullOrWhiteSpace(pattern)) return Enumerable.Empty<string>();

        return files.Where(file => IsFileMatch(Path.GetFileName(file), pattern));
    }

    private static bool IsFileMatch(string fileName, string pattern)
    {
        if (pattern is "*" or "*.*") return true;

        if (pattern.Contains('*') || pattern.Contains('?'))
        {
            string regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            return System.Text.RegularExpressions.Regex.IsMatch(fileName, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDateFilePattern(string? fileMask, DateTime date, string groupName)
    {
        if (string.IsNullOrWhiteSpace(fileMask))
            return groupName == WebGroupKey ? "DDM_Web.log" : $"log {date:yyyy}Y{date:MM}M{date:dd}D";

        return ApplyDatePlaceholders(fileMask.Trim(), date);
    }

    private static string BuildArchiveDatePattern(string currentLogMask, DateTime date)
    {
        string patternWithDate = ApplyDatePlaceholders(currentLogMask, date);
        if (!string.Equals(patternWithDate, currentLogMask, StringComparison.Ordinal)) return patternWithDate;

        string baseName = Path.GetFileNameWithoutExtension(currentLogMask);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = currentLogMask;

        return $"{baseName}_plain_{date:yyyyMMdd}";
    }

    private static string ApplyDatePlaceholders(string value, DateTime date) => value
        .Replace("{yyyyMMdd}", date.ToString("yyyyMMdd", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
        .Replace("{yyyy-MM-dd}", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
        .Replace("{yyyy}", date.ToString("yyyy", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
        .Replace("{MM}", date.ToString("MM", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
        .Replace("{dd}", date.ToString("dd", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

    private static string RequirePath(string? path, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException($"В БД не заполнен {fieldName}.");

        return path.Trim();
    }

    private static Encoding ResolveEncoding(string? encodingName)
    {
        if (string.IsNullOrWhiteSpace(encodingName)) return Encoding.UTF8;

        try { return Encoding.GetEncoding(encodingName.Trim()); }
        catch { return Encoding.UTF8; }
    }

    private static string MakeSafeFileName(string value)
    {
        string result = string.IsNullOrWhiteSpace(value) ? "server" : value.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
            result = result.Replace(invalidChar, '_');

        return result;
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
