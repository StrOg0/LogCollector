using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LogCollectorApp.Services;

public static class LogSearcher
{
    private static readonly Regex AppDateTimeRegex = new(@"DateTime=\d{4}-\d{2}-\d{2}T(\d{2}):(\d{2}):\d{2}", RegexOptions.Compiled);

    public static List<string> FindLogFilesByDate(List<string> files, DateTime date, string groupName)
    {
        string pattern = GetDatePattern(groupName, date);
        return files
            .Where(file => Path.GetFileName(file).Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static List<string> SearchLogsByTimeRange(string filePath, DateTime startTime, DateTime endTime, string groupName)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException($"Файл не найден: {filePath}");

        int start = ToMinutes(startTime);
        int end = ToMinutes(endTime);
        bool isApp = groupName.Equals("app", StringComparison.OrdinalIgnoreCase);

        return File.ReadLines(filePath)
            .Select(line => line.TrimStart())
            .Where(line => TryGetLogMinutes(line, isApp, out int minutes) && minutes >= start && minutes <= end)
            .ToList();
    }

    public static List<string> ExtractFullLogEntries(List<string> foundLines, string[] allLines, string groupName)
    {
        return groupName.Equals("web", StringComparison.OrdinalIgnoreCase)
            ? ExtractEntries(foundLines, allLines, IsWebLogLine)
            : ExtractAppEntries(foundLines, allLines);
    }

    private static List<string> ExtractEntries(IEnumerable<string> foundLines, IEnumerable<string> allLines, Func<string, bool> isNewEntry)
    {
        var targets = new HashSet<string>(foundLines);
        var result = new List<string>();
        var current = new List<string>();
        bool hasTarget = false;

        foreach (var line in allLines)
        {
            string trimmed = line.TrimStart();
            if (isNewEntry(trimmed))
            {
                AddIfTarget(result, current, hasTarget);
                current.Clear();
                hasTarget = targets.Contains(trimmed);
            }

            if (current.Count > 0 || isNewEntry(trimmed)) current.Add(line);
        }

        AddIfTarget(result, current, hasTarget);
        return result;
    }

    private static List<string> ExtractAppEntries(List<string> foundLines, string[] allLines)
    {
        var targets = new HashSet<string>(foundLines);
        var result = new List<string>();
        var current = new List<string>();
        bool hasTarget = false;

        foreach (var line in allLines)
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("StorageServerRuntime", StringComparison.OrdinalIgnoreCase))
            {
                AddIfTarget(result, current, hasTarget);
                current.Clear();
                hasTarget = false;
            }

            if (current.Count > 0 || trimmed.StartsWith("StorageServerRuntime", StringComparison.OrdinalIgnoreCase))
            {
                current.Add(line);
                hasTarget |= targets.Contains(trimmed);
            }
        }

        AddIfTarget(result, current, hasTarget);
        return result;
    }

    private static void AddIfTarget(List<string> result, List<string> entry, bool hasTarget)
    {
        if (hasTarget && entry.Count > 0) result.Add(string.Join(Environment.NewLine, entry));
    }

    private static bool TryGetLogMinutes(string line, bool isApp, out int minutes)
    {
        minutes = 0;
        if (isApp)
        {
            var match = AppDateTimeRegex.Match(line);
            if (!match.Success) return false;

            minutes = int.Parse(match.Groups[1].Value) * 60 + int.Parse(match.Groups[2].Value);
            return true;
        }

        if (!IsWebLogLine(line)) return false;
        minutes = (line[11] - '0') * 600 + (line[12] - '0') * 60 + (line[14] - '0') * 10 + line[15] - '0';
        return true;
    }

    private static bool IsWebLogLine(string line) =>
        line.Length >= 19 &&
        char.IsDigit(line[0]) && char.IsDigit(line[1]) && char.IsDigit(line[2]) && char.IsDigit(line[3]) &&
        line[4] == '-' && line[7] == '-' && line[10] == ' ' && line[13] == ':' && line[16] == ':';

    private static int ToMinutes(DateTime time) => time.Hour * 60 + time.Minute;

    private static string GetDatePattern(string groupName, DateTime date) => groupName.ToLowerInvariant() switch
    {
        "app" => $"log {date:yyyy}Y{date:MM}M{date:dd}D",
        "web" => $"DDM_Web_plain_{date:yyyyMMdd}",
        _ => $"log {date:yyyy}Y{date:MM}M{date:dd}D"
    };
}
