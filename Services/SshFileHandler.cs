using LogCollectorApp.Interfaces;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LogCollectorApp.Services;

public class SshFileHandler : ISshFileHandler
{
    private bool _disposed;

    public async Task DownloadFileAsync(
        string host,
        int port,
        string remoteFilePath,
        string localTempDirectory,
        IProgress<string> progress,
        CancellationToken ct)
    {
        ThrowIfDisposed();

        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Подключение к {host}...");

            var keyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ssh", "id_rsa");

            var keyFile = new PrivateKeyFile(keyPath);
            var connectionInfo = new ConnectionInfo(host, port, "root",
                new PrivateKeyAuthenticationMethod("root", keyFile))
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            using var client = new SftpClient(connectionInfo);
            client.Connect();

            progress?.Report($"Скачивание {remoteFilePath}...");

            string localFilePath = Path.Combine(localTempDirectory, Path.GetFileName(remoteFilePath));

            using var localStream = File.Create(localFilePath);
            client.DownloadFile(remoteFilePath, localStream);

            client.Disconnect();
            progress?.Report($"Файл сохранен: {localFilePath}");
        }, ct);
    }

    public async Task<List<string>> GetFilesListAsync(
        string host,
        int port,
        string remoteDirectoryPath,
        CancellationToken ct)
    {
        ThrowIfDisposed();

        return await Task.Run(() =>
        {
            var keyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ssh", "id_rsa");

            var keyFile = new PrivateKeyFile(keyPath);
            var connectionInfo = new ConnectionInfo(host, port, "root",
                new PrivateKeyAuthenticationMethod("root", keyFile))
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            using var client = new SftpClient(connectionInfo);
            client.Connect();

            var files = client.ListDirectory(remoteDirectoryPath)
                .Where(f => !f.IsDirectory)
                .Select(f => f.FullName)
                .ToList();

            client.Disconnect();
            return files;
        }, ct);
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
        if (_disposed) throw new ObjectDisposedException(nameof(SshFileHandler));
    }
}