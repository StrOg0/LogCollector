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

    public Task DownloadFileAsync(
        string host, int port, string username, string password,
        string remoteFilePath, string localTempDirectory,
        IProgress<string> progress, CancellationToken ct)
    {
        ThrowIfDisposed();

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(localTempDirectory);

            using var client = CreateClient(host, port, username, password);
            try
            {
                progress?.Report($"Подключение к {CleanHost(host)}...");
                client.Connect();

                string localPath = Path.Combine(localTempDirectory, Path.GetFileName(remoteFilePath));
                progress?.Report($"Скачивание: {Path.GetFileName(remoteFilePath)}");

                using var stream = File.Create(localPath);
                client.DownloadFile(remoteFilePath, stream);
            }
            finally
            {
                if (client.IsConnected) client.Disconnect();
            }
        }, ct);
    }

    public Task<List<string>> GetFilesListAsync(
        string host, int port, string username, string password,
        string remoteDirectoryPath, CancellationToken ct)
    {
        ThrowIfDisposed();

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            using var client = CreateClient(host, port, username, password);
            try
            {
                client.Connect();

                return client.ListDirectory(remoteDirectoryPath)
                    .Where(file => !file.IsDirectory)
                    .Select(file => file.FullName)
                    .ToList();
            }
            finally
            {
                if (client.IsConnected) client.Disconnect();
            }
        }, ct);
    }

    public void Dispose() => _disposed = true;

    private static SftpClient CreateClient(string host, int port, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("IP-адрес сервера не указан");
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Логин SSH не указан");
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Пароль SSH не указан");
        if (port is < 1 or > 65535) throw new ArgumentException("Порт SSH должен быть в диапазоне от 1 до 65535");

        var connection = new PasswordConnectionInfo(CleanHost(host), port, username, password)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        var client = new SftpClient(connection);
        client.HostKeyReceived += (_, e) => e.CanTrust = true;
        return client;
    }

    private static string CleanHost(string host)
    {
        host = host.Trim();

        if (Uri.TryCreate(host, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            return uri.Host;

        int slashIndex = host.IndexOf('/');
        return slashIndex >= 0 ? host[..slashIndex] : host;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SshFileHandler));
    }
}
