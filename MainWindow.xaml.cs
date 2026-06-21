using LogCollectorApp.Data;
using LogCollectorApp.Data.Repositories;
using LogCollectorApp.Models;
using LogCollectorApp.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LogCollectorApp
{
    public partial class MainWindow : Window
    {
        private IServerService? _serverService;
        private List<ServerGroup>? _groups;
        private List<Server>? _currentServers;

        public MainWindow()
        {
            InitializeComponent();
            InitializeDependencies();
            LoadGroups();
        }

        private void InitializeDependencies()
        {
            var dbContext = new AppDbContext();
            var serverRepository = new ServerRepository(dbContext);
            _serverService = new ServerService(serverRepository);
        }

        private async void LoadGroups()
        {
            try
            {
                _groups = await _serverService!.GetAllGroupsAsync();
                cmbGroup.ItemsSource = _groups;
                cmbGroup.DisplayMemberPath = "Name";
                cmbGroup.SelectedValuePath = "Id";
                
                txtStatus.Text = $"Загружено групп: {_groups.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки групп: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CmbGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbGroup.SelectedItem == null) return;

            var selectedGroup = cmbGroup.SelectedItem as ServerGroup;
            if (selectedGroup == null) return;

            try
            {
                _currentServers = await _serverService!.GetServersByGroupAsync(selectedGroup.Id);
                
                dgServers.ItemsSource = _currentServers;
                
                txtServerCount.Text = $"Всего серверов: {_currentServers.Count}";
                UpdateSelectedCount();
                btnSearch.IsEnabled = _currentServers.Count > 0;
                
                txtStatus.Text = $"Выбрана группа: {selectedGroup.Name}. Отметьте серверы для сбора логов.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки серверов: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSelectedCount()
        {
            if (_currentServers == null) return;
            
            int selectedCount = _currentServers.Count(s => s.IsSelected);
            txtSelectedCount.Text = $"Выбрано серверов: {selectedCount}";
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
{
    // 🔥 Создаем файл для логов
    string logFile = Path.Combine(Directory.GetCurrentDirectory(), $"debug_{DateTime.Now:yyyyMMdd_HHmmss}.log");
    File.AppendAllText(logFile, $"\n========== НАЧАЛО СБОРА {DateTime.Now} ==========\n");

    try
    {
        string startInput = txtDateTimeStart.Text.Trim();
        string endInput = txtDateTimeEnd.Text.Trim();
        
        File.AppendAllText(logFile, $"Ввод: {startInput} - {endInput}\n");

        if (!DateTime.TryParseExact(startInput, "dd.MM.yyyy HH:mm", 
            System.Globalization.CultureInfo.InvariantCulture, 
            System.Globalization.DateTimeStyles.None, out DateTime startDate))
        {
            File.AppendAllText(logFile, "Ошибка парсинга начальной даты\n");
            MessageBox.Show("Неверный формат начального времени.", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!DateTime.TryParseExact(endInput, "dd.MM.yyyy HH:mm", 
            System.Globalization.CultureInfo.InvariantCulture, 
            System.Globalization.DateTimeStyles.None, out DateTime endDate))
        {
            File.AppendAllText(logFile, "Ошибка парсинга конечной даты\n");
            MessageBox.Show("Неверный формат конечного времени.", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        File.AppendAllText(logFile, $"Распарсено: {startDate} - {endDate}\n");

        if (endDate <= startDate)
        {
            File.AppendAllText(logFile, "Конечная дата меньше начальной\n");
            MessageBox.Show("Конечное время должно быть больше начального!", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedServers = _currentServers?.Where(s => s.IsSelected).ToList();
        
        File.AppendAllText(logFile, $"Выбрано серверов: {selectedServers?.Count ?? 0}\n");

        if (selectedServers == null || selectedServers.Count == 0)
        {
            File.AppendAllText(logFile, "Ни один сервер не выбран\n");
            MessageBox.Show("Выберите хотя бы один сервер для сбора логов!", "Внимание", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedGroup = cmbGroup.SelectedItem as ServerGroup;
        if (selectedGroup == null)
        {
            File.AppendAllText(logFile, "Группа не выбрана\n");
            return;
        }

        File.AppendAllText(logFile, $"Группа: {selectedGroup.Name}\n");

        btnSearch.IsEnabled = false;
        txtStatus.Text = $"Начинаем сбор логов с {startDate:dd.MM.yyyy HH:mm} по {endDate:dd.MM.yyyy HH:mm}...";

        try
        {
            string outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Output");
            Directory.CreateDirectory(outputDirectory);

            string testLogsPath = Path.Combine(Directory.GetCurrentDirectory(), "TestLogs");
            
            File.AppendAllText(logFile, $"TestLogs путь: {testLogsPath}\n");
            File.AppendAllText(logFile, $"TestLogs существует: {Directory.Exists(testLogsPath)}\n");

            if (!Directory.Exists(testLogsPath))
            {
                File.AppendAllText(logFile, "Папка TestLogs не найдена\n");
                MessageBox.Show($"Папка TestLogs не найдена:\n{testLogsPath}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Показываем содержимое TestLogs
            var testFiles = Directory.GetFiles(testLogsPath, "*", SearchOption.AllDirectories);
            File.AppendAllText(logFile, $"Файлов в TestLogs: {testFiles.Length}\n");
            foreach (var f in testFiles)
            {
                File.AppendAllText(logFile, $"  - {Path.GetFileName(f)}\n");
            }

            // Используем заглушку для тестирования
            var mockSshHandler = new MockSshFileHandler(testLogsPath);
            var logCollectionService = new LogCollectionService(mockSshHandler);

            var progress = new Progress<string>(message =>
            {
                txtStatus.Text = message;
                File.AppendAllText(logFile, $"PROGRESS: {message}\n");
            });

            int successCount = 0;
            int errorCount = 0;
            var resultFiles = new List<string>();
            var errorMessages = new List<string>();

            foreach (var server in selectedServers)
            {
                try
                {
                    File.AppendAllText(logFile, $"\n=== Обработка сервера: {server.Name} ===\n");
                    txtStatus.Text = $"Обработка: {server.Name}...";

                    string tempDir = Path.Combine(Path.GetTempPath(), $"log_collector_{server.Id}_{DateTime.Now:yyyyMMdd_HHmmss}");
                    Directory.CreateDirectory(tempDir);

                    File.AppendAllText(logFile, $"Temp dir: {tempDir}\n");

                    var result = await logCollectionService.CollectLogsAsync(
                        server,
                        startDate,
                        endDate,
                        tempDir,
                        outputDirectory,
                        progress,
                        CancellationToken.None);

                    File.AppendAllText(logFile, $"Результат: {result.Status} - {result.Message}\n");

                    if (result.Status == CollectionStatus.Success)
                    {
                        successCount++;
                        if (!string.IsNullOrEmpty(result.ResultFilePath))
                        {
                            resultFiles.Add(result.ResultFilePath);
                        }
                        txtStatus.Text = $"✓ {server.Name}: {result.Message}";
                    }
                    else
                    {
                        errorCount++;
                        string errorMsg = $"{server.Name}: {result.Message}";
                        errorMessages.Add(errorMsg);
                        txtStatus.Text = $"✗ {errorMsg}";
                    }

                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    string errorMsg = $"{server.Name}: {ex.Message}";
                    errorMessages.Add(errorMsg);
                    txtStatus.Text = $"Ошибка {server.Name}: {ex.Message}";
                    
                    File.AppendAllText(logFile, $"EXCEPTION: {ex.Message}\n");
                    File.AppendAllText(logFile, $"STACK TRACE: {ex.StackTrace}\n");
                    
                    if (ex.InnerException != null)
                    {
                        errorMessages.Add($"Inner exception: {ex.InnerException.Message}");
                        File.AppendAllText(logFile, $"INNER: {ex.InnerException.Message}\n");
                    }
                }
            }

            txtStatus.Text = $"Завершено! Успешно: {successCount}, Ошибок: {errorCount}";
            
            string message = $"Сбор логов завершен!\n\n" +
                          $"Период: {startDate:dd.MM.yyyy HH:mm} - {endDate:dd.MM.yyyy HH:mm}\n" +
                          $"Успешно: {successCount}\n" +
                          $"Ошибок: {errorCount}";
            
            if (errorMessages.Count > 0)
            {
                message += "\n\nДетали ошибок:\n" + string.Join("\n\n", errorMessages);
            }
            
            if (resultFiles.Count > 0)
            {
                message += "\n\nСозданные файлы:\n" + string.Join("\n", resultFiles.Select(f => Path.GetFileName(f)));
            }

            message += $"\n\nРезультаты в папке:\n{outputDirectory}";
            message += $"\n\nЛог отладки:\n{logFile}";

            MessageBox.Show(message, "Результат", MessageBoxButton.OK, MessageBoxImage.Information);

            if (Directory.Exists(outputDirectory))
            {
                System.Diagnostics.Process.Start("explorer.exe", outputDirectory);
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText(logFile, $"CRITICAL ERROR: {ex.Message}\n");
            File.AppendAllText(logFile, $"STACK: {ex.StackTrace}\n");
            
            MessageBox.Show($"Критическая ошибка: {ex.Message}\n\nЛог: {logFile}", 
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btnSearch.IsEnabled = true;
        }
    }
    finally
    {
        File.AppendAllText(logFile, $"========== КОНЕЦ {DateTime.Now} ==========\n\n");
    }
}
    }
}