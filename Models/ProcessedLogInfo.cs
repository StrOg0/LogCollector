namespace LogCollectorApp.Models
{
    public class ProcessedLogInfo
    {
        public string ServerIp { get; set; }
        public string ServerName { get; set; }
        public string TempFilePath { get; set; }
        public DateTime LogDate { get; set; }
    }
}