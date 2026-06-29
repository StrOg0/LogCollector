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

            string normalizedRemoteFilePath = NormalizeRemotePath(remoteFilePath);

            using var client = CreateClient(host, port, username, password);
            try
            {
                progress?.Report($"Подключение к {CleanHost(host)}...");
                client.Connect();

                string localFileName = Path.GetFileName(normalizedRemoteFilePath) ?? throw new ArgumentException("Не удалось определить имя удаленного файла.");
                string localPath = Path.Combine(localTempDirectory, localFileName);
                progress?.Report($"Скачивание: {localFileName}");

                using var stream = File.Create(localPath);
                client.DownloadFile(normalizedRemoteFilePath, stream);
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

            string normalizedRemoteDirectoryPath = NormalizeRemotePath(remoteDirectoryPath);

            using var client = CreateClient(host, port, username, password);
            try
            {
                client.Connect();

                return client.ListDirectory(normalizedRemoteDirectoryPath)
                    .Where(file => !file.IsDirectory)
                    .Select(file => NormalizeRemotePath(file.FullName))
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
        if (slashIndex >= 0)
            host = host[..slashIndex];

        int colonIndex = host.LastIndexOf(':');
        if (colonIndex > 0 && host.Count(ch => ch == ':') == 1)
            host = host[..colonIndex];

        return host;
    }

    private static string NormalizeRemotePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("SFTP-путь не указан");

        string normalized = path.Trim().Replace('\\', '/');

        while (normalized.Contains("//", StringComparison.Ordinal))
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);

        if (normalized.Length > 1)
            normalized = normalized.TrimEnd('/');

        return normalized;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SshFileHandler));
    }
}
