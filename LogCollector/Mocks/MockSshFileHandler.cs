using LogCollector.Interfaces;

namespace LogCollector.App.BLL.Mocks;

public class MockSshFileHandler : ISshFileHandler
{
    private bool _disposed;

    public async Task DownloadFileAsync(string host, int port, string username, string password,
        string remoteFilePath, string localTempDirectory, IProgress<string> progress, CancellationToken ct)
    {
        ThrowIfDisposed();

        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"[MOCK] Имитация подключения к {host}...");
            Thread.Sleep(1000); // Имитация задержки сети

            string fileName = Path.GetFileName(remoteFilePath);
            string localFilePath = Path.Combine(localTempDirectory, fileName);

            progress?.Report($"[MOCK] Имитация скачивания {fileName}...");

            File.WriteAllText(localFilePath, $"Mock log data for {fileName}");

            Thread.Sleep(1000);
            progress?.Report($"[MOCK] Файл успешно 'скачан' в {localFilePath}");
        }, ct);
    }

    public async Task<List<string>> GetFilesListAsync(string host, int port, string username, string password,
        string remoteDirectoryPath, CancellationToken ct)
    {
        ThrowIfDisposed();

        return await Task.Run(() =>
        {
            return new List<string>
            {
                "/var/log/app/DDM_Web.log",
                "/var/log/app/archive/2026-06-08.zip"
            };
        }, ct);
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MockSshFileHandler));
    }
}