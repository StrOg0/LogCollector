namespace LogCollectorApp.Models;

public class ProcessedLogInfo
{
    public string ServerIp { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string TempFilePath { get; set; } = string.Empty;
    public DateTime LogDate { get; set; }
}
