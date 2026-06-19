using LogCollectorApp.Data;
using LogCollectorApp.Data.Repositories;
using LogCollectorApp.Models;
using LogCollectorApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LogCollectorApp
{
    public partial class MainWindow : Window
    {
        private IServerService _serverService;
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
                _groups = await _serverService.GetAllGroupsAsync();
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
                _currentServers = await _serverService.GetServersByGroupAsync(selectedGroup.Id);
                
                dgServers.ItemsSource = _currentServers;
                
                txtServerCount.Text = $"Серверов в группе: {_currentServers.Count}";
                btnSearch.IsEnabled = _currentServers.Count > 0;
                
                txtStatus.Text = $"Выбрана группа: {selectedGroup.Name}. Введите дату и время для поиска.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки серверов: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
{
    string dateTimeInput = txtDateTime.Text.Trim();
    
    if (!DateTime.TryParseExact(dateTimeInput, "dd.MM.yyyy HH:mm", 
        System.Globalization.CultureInfo.InvariantCulture, 
        System.Globalization.DateTimeStyles.None, out DateTime searchDateTime))
    {
        MessageBox.Show("Неверный формат даты и времени.", "Ошибка", 
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    var selectedGroup = cmbGroup.SelectedItem as ServerGroup;
    if (selectedGroup == null || _currentServers == null) return;

    btnSearch.IsEnabled = false;
    
    try
    {
        string outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Output");
        Directory.CreateDirectory(outputDirectory);

        string testLogsPath = Path.Combine(Directory.GetCurrentDirectory(), "TestLogs");
        
        if (!Directory.Exists(testLogsPath))
        {
            MessageBox.Show($"Папка TestLogs не найдена:\n{testLogsPath}", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        txtStatus.Text = $"Начинаем сбор логов за {searchDateTime:dd.MM.yyyy HH:mm}...";

        // 🔥 ИСПОЛЬЗУЕМ ЗАГЛУШКУ ВМЕСТО РЕАЛЬНОГО SSH
        var mockSshHandler = new MockSshFileHandler(testLogsPath);
        var logCollectionService = new LogCollectionService(mockSshHandler);

        var progress = new Progress<string>(message =>
        {
            txtStatus.Text = message;
        });

        int successCount = 0;
        int errorCount = 0;

        foreach (var server in _currentServers)
        {
            try
            {
                txtStatus.Text = $"Обработка: {server.Name}...";

                string tempDir = Path.Combine(Path.GetTempPath(), $"log_collector_{DateTime.Now:yyyyMMdd_HHmmss}");
                Directory.CreateDirectory(tempDir);

                string logPath = GetLogPathForGroup(selectedGroup.Id);

                var result = await logCollectionService.CollectLogsAsync(
                    server,
                    searchDateTime,
                    searchDateTime.AddHours(1),
                    tempDir,
                    outputDirectory,
                    progress,
                    CancellationToken.None);

                if (result.Status == CollectionStatus.Success)
                {
                    successCount++;
                    txtStatus.Text = $"✓ {server.Name}: {result.Message}";
                }
                else
                {
                    errorCount++;
                    txtStatus.Text = $"✗ {server.Name}: {result.Message}";
                }

                // Очистка временной папки
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                txtStatus.Text = $"Ошибка {server.Name}: {ex.Message}";
            }
        }

        txtStatus.Text = $"Завершено! Успешно: {successCount}, Ошибок: {errorCount}";
        
        MessageBox.Show($"Сбор логов завершен!\n\n" +
                      $"Успешно: {successCount}\n" +
                      $"Ошибок: {errorCount}\n\n" +
                      $"Результаты в папке:\n{outputDirectory}", 
            "Результат", MessageBoxButton.OK, MessageBoxImage.Information);

        // Открываем папку с результатами
        if (Directory.Exists(outputDirectory))
        {
            System.Diagnostics.Process.Start("explorer.exe", outputDirectory);
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", 
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
    finally
    {
        btnSearch.IsEnabled = true;
    }
}

private string GetLogPathForGroup(long groupId)
{
    return groupId switch
    {
        1 => "/digdes/TK/dock/ddmwebapi_log",
        2 => "/var/log/digdes/sdu",
        3 => "/var/log/postgresql",
        _ => "/var/log/digdes/sdu"
    };
}
    }
}