using LogCollectorApp.Services;
using NUnit.Framework;

namespace LogCollectorApp.Tests.Services;

[TestFixture]
[Category("Integration")]
[NonParallelizable]
public class SshFileHandlerIntegrationTests
{
    private SshFileHandler _handler = null!;
    private string _tempDir = null!;

    private string _host = null!;
    private int _port;
    private string _username = null!;
    private string _password = null!;
    private string _remoteDirectory = null!;
    private string _remoteFilePath = null!;
    private string _remoteFileName = null!;
    private string? _expectedText;

    [SetUp]
    public void SetUp()
    {
        _host = GetRequiredEnvironmentVariable("LOGCOLLECTOR_SFTP_HOST");
        _username = GetRequiredEnvironmentVariable("LOGCOLLECTOR_SFTP_USER");
        _password = GetRequiredEnvironmentVariable("LOGCOLLECTOR_SFTP_PASSWORD");
        _remoteDirectory = NormalizeRemotePath(GetRequiredEnvironmentVariable("LOGCOLLECTOR_SFTP_DIR"));

        string portText = GetRequiredEnvironmentVariable("LOGCOLLECTOR_SFTP_PORT");

        if (!int.TryParse(portText, out _port))
            Assert.Ignore("LOGCOLLECTOR_SFTP_PORT должен быть числом.");

        string? configuredFile = Environment.GetEnvironmentVariable("LOGCOLLECTOR_SFTP_FILE");

        if (string.IsNullOrWhiteSpace(configuredFile))
            Assert.Ignore("Интеграционный SFTP-тест пропущен: переменная LOGCOLLECTOR_SFTP_FILE не задана.");

        _remoteFilePath = BuildRemoteFilePath(_remoteDirectory, configuredFile);
        _remoteFileName = Path.GetFileName(_remoteFilePath) ?? _remoteFilePath.Trim('/');
        _expectedText = Environment.GetEnvironmentVariable("LOGCOLLECTOR_SFTP_EXPECTED_TEXT");

        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _handler = new SshFileHandler();
    }

    [TearDown]
    public void TearDown()
    {
        _handler?.Dispose();

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task GetFilesListAsync_WhenRemoteDirectoryContainsTestFile_ReturnsRemoteFilePath()
    {
        List<string> files = await _handler.GetFilesListAsync(
            _host,
            _port,
            _username,
            _password,
            _remoteDirectory,
            CancellationToken.None);

        AssertRemoteFileIsPresent(files, _remoteFilePath, _remoteFileName);
    }

    [Test]
    public async Task DownloadFileAsync_WhenRemoteFileExists_CreatesLocalFile()
    {
        await DownloadTestFile();

        string localFilePath = Path.Combine(_tempDir, _remoteFileName);

        Assert.That(File.Exists(localFilePath), Is.True);
    }

    [Test]
    public async Task DownloadFileAsync_WhenRemoteFileExists_DownloadsNonEmptyContent()
    {
        await DownloadTestFile();

        string localFilePath = Path.Combine(_tempDir, _remoteFileName);
        var fileInfo = new FileInfo(localFilePath);

        Assert.That(fileInfo.Exists, Is.True);
        Assert.That(fileInfo.Length, Is.GreaterThan(0));

        if (!string.IsNullOrWhiteSpace(_expectedText))
            Assert.That(FileContainsText(localFilePath, _expectedText), Is.True);
    }

    [Test]
    public async Task DownloadFileAsync_WhenProgressProvided_ReportsConnectionAndDownloadMessages()
    {
        var progress = new TestProgress();

        await _handler.DownloadFileAsync(
            _host,
            _port,
            _username,
            _password,
            _remoteFilePath,
            _tempDir,
            progress,
            CancellationToken.None);

        Assert.That(progress.Messages, Has.Some.Contains("Подключение"));
        Assert.That(progress.Messages, Has.Some.Contains("Скачивание"));
        Assert.That(progress.Messages, Has.Some.Contains(_remoteFileName));
    }

    [Test]
    public async Task GetFilesListAsync_WhenHostContainsProtocol_StillConnects()
    {
        string hostWithProtocol = $"sftp://{_host}";

        List<string> files = await _handler.GetFilesListAsync(
            hostWithProtocol,
            _port,
            _username,
            _password,
            _remoteDirectory,
            CancellationToken.None);

        AssertRemoteFileIsPresent(files, _remoteFilePath, _remoteFileName);
    }

    private Task DownloadTestFile()
    {
        return _handler.DownloadFileAsync(
            _host,
            _port,
            _username,
            _password,
            _remoteFilePath,
            _tempDir,
            new Progress<string>(),
            CancellationToken.None);
    }

    private static string GetRequiredEnvironmentVariable(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(value))
            Assert.Ignore($"Интеграционный SFTP-тест пропущен: переменная окружения {name} не задана.");

        return value;
    }

    private static string BuildRemoteFilePath(string directory, string fileOrPath)
    {
        string normalizedFileOrPath = NormalizeRemotePath(fileOrPath);

        if (normalizedFileOrPath.StartsWith('/'))
            return normalizedFileOrPath;

        return $"{NormalizeRemotePath(directory).TrimEnd('/')}/{normalizedFileOrPath.TrimStart('/')}";
    }

    private static string NormalizeRemotePath(string path)
    {
        string result = path.Trim().Replace('\\', '/');

        while (result.Contains("//", StringComparison.Ordinal))
            result = result.Replace("//", "/", StringComparison.Ordinal);

        if (result.Length > 1)
            result = result.TrimEnd('/');

        return result;
    }

    private static void AssertRemoteFileIsPresent(IEnumerable<string> files, string expectedRemoteFilePath, string expectedRemoteFileName)
    {
        var normalizedFiles = files.Select(NormalizeRemotePath).ToList();
        string normalizedExpectedPath = NormalizeRemotePath(expectedRemoteFilePath);

        bool exactPathFound = normalizedFiles.Contains(normalizedExpectedPath, StringComparer.OrdinalIgnoreCase);
        bool fileNameFound = normalizedFiles.Any(file =>
            string.Equals(Path.GetFileName(file), expectedRemoteFileName, StringComparison.OrdinalIgnoreCase));

        Assert.That(exactPathFound || fileNameFound, Is.True,
            $"В каталоге не найден файл {normalizedExpectedPath}. Фактически получено: {string.Join(", ", normalizedFiles)}");
    }

    private static bool FileContainsText(string path, string expectedText)
    {
        using var reader = new StreamReader(path);

        while (reader.ReadLine() is { } line)
        {
            if (line.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private sealed class TestProgress : IProgress<string>
    {
        private readonly object _lock = new();

        public List<string> Messages { get; } = new();

        public void Report(string value)
        {
            lock (_lock)
            {
                Messages.Add(value);
            }
        }
    }
}
