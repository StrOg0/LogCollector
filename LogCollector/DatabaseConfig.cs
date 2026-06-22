namespace LogCollectorApp.Configuration
{
    // Настройки подключения к базе данных PostgreSQL
    public static class DatabaseConfig
    {
        // Строка подключения к БД logs_collecting
        public static string ConnectionString { get; set; } = 
            "Host=localhost;Port=5432;Database=postgres;" +
            "Username=postgres;Password=123;" +
            "SearchPath=log_collector";  // Явно указываем схему 

        // Метод для обновления строки подключения (например, из настроек приложения)
        public static void UpdateConnectionString(string host, int port, string database, 
            string username, string password)
        {
            ConnectionString = $"Host={host};Port={port};Database={database};" +
                             $"Username={username};Password={password};" +
                             "SearchPath=log_collector";
        }
    }
}