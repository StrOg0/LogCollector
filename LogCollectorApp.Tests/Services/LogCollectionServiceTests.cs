using LogCollectorApp.Models;
using LogCollectorApp.Services;
using LogCollectorApp.Tests.Fakes;

namespace LogCollectorApp.Tests.Services;

[TestFixture]
public class LogCollectionServiceTests
{
    private string _testRoot = null!;
    private string _tempRoot = null!;
    private string _outputDir = null!;
    private FakeSshFileHandler _fakeSsh = null!;
    private LogCollectionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _tempRoot = Path.Combine(_testRoot, "temp");
        _outputDir = Path.Combine(_testRoot, "output");

        Directory.CreateDirectory(_tempRoot);
        Directory.CreateDirectory(_outputDir);

        _fakeSsh = new FakeSshFileHandler();
        _service = new LogCollectionService(_fakeSsh, new ArchiveManager());
    }

    [TearDown]
    public void TearDown()
    {
        _fakeSsh?.Dispose();

        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    [Test]
    public async Task CollectLogsAsync_WhenAppServerHasMatchingLog_ReturnsSuccessAndCreatesResultFile()
    {
        const string remoteDirectory = "/var/log/digdes/sdu";
        const string remoteFile = "/var/log/digdes/sdu/log 2026Y06M08D 14H00M00S.log";

        _fakeSsh.DirectoryFiles[remoteDirectory] = new List<string>
        {
            remoteFile
        };

        _fakeSsh.RemoteTextFiles[remoteFile] =
            """
            StorageServerRuntime first entry
            User=Ivanov
            DateTime=2026-06-08T14:00:00 target line
            Action=OpenDocument
            StorageServerRuntime second entry
            DateTime=2026-06-08T15:00:00 outside range
            Action=CloseDocument
            """;

        Server server = CreateAppServer();

        CollectionResult result = await _service.CollectLogsAsync(
            server,
            new DateTime(2026, 6, 8, 14, 0, 0),
            new DateTime(2026, 6, 8, 14, 5, 0),
            _tempRoot,
            _outputDir,
            progress: null!,
            CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(CollectionStatus.Success));
        Assert.That(result.Message, Does.Contain("Найдено"));
        Assert.That(File.Exists(result.ResultFilePath), Is.True);

        string resultContent = File.ReadAllText(result.ResultFilePath);

        Assert.That(resultContent, Does.Contain("StorageServerRuntime first entry"));
        Assert.That(resultContent, Does.Contain("target line"));
        Assert.That(resultContent, Does.Contain("Action=OpenDocument"));
        Assert.That(resultContent, Does.Not.Contain("outside range"));

        Assert.That(_fakeSsh.RequestedDirectories, Does.Contain(remoteDirectory));
        Assert.That(_fakeSsh.DownloadedFiles, Does.Contain(remoteFile));
    }

    private static Server CreateAppServer()
    {
        return new Server
        {
            Id = 1,
            GroupId = 1,
            Name = "app-1",
            IpAddress = "10.10.130.6",
            SshPort = 22,
            SshUsername = "test_user",
            SshPassword = "test_password",
            Group = new ServerGroup
            {
                Id = 1,
                Name = "app"
            }
        };
    }

    [Test]
    public async Task CollectLogsAsync_WhenAppServerHasNoFiles_ReturnsNoData()
    {
        const string remoteDirectory = "/var/log/digdes/sdu";

        _fakeSsh.DirectoryFiles[remoteDirectory] = new List<string>();

        Server server = CreateAppServer();

        CollectionResult result = await _service.CollectLogsAsync(
            server,
            new DateTime(2026, 6, 8, 14, 0, 0),
            new DateTime(2026, 6, 8, 14, 5, 0),
            _tempRoot,
            _outputDir,
            progress: null!,
            CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(CollectionStatus.NoData));
        Assert.That(result.Message, Is.EqualTo("Записи не найдены"));
        Assert.That(result.ResultFilePath, Is.Empty);
        Assert.That(_fakeSsh.DownloadedFiles, Is.Empty);
    }

    [Test]
    public async Task CollectLogsAsync_WhenAppDirectoryIsUnavailable_ReturnsError()
    {
        const string remoteDirectory = "/var/log/digdes/sdu";

        _fakeSsh.DirectoriesThatThrow.Add(remoteDirectory);

        Server server = CreateAppServer();

        CollectionResult result = await _service.CollectLogsAsync(
            server,
            new DateTime(2026, 6, 8, 14, 0, 0),
            new DateTime(2026, 6, 8, 14, 5, 0),
            _tempRoot,
            _outputDir,
            progress: null!,
            CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(CollectionStatus.Error));
        Assert.That(result.Message, Does.Contain("Каталог недоступен"));
        Assert.That(result.ResultFilePath, Is.Empty);
    }

    [Test]
    public async Task CollectLogsAsync_WhenDownloadFails_ReturnsError()
    {
        const string remoteDirectory = "/var/log/digdes/sdu";
        const string remoteFile = "/var/log/digdes/sdu/log 2026Y06M08D 14H00M00S.log";

        _fakeSsh.DirectoryFiles[remoteDirectory] = new List<string>
    {
        remoteFile
    };

        _fakeSsh.FilesThatThrowOnDownload.Add(remoteFile);

        Server server = CreateAppServer();

        CollectionResult result = await _service.CollectLogsAsync(
            server,
            new DateTime(2026, 6, 8, 14, 0, 0),
            new DateTime(2026, 6, 8, 14, 5, 0),
            _tempRoot,
            _outputDir,
            progress: null!,
            CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(CollectionStatus.Error));
        Assert.That(result.Message, Does.Contain("Файл недоступен"));
        Assert.That(_fakeSsh.DownloadedFiles, Does.Contain(remoteFile));
    }

    [Test]
    public async Task CollectLogsAsync_WhenCancellationRequested_ReturnsCancelled()
    {
        Server server = CreateAppServer();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        CollectionResult result = await _service.CollectLogsAsync(
            server,
            new DateTime(2026, 6, 8, 14, 0, 0),
            new DateTime(2026, 6, 8, 14, 5, 0),
            _tempRoot,
            _outputDir,
            progress: null!,
            cts.Token);

        Assert.That(result.Status, Is.EqualTo(CollectionStatus.Cancelled));
        Assert.That(result.Message, Is.EqualTo("Отменено"));
    }

    [Test]
    public async Task CollectLogsAsync_WhenOperationFinished_DeletesTempDirectory()
    {
        const string remoteDirectory = "/var/log/digdes/sdu";
        const string remoteFile = "/var/log/digdes/sdu/log 2026Y06M08D 14H00M00S.log";

        _fakeSsh.DirectoryFiles[remoteDirectory] = new List<string>
    {
        remoteFile
    };

        _fakeSsh.RemoteTextFiles[remoteFile] =
            """
        StorageServerRuntime first entry
        DateTime=2026-06-08T14:00:00 target line
        Action=OpenDocument
        """;

        Server server = CreateAppServer();

        CollectionResult result = await _service.CollectLogsAsync(
            server,
            new DateTime(2026, 6, 8, 14, 0, 0),
            new DateTime(2026, 6, 8, 14, 5, 0),
            _tempRoot,
            _outputDir,
            progress: null!,
            CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(CollectionStatus.Success));
        Assert.That(Directory.Exists(_tempRoot), Is.False);
        Assert.That(File.Exists(result.ResultFilePath), Is.True);
    }


}