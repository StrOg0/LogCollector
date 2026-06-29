using LogCollectorApp.Data;
using LogCollectorApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Windows;

namespace LogCollectorApp.Windows;

public partial class GroupEditWindow : Window
{
    private const string DefaultEncoding = "UTF-8";

    private readonly ServerGroup? _group;
    public ServerGroup? EditedGroup { get; private set; }

    public GroupEditWindow(ServerGroup? group)
    {
        InitializeComponent();
        _group = group;

        txtEncoding.Text = DefaultEncoding;

        if (group == null) return;

        txtName.Text = group.Name;
        txtDescription.Text = group.Description ?? string.Empty;
        chkIsActive.IsChecked = group.IsActive;

        LogSource? source = group.LogSource;
        txtLogPath.Text = source?.LogPath ?? GetDefaultLogPath(group.Name);
        txtArchivePath.Text = source?.ArchivePath ?? GetDefaultArchivePath(group.Name);
        txtFileMask.Text = source?.FileMask ?? GetDefaultFileMask(group.Name);
        txtSearchMask.Text = source?.SearchMask ?? GetDefaultSearchMask(group.Name);
        txtEncoding.Text = string.IsNullOrWhiteSpace(source?.Encoding) ? DefaultEncoding : source.Encoding;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string name = txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowWarning("Название группы обязательно");
            return;
        }

        string logPath = txtLogPath.Text.Trim();
        string archivePath = txtArchivePath.Text.Trim();
        string fileMask = txtFileMask.Text.Trim();
        string searchMask = txtSearchMask.Text.Trim();
        string encoding = txtEncoding.Text.Trim();

        bool hasLogSourceData = !string.IsNullOrWhiteSpace(logPath)
                                || !string.IsNullOrWhiteSpace(archivePath)
                                || !string.IsNullOrWhiteSpace(fileMask)
                                || !string.IsNullOrWhiteSpace(searchMask);

        if (hasLogSourceData && (string.IsNullOrWhiteSpace(logPath) || string.IsNullOrWhiteSpace(fileMask) || string.IsNullOrWhiteSpace(searchMask)))
        {
            ShowWarning("Для источника логов нужно заполнить путь к логам, маску файла и маску поиска.");
            return;
        }

        if (string.IsNullOrWhiteSpace(encoding)) encoding = DefaultEncoding;

        try
        {
            using var context = new AppDbContext();
            ServerGroup? group;

            if (_group == null)
            {
                group = new ServerGroup { CreatedAt = DateTime.UtcNow };
                context.ServerGroups.Add(group);
            }
            else
            {
                group = await context.ServerGroups
                    .Include(g => g.LogSource)
                    .FirstOrDefaultAsync(g => g.Id == _group.Id);
            }

            if (group == null)
            {
                ShowError("Группа не найдена");
                return;
            }

            group.Name = name;
            group.Description = txtDescription.Text.Trim();
            group.IsActive = chkIsActive.IsChecked ?? true;

            if (hasLogSourceData)
            {
                group.LogSource ??= new LogSource { Group = group };
                group.LogSource.LogPath = logPath;
                group.LogSource.ArchivePath = string.IsNullOrWhiteSpace(archivePath) ? null : archivePath;
                group.LogSource.FileMask = fileMask;
                group.LogSource.SearchMask = searchMask;
                group.LogSource.Encoding = encoding;
            }

            await context.SaveChangesAsync();

            EditedGroup = group;
            DialogResult = true;
            Close();
        }
        catch (Exception ex) { ShowError($"Ошибка сохранения: {ex.Message}"); }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static string GetDefaultLogPath(string groupName) => IsWebGroup(groupName)
        ? "/digdes/TK/dock/ddmwebapi_log"
        : "/var/log/digdes/sdu";

    private static string GetDefaultArchivePath(string groupName) => IsWebGroup(groupName)
        ? "/digdes/TK/dock/ddmwebapi_log/archive"
        : string.Empty;

    private static string GetDefaultFileMask(string groupName) => IsWebGroup(groupName)
        ? "DDM_Web.log"
        : "log {yyyy}Y{MM}M{dd}D";

    private static string GetDefaultSearchMask(string groupName) => IsWebGroup(groupName)
        ? "{yyyy-MM-dd} {HH}:{mm}:"
        : "DateTime={yyyy-MM-dd}T{HH}:{mm}:";

    private static bool IsWebGroup(string groupName)
    {
        string value = groupName.ToLowerInvariant();
        return value.Contains("web") || value.Contains("веб");
    }

    private static void ShowWarning(string message) => MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
    private static void ShowError(string message) => MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
}
