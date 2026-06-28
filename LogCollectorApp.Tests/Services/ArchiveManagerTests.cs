using System.IO.Compression;
using LogCollectorApp.Models;
using LogCollectorApp.Services;
using NUnit.Framework;

namespace LogCollectorApp.Tests.Services;

[TestFixture]
public class ArchiveManagerTests
{
    private string _tempDir = null!;
    private ArchiveManager _archiveManager = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _archiveManager = new ArchiveManager();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void ExtractArchives_WhenSourceArchiveDoesNotExist_ThrowsFileNotFoundException()
    {
        string missingArchivePath = Path.Combine(_tempDir, "missing.zip");
        string extractDir = Path.Combine(_tempDir, "extract");

        Assert.Throws<FileNotFoundException>(() =>
        {
            _archiveManager.ExtractArchives(missingArchivePath, extractDir);
        });
    }

    [Test]
    public void ExtractArchives_WhenZipContainsLogFile_ReturnsExtractedLogFile()
    {
        string archivePath = Path.Combine(_tempDir, "logs.zip");
        string sourceLogPath = Path.Combine(_tempDir, "DDM_Web.log");

        File.WriteAllText(sourceLogPath, "2026-06-08 14:00:00 test log line");

        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(sourceLogPath, "DDM_Web.log");
        }

        string extractDir = Path.Combine(_tempDir, "extract");

