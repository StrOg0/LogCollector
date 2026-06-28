using LogCollectorApp.Services;
using NUnit.Framework;

namespace LogCollectorApp.Tests.Services;

[TestFixture]
public class LogSearcherTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void FindLogFilesByDate_WhenGroupIsWeb_ReturnsOnlyFilesForSelectedDate()
    {
        var files = new List<string>
        {
            "DDM_Web_plain_20260608_140000.zip",
            "DDM_Web_plain_20260608_150000.zip",
            "DDM_Web_plain_20260609_140000.zip",
            "other_file.zip"
        };

        var result = LogSearcher.FindLogFilesByDate(
            files,
            new DateTime(2026, 6, 8),
            "web");

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result, Does.Contain("DDM_Web_plain_20260608_140000.zip"));
        Assert.That(result, Does.Contain("DDM_Web_plain_20260608_150000.zip"));
    }

    [Test]
    public void FindLogFilesByDate_WhenGroupIsApp_ReturnsOnlyFilesForSelectedDate()
    {
        var files = new List<string>
        {
            "log 2026Y06M08D 14H00M00S.log",
            "log 2026Y06M08D 15H00M00S.log",
            "log 2026Y06M09D 14H00M00S.log",
            "DDM_Web_plain_20260608_140000.zip"
        };

        var result = LogSearcher.FindLogFilesByDate(
            files,
            new DateTime(2026, 6, 8),
            "app");

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result, Does.Contain("log 2026Y06M08D 14H00M00S.log"));
        Assert.That(result, Does.Contain("log 2026Y06M08D 15H00M00S.log"));
    }

    [Test]
    public void SearchLogsByTimeRange_WhenWebLogContainsLinesInsideRange_ReturnsMatchingLines()
    {
        string filePath = Path.Combine(_tempDir, "DDM_Web.log");

        File.WriteAllLines(filePath, new[]
        {
            "2026-06-08 13:59:59 before",
            "2026-06-08 14:00:00 first target",
            "    2026-06-08 14:03:59 second target with spaces",
            "not a log timestamp",
            "2026-06-08 14:05:00 third target",
            "2026-06-08 14:06:00 after"
        });

        var result = LogSearcher.SearchLogsByTimeRange(
            filePath,
            new DateTime(2026, 6, 8, 14, 0, 0),
            new DateTime(2026, 6, 8, 14, 5, 0),
            "web");

        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result[0], Does.Contain("first target"));
        Assert.That(result[1], Does.Contain("second target"));
        Assert.That(result[2], Does.Contain("third target"));
    }

    [Test]
    public void SearchLogsByTimeRange_WhenAppLogContainsDateTimeAttribute_ReturnsMatchingLines()
    {
        string filePath = Path.Combine(_tempDir, "app.log");

        File.WriteAllLines(filePath, new[]
        {
            "StorageServerRuntime",
            "DateTime=2026-06-08T13:59:59 before",
            "DateTime=2026-06-08T14:00:00 first target",
            "DateTime=2026-06-08T14:04:30 second target",
            "DateTime=2026-06-08T14:06:00 after"
        });

        var result = LogSearcher.SearchLogsByTimeRange(
            filePath,
            new DateTime(2026, 6, 8, 14, 0, 0),
            new DateTime(2026, 6, 8, 14, 5, 0),
            "app");

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Does.Contain("first target"));
        Assert.That(result[1], Does.Contain("second target"));
    }

    [Test]
    public void SearchLogsByTimeRange_WhenFileDoesNotExist_ThrowsFileNotFoundException()
    {
        string filePath = Path.Combine(_tempDir, "missing.log");

        Assert.Throws<FileNotFoundException>(() =>
        {
            LogSearcher.SearchLogsByTimeRange(
                filePath,
                new DateTime(2026, 6, 8, 14, 0, 0),
                new DateTime(2026, 6, 8, 14, 5, 0),
                "web");
        });
    }

    [Test]
    public void ExtractFullLogEntries_WhenGroupIsWeb_ReturnsFullEntryWithContinuationLines()
    {
        string[] allLines =
        {
            "2026-06-08 14:00:00 first target line",
            " continuation line 1",
            " continuation line 2",
            "2026-06-08 14:01:00 another entry",
            " another continuation"
        };

        var foundLines = new List<string>
        {
            "2026-06-08 14:00:00 first target line"
        };

        var result = LogSearcher.ExtractFullLogEntries(foundLines, allLines, "web");

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Does.Contain("first target line"));
        Assert.That(result[0], Does.Contain("continuation line 1"));
        Assert.That(result[0], Does.Contain("continuation line 2"));
        Assert.That(result[0], Does.Not.Contain("another entry"));
    }

    [Test]
    public void ExtractFullLogEntries_WhenGroupIsApp_ReturnsFullStorageServerRuntimeEntry()
    {
        string[] allLines =
        {
            "StorageServerRuntime first entry",
            "User=Ivanov",
            "DateTime=2026-06-08T14:00:00 target line",
            "Action=OpenDocument",
            "StorageServerRuntime second entry",
            "DateTime=2026-06-08T14:01:00 another line",
            "Action=CloseDocument"
        };

        var foundLines = new List<string>
        {
            "DateTime=2026-06-08T14:00:00 target line"
        };

        var result = LogSearcher.ExtractFullLogEntries(foundLines, allLines, "app");

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Does.Contain("StorageServerRuntime first entry"));
        Assert.That(result[0], Does.Contain("User=Ivanov"));
        Assert.That(result[0], Does.Contain("target line"));
        Assert.That(result[0], Does.Contain("Action=OpenDocument"));
        Assert.That(result[0], Does.Not.Contain("StorageServerRuntime second entry"));
    }

    [Test]
    public void ExtractFullLogEntries_WhenFoundLinesIsEmpty_ReturnsEmptyList()
    {
        string[] allLines =
        {
            "2026-06-08 14:00:00 first line",
            " continuation",
            "2026-06-08 14:01:00 second line"
        };

        var result = LogSearcher.ExtractFullLogEntries(
            new List<string>(),
            allLines,
            "web");

        Assert.That(result, Is.Empty);
    }
}