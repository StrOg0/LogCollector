using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LogCollector.Interfaces;
using LogCollectorApp.Models;

namespace LogCollector.BLL
{
    public class LogSearchModule : ILogSearchModule
    {
        public async Task<long> SearchLogsAsync(
            string inputFilePath,
            string outputFilePath,
            DateTime startTime,
            DateTime endTime,
            LogSource logSource,
            bool append = false)
        {
            return await Task.Run(() =>
            {
                long totalLinesFound = 0;

                var encoding = GetEncoding(logSource.Encoding);
                var (netFormat, dateRegex) = BuildDateParser(logSource.TimestampFormat);

                using var reader = new StreamReader(inputFilePath, encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 1024 * 1024);
                using var writer = new StreamWriter(outputFilePath, append, encoding);

                string line;
                var currentRecordLines = new List<string>();
                DateTime? currentRecordTime = null;

                while ((line = reader.ReadLine()) != null)
                {
                    // Пытаемся найти дату в текущей строке
                    DateTime? parsedTime = TryParseDateTime(line, netFormat, dateRegex);

                    if (parsedTime.HasValue)
                    {
                        // Найдена дата → завершаем предыдущую запись (если есть)
                        if (currentRecordTime.HasValue)
                        {
                            if (currentRecordTime.Value >= startTime && currentRecordTime.Value <= endTime)
                            {
                                foreach (var recLine in currentRecordLines)
                                {
                                    writer.WriteLine(recLine);
                                    totalLinesFound++;
                                }
                            }
                        }

                        // Начинаем новую запись с текущей строки
                        currentRecordLines.Clear();
                        currentRecordLines.Add(line);
                        currentRecordTime = parsedTime.Value;
                    }
                    else
                    {
                        // Дата не найдена → добавляем к текущей записи
                        if (currentRecordTime.HasValue)
                        {
                            currentRecordLines.Add(line);
                        }
                        else
                        {
                            // Это "хвост" до первой даты — игнорируем (например, заголовки)
                            // Либо это начало апп-лога без даты — но без даты мы не можем его привязать!
                            // Поэтому такие строки теряются, если до них не было даты.
                            // Это ограничение, но без дополнительных правил начала записи — неизбежно.
                        }
                    }
                }

                // Обработка последней записи
                if (currentRecordTime.HasValue)
                {
                    if (currentRecordTime.Value >= startTime && currentRecordTime.Value <= endTime)
                    {
                        foreach (var recLine in currentRecordLines)
                        {
                            writer.WriteLine(recLine);
                            totalLinesFound++;
                        }
                    }
                }

                return totalLinesFound;
            });
        }

        private Encoding GetEncoding(string encodingName)
        {
            try
            {
                return Encoding.GetEncoding(encodingName.Trim());
            }
            catch
            {
                return Encoding.UTF8;
            }
        }

        private (string netFormat, Regex regex) BuildDateParser(string? timestampFormat)
        {
            if (string.IsNullOrWhiteSpace(timestampFormat))
            {
                // Без формата используем fallback: пытаемся найти любую дату в известных форматах
                // Но для простоты вернём null и будем использовать универсальный парсер
                return (null, null);
            }

            string netFormat = timestampFormat
                .Replace("YYYY", "yyyy")
                .Replace("MM", "MM")
                .Replace("DD", "dd")
                .Replace("HH24", "HH")
                .Replace("HH", "HH") // остаётся HH
                .Replace("MI", "mm")
                .Replace("SS", "ss");

            // Генерация regex: заменяем формат на \d{...}
            string temp = timestampFormat;
            temp = temp.Replace("YYYY", "\x01");
            temp = temp.Replace("MM", "\x02");
            temp = temp.Replace("DD", "\x03");
            temp = temp.Replace("HH24", "\x04");
            temp = temp.Replace("HH", "\x05");
            temp = temp.Replace("MI", "\x06");
            temp = temp.Replace("SS", "\x07");

            string escaped = Regex.Escape(temp);
            escaped = escaped
                .Replace("\x01", @"\d{4}")
                .Replace("\x02", @"\d{2}")
                .Replace("\x03", @"\d{2}")
                .Replace("\x04", @"\d{2}")
                .Replace("\x05", @"\d{2}")
                .Replace("\x06", @"\d{2}")
                .Replace("\x07", @"\d{2}");

            var regex = new Regex(escaped, RegexOptions.Compiled);
            return (netFormat, regex);
        }

        private DateTime? TryParseDateTime(string line, string? netFormat, Regex? dateRegex)
        {
            if (dateRegex != null && !string.IsNullOrEmpty(netFormat))
            {
                var match = dateRegex.Match(line);
                if (match.Success)
                {
                    if (DateTime.TryParseExact(match.Value, netFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    {
                        return dt;
                    }
                }
            }

            // Fallback: пробуем стандартные форматы
            var formats = new[]
            {
                "yyyy-MM-dd HH:mm:ss.ffff",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss.fffffffK"
            };

            foreach (var fmt in formats)
            {
                if (DateTime.TryParseExact(line, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    return dt;
                }

                // Или ищем вхождение
                int len = fmt.Length;
                for (int i = 0; i <= line.Length - len; i++)
                {
                    string substr = line.Substring(i, len);
                    if (DateTime.TryParseExact(substr, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                    {
                        return dt;
                    }
                }
            }

            // Специальный случай: DateTime=2026-06-07T02:15:41...
            if (line.Contains("DateTime="))
            {
                int idx = line.IndexOf("DateTime=") + 9;
                string rest = line.Substring(idx).Trim();
                // Обрезаем всё после даты (например, +03:00 и дальше)
                int spaceIdx = rest.IndexOf(' ');
                if (spaceIdx > 0) rest = rest.Substring(0, spaceIdx);

                if (DateTime.TryParse(rest, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                {
                    return dt;
                }
            }

            return null;
        }
    }
}