        List<string> result = _archiveManager.ExtractArchives(archivePath, extractDir);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(File.Exists(result[0]), Is.True);
        Assert.That(Path.GetFileName(result[0]), Does.EndWith("DDM_Web.log"));
        Assert.That(File.ReadAllText(result[0]), Does.Contain("test log line"));
    }

    [Test]
    public void ExtractArchives_WhenZipContainsNestedZip_ReturnsLogFromNestedArchive()
    {
        string innerLogPath = Path.Combine(_tempDir, "inner.log");
        string innerZipPath = Path.Combine(_tempDir, "inner.zip");
        string outerZipPath = Path.Combine(_tempDir, "outer.zip");

        File.WriteAllText(innerLogPath, "log from nested archive");

        using (var innerArchive = ZipFile.Open(innerZipPath, ZipArchiveMode.Create))
        {
            innerArchive.CreateEntryFromFile(innerLogPath, "inner.log");
        }

        using (var outerArchive = ZipFile.Open(outerZipPath, ZipArchiveMode.Create))
        {
            outerArchive.CreateEntryFromFile(innerZipPath, "inner.zip");
        }

        string extractDir = Path.Combine(_tempDir, "extract");

        List<string> result = _archiveManager.ExtractArchives(outerZipPath, extractDir);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(File.Exists(result[0]), Is.True);
        Assert.That(File.ReadAllText(result[0]), Does.Contain("log from nested archive"));
    }

    [Test]
    public void ExtractArchives_WhenArchiveContainsNonLogFiles_IgnoresThem()
    {
        string archivePath = Path.Combine(_tempDir, "mixed.zip");
        string logPath = Path.Combine(_tempDir, "result.log");
        string txtPath = Path.Combine(_tempDir, "readme.txt");

        File.WriteAllText(logPath, "log content");
        File.WriteAllText(txtPath, "not log content");

        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(logPath, "result.log");
            archive.CreateEntryFromFile(txtPath, "readme.txt");
        }

        string extractDir = Path.Combine(_tempDir, "extract");

        List<string> result = _archiveManager.ExtractArchives(archivePath, extractDir);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(Path.GetFileName(result[0]), Does.EndWith(".log"));
    }

    [Test]
    public void ExtractArchives_WhenArchiveIsCorrupted_ReturnsEmptyList()
    {
        string archivePath = Path.Combine(_tempDir, "broken.zip");
        File.WriteAllText(archivePath, "this is not a real zip archive");

        string extractDir = Path.Combine(_tempDir, "extract");

        List<string> result = _archiveManager.ExtractArchives(archivePath, extractDir);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void CreateResultArchive_WhenProcessedLogsExist_CreatesZipArchive()
    {
        string tempLogPath = Path.Combine(_tempDir, "server1_temp.log");
        string resultArchivePath = Path.Combine(_tempDir, "result.zip");

        File.WriteAllText(tempLogPath, "filtered log content");

        var processedLogs = new List<ProcessedLogInfo>
        {
            new()
            {
                ServerIp = "10.10.130.6",
                ServerName = "server1",
                TempFilePath = tempLogPath,
                LogDate = new DateTime(2026, 6, 8)
            }
        };

        string result = _archiveManager.CreateResultArchive(resultArchivePath, processedLogs);

        Assert.That(result, Is.EqualTo(resultArchivePath));
        Assert.That(File.Exists(resultArchivePath), Is.True);
    }

    [Test]
    public void CreateResultArchive_WhenProcessedLogsExist_CreatesEntryNamedByServerIp()
    {
        string tempLogPath = Path.Combine(_tempDir, "server1_temp.log");
        string resultArchivePath = Path.Combine(_tempDir, "result.zip");

        File.WriteAllText(tempLogPath, "filtered log content");

        var processedLogs = new List<ProcessedLogInfo>
        {
            new()
            {
                ServerIp = "10.10.130.6",
                ServerName = "server1",
                TempFilePath = tempLogPath,
                LogDate = new DateTime(2026, 6, 8)
            }
        };

        _archiveManager.CreateResultArchive(resultArchivePath, processedLogs);

        using ZipArchive archive = ZipFile.OpenRead(resultArchivePath);

        ZipArchiveEntry? entry = archive.GetEntry("10.10.130.6.log");

        Assert.That(entry, Is.Not.Null);
    }

    [Test]
    public void CreateResultArchive_WhenSeveralLogsHaveSameServerIp_CreatesUniqueEntryNames()
    {
        string firstLogPath = Path.Combine(_tempDir, "first.log");
        string secondLogPath = Path.Combine(_tempDir, "second.log");
        string resultArchivePath = Path.Combine(_tempDir, "result.zip");

        File.WriteAllText(firstLogPath, "first content");
        File.WriteAllText(secondLogPath, "second content");

        var processedLogs = new List<ProcessedLogInfo>
        {
            new()
            {
                ServerIp = "10.10.130.6",
                ServerName = "server1",
                TempFilePath = firstLogPath,
                LogDate = new DateTime(2026, 6, 8)
            },
            new()
            {
                ServerIp = "10.10.130.6",
                ServerName = "server1",
                TempFilePath = secondLogPath,
                LogDate = new DateTime(2026, 6, 8)
            }
        };

        _archiveManager.CreateResultArchive(resultArchivePath, processedLogs);

        using ZipArchive archive = ZipFile.OpenRead(resultArchivePath);

        var entryNames = archive.Entries.Select(x => x.FullName).ToList();

        Assert.That(entryNames, Has.Count.EqualTo(2));
        Assert.That(entryNames, Does.Contain("10.10.130.6.log"));
        Assert.That(entryNames, Does.Contain("10.10.130.6_part2.log"));
    }

    [Test]
    public void CreateResultArchive_WhenNoExistingLogFiles_ThrowsArgumentException()
    {
        string resultArchivePath = Path.Combine(_tempDir, "result.zip");

        var processedLogs = new List<ProcessedLogInfo>
        {
            new()
            {
                ServerIp = "10.10.130.6",
                ServerName = "server1",
                TempFilePath = Path.Combine(_tempDir, "missing.log"),
                LogDate = new DateTime(2026, 6, 8)
            }
        };

        Assert.Throws<ArgumentException>(() =>
        {
            _archiveManager.CreateResultArchive(resultArchivePath, processedLogs);
        });
    }
}