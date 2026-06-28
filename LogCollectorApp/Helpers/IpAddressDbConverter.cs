using System.Net;

namespace LogCollectorApp.Helpers;

public static class IpAddressDbConverter
{
    public static IPAddress ToDatabase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new FormatException("IP-адрес не может быть пустым");

        value = value.Trim();

        if (!IPAddress.TryParse(value, out var ipAddress))
            throw new FormatException($"Некорректный IP-адрес: {value}");

        return ipAddress;
    }

    public static string FromDatabase(IPAddress value)
    {
        return value.ToString();
    }

    public static bool IsValid(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && IPAddress.TryParse(value.Trim(), out _);
    }
}
