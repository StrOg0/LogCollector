using LogCollectorApp.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LogCollectorApp.Services;

public class MockSshFileHandler : ISshFileHandler
{
    private bool _disposed;
    private readonly string _testLogsPath;

    public MockSshFileHandler(string testLogsPath)
    {
        _testLogsPath = testLogsPath;
    }

    public async Task DownloadFileAsync(
        string host,
        int port,
        string remoteFilePath,
        string localTempDirectory,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report($"[MOCK] Подключение к {host}:{port}...");
        await Task.Delay(500, cancellationToken);

        string fileName = Path.GetFileName(remoteFilePath);
        progress?.Report($"[MOCK] Поиск файла: {fileName}");

        string? sourceFile = FindFileInTestLogs(fileName);

        if (sourceFile == null)
        {
            throw new FileNotFoundException($"Файл не найден в TestLogs: {fileName}");
        }

        string destFile = Path.Combine(localTempDirectory, fileName);
        File.Copy(sourceFile, destFile, overwrite: true);

        progress?.Report($"[MOCK] Файл скопирован: {destFile}");
    }

    public async Task<List<string>> GetFilesListAsync(
        string host,
        int port,
        string remoteDirectoryPath,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        await Task.Delay(300, cancellationToken);

        string groupName = DetermineGroupFromPath(remoteDirectoryPath);
        string groupPath = Path.Combine(_testLogsPath, groupName);

        if (!Directory.Exists(groupPath))
        {
            return new List<string>();
        }

        var files = Directory.GetFiles(groupPath)
            .Where(f => !Path.GetFileName(f).Contains("archive", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return files;
    }

    private string? FindFileInTestLogs(string fileName)
    {
        var allFiles = Directory.GetFiles(_testLogsPath, "*", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f).Contains(fileName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return allFiles.Length > 0 ? allFiles[0] : null;
    }

    private string DetermineGroupFromPath(string remotePath)
    {
        if (remotePath.Contains("ddmwebapi_log", StringComparison.OrdinalIgnoreCase))
            return "web";

        if (remotePath.Contains("digdes/sdu", StringComparison.OrdinalIgnoreCase))
            return "app";

        return "app";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MockSshFileHandler));
    }
}