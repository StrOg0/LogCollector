namespace LogCollectorApp.Models
{
    public class ArchiveProgressInfo
    {
        public string Stage { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int Percent { get; set; }
    }
}