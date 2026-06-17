namespace LogCollector.Models;

public class Server
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public bool IsActive { get; set; } = true;
}