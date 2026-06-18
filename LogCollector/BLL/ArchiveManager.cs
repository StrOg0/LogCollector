using System.IO.Compression;
using LogCollector.Interfaces;

namespace LogCollector.BLL
{
    public class ArchiveManager : IArchiveManager
    {
        public List<string> ExtractArchives(string sourceArchivePath, string targetDirectory)
        {
            if (!File.Exists(sourceArchivePath))
                throw new FileNotFoundException($"Архив не найден: {sourceArchivePath}");

            Directory.CreateDirectory(targetDirectory);
            var extractedLogFiles = new List<string>();
            var archivesToProcess = new Queue<string>();

            archivesToProcess.Enqueue(sourceArchivePath);

            // Используем очередь вместо рекурсии, чтобы избежать переполнения стека 
            // и наглядно контролировать процесс распаковки вложенных архивов.
            while (archivesToProcess.Count > 0)
            {
                string currentArchive = archivesToProcess.Dequeue();
                string currentExtractDir = Path.Combine(targetDirectory, Path.GetFileNameWithoutExtension(currentArchive));

                // Если папка уже существует (например, при повторной обработке), добавляем GUID
                if (Directory.Exists(currentExtractDir))
                    currentExtractDir += $"_{Guid.NewGuid():N}";

                Directory.CreateDirectory(currentExtractDir);

                try
                {
                    ZipFile.ExtractToDirectory(currentArchive, currentExtractDir);
                }
                catch (Exception ex)
                {
                    // Логируем ошибку, но не прерываем работу, если архив битый
                    // Core модуль должен будет обработать отсутствие логов для этого сервера
                    throw new InvalidDataException($"Ошибка распаковки архива {currentArchive}: {ex.Message}", ex);
                }

                // Ищем логи и вложенные архивы
                foreach (var file in Directory.GetFiles(currentExtractDir, "*.*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();

                    if (ext == ".log")
                    {
                        extractedLogFiles.Add(file);
                    }
                    else if (ext == ".zip") // Если внутри есть еще архивы
                    {
                        archivesToProcess.Enqueue(file);
                    }
                }
            }

            return extractedLogFiles;
        }

        public string CreateResultArchive(string destinationArchivePath, Dictionary<string, string> logsByServer)
        {
            if (logsByServer == null || !logsByServer.Any())
                throw new ArgumentException("Нет данных для упаковки в итоговый архив.");

            // Удаляем файл, если он случайно остался от прошлых запусков
            if (File.Exists(destinationArchivePath))
                File.Delete(destinationArchivePath);

            using (ZipArchive archive = ZipFile.Open(destinationArchivePath, ZipArchiveMode.Create))
            {
                foreach (var kvp in logsByServer)
                {
                    string serverIp = kvp.Key;
                    string tempLogPath = kvp.Value;

                    if (File.Exists(tempLogPath))
                    {
                        // Формируем имя файла внутри архива строго по ТЗ: {IP-адрес}.log
                        string entryName = $"{serverIp}.log";

                        archive.CreateEntryFromFile(tempLogPath, entryName, CompressionLevel.Optimal);
                    }
                }
            }

            return destinationArchivePath;
        }
    }
}