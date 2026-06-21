using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LogCollectorApp.Services;

public class LogSearcher
{
    public static List<string> FindLogFilesByDate(List<string> files, DateTime targetDate, string groupName)
    {
        string datePattern = GetDatePattern(groupName, targetDate);
        
        Console.WriteLine($"\n=== Поиск файлов за {targetDate:yyyy-MM-dd} ===");
        Console.WriteLine($"Паттерн: '{datePattern}'");

        var foundFiles = new List<string>();

        foreach (var file in files)
        {
            string fileName = Path.GetFileName(file);
            if (fileName.Contains(datePattern, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"✓ Найден: {fileName}");
                foundFiles.Add(file);
            }
        }

        Console.WriteLine($"Найдено файлов: {foundFiles.Count}");
        return foundFiles;
    }

    public static List<string> SearchLogsByTimeRange(string filePath, DateTime startTime, DateTime endTime, string groupName)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Файл не найден: {filePath}");
        }

        Console.WriteLine($"\n=== ПОИСК В ФАЙЛЕ: {Path.GetFileName(filePath)} ===");
        Console.WriteLine($"Ищем время: {startTime:HH:mm} - {endTime:HH:mm}");
        Console.WriteLine($"Группа: {groupName}");

        var allLines = File.ReadAllLines(filePath);
        var foundLines = new List<string>();

        foreach (var line in allLines)
        {
            string trimmedLine = line.TrimStart();
            
            // 🔥 РАЗНАЯ ЛОГИКА ДЛЯ APP И WEB
            if (groupName.ToLower() == "app")
            {
                // App: ищем DateTime=2026-06-08T12:30:42
                if (trimmedLine.StartsWith("DateTime=", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(trimmedLine, @"DateTime=\d{4}-\d{2}-\d{2}T(\d{2}):(\d{2}):\d{2}");
                    
                    if (match.Success)
                    {
                        int hour = int.Parse(match.Groups[1].Value);
                        int minute = int.Parse(match.Groups[2].Value);
                        int logTimeMinutes = hour * 60 + minute;
                        int startTimeMinutes = startTime.Hour * 60 + startTime.Minute;
                        int endTimeMinutes = endTime.Hour * 60 + endTime.Minute;

                        if (logTimeMinutes >= startTimeMinutes && logTimeMinutes <= endTimeMinutes)
                        {
                            Console.WriteLine($"  ✓ APP: {hour:D2}:{minute:D2}");
                            foundLines.Add(trimmedLine);
                        }
                    }
                }
            }
            else // web
            {
                // 🔥 Web: ищем 2026-06-09 08:37:48 в начале строки
                var match = Regex.Match(trimmedLine, @"^(\d{4}-\d{2}-\d{2}) (\d{2}):(\d{2}):\d{2}");
                
                if (match.Success)
                {
                    int hour = int.Parse(match.Groups[2].Value);
                    int minute = int.Parse(match.Groups[3].Value);
                    int logTimeMinutes = hour * 60 + minute;
                    int startTimeMinutes = startTime.Hour * 60 + startTime.Minute;
                    int endTimeMinutes = endTime.Hour * 60 + endTime.Minute;

                    if (logTimeMinutes >= startTimeMinutes && logTimeMinutes <= endTimeMinutes)
                    {
                        Console.WriteLine($"  ✓ WEB: {hour:D2}:{minute:D2}");
                        foundLines.Add(trimmedLine);
                    }
                }
            }
        }

        Console.WriteLine($"Найдено строк: {foundLines.Count}");
        return foundLines;
    }

    public static List<string> ExtractFullLogEntries(List<string> foundLines, string[] allLines, string groupName)
    {
        Console.WriteLine($"\n=== Извлечение записей (группа: {groupName}) ===");
        Console.WriteLine($"Найдено строк: {foundLines.Count}");

        if (groupName.ToLower() == "web")
        {
            // 🔥 Для web: каждая строка - отдельная запись
            Console.WriteLine($"WEB: каждая строка - отдельная запись");
            return foundLines;
        }
        else
        {
            // Для app: извлекаем полные записи
            Console.WriteLine($"APP: извлекаем полные записи");
            
            var fullEntries = new List<string>();
            var currentEntry = new List<string>();
            bool currentEntryHasTarget = false;

            foreach (var line in allLines)
            {
                string trimmedLine = line.TrimStart();
                
                if (trimmedLine.StartsWith("StorageServerRuntime", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentEntryHasTarget && currentEntry.Count > 0)
                    {
                        fullEntries.Add(string.Join(Environment.NewLine, currentEntry));
                        Console.WriteLine($"  Сохранена запись ({currentEntry.Count} строк)");
                    }

                    currentEntry.Clear();
                    currentEntry.Add(line);
                    currentEntryHasTarget = false;
                }
                else
                {
                    if (currentEntry.Count > 0)
                    {
                        currentEntry.Add(line);
                        
                        if (!currentEntryHasTarget && foundLines.Any(found => trimmedLine.Contains(found, StringComparison.OrdinalIgnoreCase)))
                        {
                            currentEntryHasTarget = true;
                            Console.WriteLine($"  Запись содержит целевое время");
                        }
                    }
                }
            }

            if (currentEntryHasTarget && currentEntry.Count > 0)
            {
                fullEntries.Add(string.Join(Environment.NewLine, currentEntry));
                Console.WriteLine($"  Сохранена запись ({currentEntry.Count} строк)");
            }

            Console.WriteLine($"Всего извлечено записей: {fullEntries.Count}");
            return fullEntries;
        }
    }

    private static string GetDatePattern(string groupName, DateTime date)
    {
        return groupName.ToLower() switch
        {
            "app" => $"log {date:yyyy}Y{date:MM}M{date:dd}D",
            "web" => $"DDM_Web_plain_{date:yyyyMMdd}",
            _ => $"log {date:yyyy}Y{date:MM}M{date:dd}D"
        };
    }
}