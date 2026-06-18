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

            while (archivesToProcess.Count > 0)
            {
                string currentArchive = archivesToProcess.Dequeue();
                string currentExtractDir = Path.Combine(targetDirectory, Path.GetFileNameWithoutExtension(currentArchive));

                if (Directory.Exists(currentExtractDir))
                    currentExtractDir += $"_{Guid.NewGuid():N}";

                Directory.CreateDirectory(currentExtractDir);

                try
                {
                    ZipFile.ExtractToDirectory(currentArchive, currentExtractDir);
                }
                catch (Exception ex)
                {
                    
                    throw new InvalidDataException($"Ошибка распаковки архива {currentArchive}: {ex.Message}", ex);
                }

                foreach (var file in Directory.GetFiles(currentExtractDir, "*.*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();

                    if (ext == ".log")
                    {
                        extractedLogFiles.Add(file);
                    }
                    else if (ext == ".zip")
                    {
                        archivesToProcess.Enqueue(file);
                    }
                }
            }

            return extractedLogFiles;
        }

        
    }
}