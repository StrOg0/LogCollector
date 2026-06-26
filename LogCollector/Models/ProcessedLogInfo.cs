namespace LogCollector.Models
{
    // Модель, описывающая метаданные обработанного лог-файла для упаковки в итоговый архив.

    public class ProcessedLogInfo
    {
        public string ServerIp { get; set; }

        public string ServerName { get; set; }

        public string TempFilePath { get; set; }

        public DateTime LogDate { get; set; }
    }
}