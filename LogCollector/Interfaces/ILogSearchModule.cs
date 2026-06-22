using LogCollectorApp.Models;

namespace LogCollector.Interfaces
{
    public interface ILogSearchModule
    {
        // Выполняет потоковый поиск записей в лог-файле.
        Task<long> SearchLogsAsync(
            string inputFilePath,
            string outputFilePath,
            DateTime startTime,
            DateTime endTime,
            LogSource logSource,
            bool append = false);
    }
}