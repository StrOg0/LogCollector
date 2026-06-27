namespace LogCollectorApp.Configuration;

public static class DatabaseConfig
{
    public static string ConnectionString { get; set; } =
        "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=wasdswad;SearchPath=pm02";

    public static void UpdateConnectionString(string host, int port, string database, string username, string password)
    {
        ConnectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};SearchPath=pm02";
    }
}
