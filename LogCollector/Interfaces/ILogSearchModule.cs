using LogCollector.BLL;

namespace LogCollector.Interfaces
{
    // Интерфейс модуля потокового поиска и фильтрации логов.
    public interface ILogSearchModule
    {
        // Выполняет потоковый поиск записей в лог-файле по временному диапазону и текстовой маске.
        // <param name="inputFilePath">Путь к исходному лог-файлу.</param>
        // <param name="outputFilePath">Путь для сохранения отфильтрованного результата.</param>
        // <param name="startTime">Начало временного периода.</param>
        // <param name="endTime">Конец временного периода.</param>
        // <param name="searchMask">Текстовая маска для поиска (null или "" — без фильтра по тексту).</param>
        // <param name="logFormat">Формат лог-файла (Web или App).</param>
        // <param name="append">Если true — дописывает в существующий файл, иначе перезаписывает.</param>
        // <returns>Количество найденных строк.</returns>
        Task<long> SearchLogsAsync(
            string inputFilePath,
            string outputFilePath,
            DateTime startTime,
            DateTime endTime,
            string searchMask,
            LogFormatType logFormat,
            bool append = false);
    }
}