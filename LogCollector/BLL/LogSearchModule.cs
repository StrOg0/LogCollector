using System.Text;
using System.Text.RegularExpressions;
using LogCollector.Interfaces;

namespace LogCollector.BLL
{
    public enum LogFormatType
    {
        Web, // Дата в начале строки (DDM_Web)
        App  // Дата в конце блока в формате ISO 8601 (StorageServer)
    }

    /// <summary>
    /// Реализация модуля потокового поиска и фильтрации логов.
    /// </summary>
    public class LogSearchModule : ILogSearchModule
    {
        // Скомпилированные регулярные выражения. 
        // RegexOptions.Compiled означает, что Regex компилируется в IL-код при старте приложения,
        // что многократно ускоряет поиск по сравнению с интерпретируемым вариантом.
        private static readonly Regex WebDateRegex = new Regex(
            @"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})",
            RegexOptions.Compiled);

        private static readonly Regex AppDateRegex = new Regex(
            @"DateTime=(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})",
            RegexOptions.Compiled);

        // Размер буфера чтения/записи — 1 МБ.
        // Критически важно для работы с файлами > 1 ГБ: 
        // вместо тысяч мелких обращений к диску, система читает данные крупными блоками.
        private const int BufferSize = 1024 * 1024;

        /// <summary>
        /// Точка входа в модуль. Делегирует работу одному из обработчиков в зависимости от формата.
        /// </summary>
        public async Task<long> SearchLogsAsync(string inputFilePath, string outputFilePath,
            DateTime startTime, DateTime endTime, string searchMask,
            LogFormatType logFormat, bool append = false)
        {
            if (!File.Exists(inputFilePath))
                throw new FileNotFoundException($"Файл лога не найден: {inputFilePath}");

            // Используем using, чтобы гарантировать освобождение файловых дескрипторов
            // даже в случае возникновения исключения.
            using (var reader = new StreamReader(inputFilePath, Encoding.UTF8, true, BufferSize))
            // Параметр append управляет режимом: false — перезапись, true — дописывание в конец
            using (var writer = new StreamWriter(outputFilePath, append, Encoding.UTF8, BufferSize))
            {
                if (logFormat == LogFormatType.Web)
                    return await ProcessWebLogAsync(reader, writer, startTime, endTime, searchMask);

                if (logFormat == LogFormatType.App)
                    return await ProcessAppLogAsync(reader, writer, startTime, endTime, searchMask);
            }

            return 0;
        }

        /// <summary>
        /// Обработка логов Web-сервера (формат DDM_Web).
        /// Особенность: дата и время идут в самом начале каждой строки.
        /// </summary>
        private async Task<long> ProcessWebLogAsync(StreamReader reader, StreamWriter writer,
            DateTime startTime, DateTime endTime, string searchMask)
        {
            long count = 0;
            // Флаг "текущая строка попадает в нужный временной диапазон".
            // Он сохраняет свое значение между строками, чтобы корректно обрабатывать
            // многострочные записи (например, XML-блоки или стек-трейсы), 
            // у которых дата есть только в первой строке.
            bool isInTimeRange = false;
            string line;

            // Читаем файл построчно. ReadLineAsync не загружает весь файл в память,
            // а берет данные из буфера размером BufferSize.
            while ((line = await reader.ReadLineAsync()) != null)
            {
                // Пытаемся найти дату в начале строки
                var match = WebDateRegex.Match(line);
                if (match.Success)
                {
                    // Если дата найдена, парсим её и обновляем флаг
                    if (DateTime.TryParse(match.Groups[1].Value, out DateTime dt))
                    {
                        isInTimeRange = (dt >= startTime && dt <= endTime);
                    }
                }

                // Если мы "внутри" нужного временного диапазона
                if (isInTimeRange)
                {
                    // Дополнительно проверяем текстовую маску (если она задана)
                    if (string.IsNullOrEmpty(searchMask) || line.Contains(searchMask))
                    {
                        // Записываем строку в результирующий файл
                        await writer.WriteLineAsync(line);
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Обработка логов App-сервера (формат StorageServer).
        /// Особенность: дата идет в КОНЦЕ логического блока (одна ошибка = 10-20 строк).
        /// </summary>
        private async Task<long> ProcessAppLogAsync(StreamReader reader, StreamWriter writer,
            DateTime startTime, DateTime endTime, string searchMask)
        {
            long count = 0;
            // Буфер для накопления строк текущего логического блока.
            // Так как дата приходит в конце, мы не можем сразу решить — писать строку или нет.
            // Поэтому копируем все строки блока в StringBuilder.
            var blockBuffer = new StringBuilder();
            int linesInBlock = 0;
            string line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                // Добавляем строку в буфер текущего блока
                blockBuffer.AppendLine(line);
                linesInBlock++;

                // Ищем строку с датой — это признак конца логического блока
                var match = AppDateRegex.Match(line);
                if (match.Success)
                {
                    // Дата найдена — блок завершен, можно принимать решение
                    if (DateTime.TryParse(match.Groups[1].Value, out DateTime dt))
                    {
                        // Проверяем, попадает ли время блока в заданный диапазон
                        if (dt >= startTime && dt <= endTime)
                        {
                            string blockText = blockBuffer.ToString();

                            // Проверяем текстовую маску по всему блоку целиком
                            if (string.IsNullOrEmpty(searchMask) || blockText.Contains(searchMask))
                            {
                                // Выгружаем весь блок в результирующий файл
                                await writer.WriteAsync(blockText);
                                count += linesInBlock;
                            }
                        }
                    }
                    // Блок обработан — очищаем буфер для следующей записи
                    blockBuffer.Clear();
                    linesInBlock = 0;
                }
            }
            return count;
        }
    }
}