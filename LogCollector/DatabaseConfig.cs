namespace LogCollectorApp.Configuration
{
    /// <summary>
    /// Настройки подключения к базе данных PostgreSQL
    /// </summary>
    public static class DatabaseConfig
    {
        /// <summary>
        /// Строка подключения к БД logs_collecting в схеме pm02
        /// ВАЖНО: Замените значения на реальные данные сервера!
        /// </summary>
        public static string ConnectionString { get; set; } = 
            "Host=localhost;Port=5432;Database=postgres;" +
            "Username=postgres;Password=wasdswad;" +
            "SearchPath=pm02";  // ← Явно указываем схему pm02

        /// <summary>
        /// Метод для обновления строки подключения (например, из настроек приложения)
        /// </summary>
        public static void UpdateConnectionString(string host, int port, string database, 
            string username, string password)
        {
            ConnectionString = $"Host={host};Port={port};Database={database};" +
                             $"Username={username};Password={password};" +
                             "SearchPath=pm02";
        }
    }
}