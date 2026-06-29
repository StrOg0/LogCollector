using LogCollectorApp.Data;
using LogCollectorApp.Data.Repositories;
using LogCollectorApp.Models;
using LogCollectorApp.Services;
using LogCollectorApp.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LogCollectorApp;

public partial class MainWindow : Window
{
    private IServerService? _serverService;
    private List<ServerGroup>? _groups;
    private List<Server>? _currentServers;
    private AppSettings _settings = new();
    private bool _suppressServerGridEvents;

    public MainWindow()
    {
        InitializeComponent();
        InitializeDependencies();
        _settings = LocalSettingsManager.Load();
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
        try { await LoadGroupsAsync(); }
        catch (Exception ex) { ShowError($"Ошибка загрузки групп: {ex.Message}"); }
    }

    private async void CmbGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbGroup.SelectedItem is not ServerGroup group) return;

        try
        {
            _currentServers = await _serverService!.GetServersByGroupAsync(group.Id);
            await SetServersGridSourceAsync(_currentServers);
            txtServerCount.Text = $"Всего серверов: {_currentServers.Count}";
            btnSearch.IsEnabled = _currentServers.Count > 0;
            UpdateSelectedCount();
            txtStatus.Text = $"Выбрана группа: {group.Name}. Отметьте серверы для сбора логов.";
        }
        catch (Exception ex) { ShowError($"Ошибка загрузки серверов: {ex.Message}"); }
    }

    private void UpdateSelectedCount()
    {
        txtSelectedCount.Text = $"Выбрано серверов: {_currentServers?.Count(s => s.IsSelected) ?? 0}";
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow { Owner = this }.ShowDialog();
    }

    private void BtnOutputPath_Click(object sender, RoutedEventArgs e)
    {
        var window = new OutputPathWindow { Owner = this };
        if (window.ShowDialog() != true) return;

        _settings = LocalSettingsManager.Load();
        txtStatus.Text = $"Папка для сохранения: {window.OutputPath}";
    }

    private async void BtnEditGroup_Click(object sender, RoutedEventArgs e)
    {
        if (cmbGroup.SelectedItem is not ServerGroup group)
        {
            ShowWarning("Выберите группу для редактирования");
            return;
        }

        long selectedGroupId = group.Id;
        var window = new GroupEditWindow(group) { Owner = this };
        if (window.ShowDialog() != true) return;

        await LoadGroupsAsync();
        cmbGroup.SelectedItem = _groups?.FirstOrDefault(g => g.Id == selectedGroupId);
        txtStatus.Text = "Группа обновлена";
    }

    private async void BtnAddGroup_Click(object sender, RoutedEventArgs e)
    {
        var window = new GroupEditWindow(null) { Owner = this };
        if (window.ShowDialog() != true) return;

        await LoadGroupsAsync();
        if (_groups?.Count > 0) cmbGroup.SelectedIndex = _groups.Count - 1;
        txtStatus.Text = "Группа добавлена";
    }

    private async void BtnEditServer_Click(object sender, RoutedEventArgs e)
    {
        if (dgServers.SelectedItem is not Server server)
        {
            ShowWarning("Выберите сервер для редактирования");
            return;
        }

        var window = new ServerEditWindow(server, server.GroupId) { Owner = this };
        if (window.ShowDialog() == true)
        {
            await ReloadServersAsync();
            txtStatus.Text = "Сервер обновлен";
        }
    }

    private async void BtnAddServer_Click(object sender, RoutedEventArgs e)
    {
        if (cmbGroup.SelectedItem is not ServerGroup group)
        {
            ShowWarning("Выберите группу");
            return;
        }

        var window = new ServerEditWindow(null, group.Id) { Owner = this };
        if (window.ShowDialog() == true)
        {
            await ReloadServersAsync();
            txtStatus.Text = "Сервер добавлен";
        }
    }

    private async Task LoadGroupsAsync()
    {
        _groups = await _serverService!.GetAllGroupsAsync();
        cmbGroup.ItemsSource = null;
        cmbGroup.DisplayMemberPath = "Name";
        cmbGroup.SelectedValuePath = "Id";
        cmbGroup.ItemsSource = _groups;
        cmbGroup.Items.Refresh();
        txtStatus.Text = $"Загружено групп: {_groups.Count}";
    }

    private async Task ReloadServersAsync()
    {
        if (cmbGroup.SelectedItem is not ServerGroup group) return;

        _currentServers = await _serverService!.GetServersByGroupAsync(group.Id);
        await SetServersGridSourceAsync(_currentServers);
        txtServerCount.Text = $"Всего серверов: {_currentServers.Count}";
        btnSearch.IsEnabled = _currentServers.Count > 0;
        UpdateSelectedCount();
    }

    private async Task SetServersGridSourceAsync(List<Server> servers)
    {
        _suppressServerGridEvents = true;
        dgServers.ItemsSource = null;
        dgServers.ItemsSource = servers;
        dgServers.Items.Refresh();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        _suppressServerGridEvents = false;
    }

    private void ChkServerSelected_Changed(object sender, RoutedEventArgs e) => UpdateSelectedCount();

    private async void ChkServerActive_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressServerGridEvents || !dgServers.IsKeyboardFocusWithin) return;
        if (sender is CheckBox { DataContext: Server server }) await SaveServerFromGridAsync(server);
    }

    private async void DgServers_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (_suppressServerGridEvents || e.EditAction != DataGridEditAction.Commit) return;

        string header = e.Column.Header?.ToString() ?? string.Empty;
        if (header is "Выбрать" or "Активен")
        {
            UpdateSelectedCount();
            return;
        }

        if (e.Row.Item is not Server server) return;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        await SaveServerFromGridAsync(server);
    }

    private async Task SaveServerFromGridAsync(Server server)
    {
        try
        {
            server.Name = server.Name?.Trim() ?? string.Empty;
            server.IpAddress = server.IpAddress?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(server.Name)) throw new ArgumentException("Название сервера не может быть пустым");
            if (string.IsNullOrWhiteSpace(server.IpAddress)) throw new ArgumentException("IP-адрес не может быть пустым");
            if (server.SshPort is < 1 or > 65535) throw new ArgumentException("Порт SSH должен быть в диапазоне от 1 до 65535");

            await _serverService!.UpdateServerAsync(server);
            txtStatus.Text = $"Сохранено: {server.Name}";
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось сохранить изменения сервера:\n{ex.Message}");
            await ReloadServersAsync();
        }
    }

    private async void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        _settings = LocalSettingsManager.Load();

        if (string.IsNullOrWhiteSpace(_settings.SshUsername) || string.IsNullOrWhiteSpace(_settings.SshPassword))
        {
            ShowWarning("Сначала настройте SSH-логин и пароль в настройках");
            BtnSettings_Click(sender, e);
            _settings = LocalSettingsManager.Load();

            if (string.IsNullOrWhiteSpace(_settings.SshUsername) || string.IsNullOrWhiteSpace(_settings.SshPassword))
                return;
        }

        if (!TryReadPeriod(out var startDate, out var endDate)) return;

        var selectedServers = _currentServers?.Where(s => s.IsSelected).ToList() ?? new List<Server>();
        if (selectedServers.Count == 0)
        {
            ShowWarning("Выберите хотя бы один сервер для сбора логов!");
            return;
        }

        foreach (var server in selectedServers)
        {
            server.SshUsername = _settings.SshUsername!;
            server.SshPassword = _settings.SshPassword!;
        }

        string outputDirectory = _settings.OutputPath ?? Path.Combine(Directory.GetCurrentDirectory(), "Output");

        btnSearch.IsEnabled = false;
        txtStatus.Text = $"Сбор логов: {startDate:dd.MM.yyyy HH:mm} - {endDate:dd.MM.yyyy HH:mm}";

        try
        {
            Directory.CreateDirectory(outputDirectory);
            using var ssh = new SshFileHandler();
            var archiveManager = new ArchiveManager();
            var service = new LogCollectionService(ssh, archiveManager);
            var progress = new Progress<string>(message => txtStatus.Text = message);

            int successCount = 0;
            int errorCount = 0;
            var resultFiles = new List<string>();
            var processedLogs = new List<ProcessedLogInfo>();
            var errors = new List<string>();

            foreach (var server in selectedServers)
            {
                string tempDir = Path.Combine(Path.GetTempPath(), $"log_collector_{server.Id}_{DateTime.Now:yyyyMMdd_HHmmss}");
                Directory.CreateDirectory(tempDir);

                try
                {
                    txtStatus.Text = $"Обработка: {server.Name}...";
                    var result = await service.CollectLogsAsync(server, startDate, endDate, tempDir, outputDirectory, progress, CancellationToken.None);

                    if (result.Status == CollectionStatus.Success)
                    {
                        successCount++;
                        if (!string.IsNullOrWhiteSpace(result.ResultFilePath))
                        {
                            resultFiles.Add(result.ResultFilePath);
                            processedLogs.Add(new ProcessedLogInfo
                            {
                                ServerIp = server.IpAddress,
                                ServerName = server.Name,
                                TempFilePath = result.ResultFilePath,
                                LogDate = startDate.Date
                            });
                        }
                    }
                    else
                    {
                        errorCount++;
                        errors.Add($"{server.Name}: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    errors.Add($"{server.Name}: {ex.Message}");
                }
                finally
                {
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                }
            }

            if (processedLogs.Count > 0)
            {
                string archivePath = Path.Combine(outputDirectory, $"logs_{startDate:yyyyMMdd_HHmmss}_{endDate:yyyyMMdd_HHmmss}.zip");
                archiveManager.CreateResultArchive(archivePath, processedLogs);
                resultFiles.Add(archivePath);
            }

            txtStatus.Text = $"Завершено. Успешно: {successCount}, ошибок: {errorCount}";
            MessageBox.Show(BuildResultMessage(startDate, endDate, successCount, errorCount, resultFiles, errors, outputDirectory),
                "Результат", MessageBoxButton.OK, MessageBoxImage.Information);

            if (Directory.Exists(outputDirectory)) Process.Start("explorer.exe", outputDirectory);
        }
        catch (Exception ex)
        {
            ShowError($"Критическая ошибка: {ex.Message}");
        }
        finally
        {
            btnSearch.IsEnabled = true;
        }
    }

    private bool TryReadPeriod(out DateTime start, out DateTime end)
    {
        const string format = "dd.MM.yyyy HH:mm";
        var culture = CultureInfo.InvariantCulture;
        start = end = default;

        if (!DateTime.TryParseExact(txtDateTimeStart.Text.Trim(), format, culture, DateTimeStyles.None, out start))
        {
            ShowWarning("Неверный формат начального времени.");
            return false;
        }

        if (!DateTime.TryParseExact(txtDateTimeEnd.Text.Trim(), format, culture, DateTimeStyles.None, out end))
        {
            ShowWarning("Неверный формат конечного времени.");
            return false;
        }

        if (end > start) return true;

        ShowWarning("Конечное время должно быть больше начального!");
        return false;
    }

    private static string BuildResultMessage(DateTime start, DateTime end, int success, int errorsCount,
        List<string> files, List<string> errors, string outputDirectory)
    {
        string message = $"Сбор логов завершен!\n\nПериод: {start:dd.MM.yyyy HH:mm} - {end:dd.MM.yyyy HH:mm}\n" +
                         $"Успешно: {success}\nОшибок: {errorsCount}";

        if (errors.Count > 0) message += "\n\nОшибки:\n" + string.Join("\n", errors);
        if (files.Count > 0) message += "\n\nСозданные файлы:\n" + string.Join("\n", files.Select(Path.GetFileName));

        return message + $"\n\nРезультаты в папке:\n{outputDirectory}";
    }

    private static void ShowWarning(string message) =>
        MessageBox.Show(message, "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);

    private static void ShowError(string message) =>
        MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
}
