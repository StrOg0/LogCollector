using LogCollectorApp.Data;
using LogCollectorApp.Models;
using System;
using System.Windows;

namespace LogCollectorApp.Windows;

public partial class GroupEditWindow : Window
{
    private readonly ServerGroup? _group;
    public ServerGroup? EditedGroup { get; private set; }

    public GroupEditWindow(ServerGroup? group)
    {
        InitializeComponent();
        _group = group;

        if (group == null) return;
        txtName.Text = group.Name;
        txtDescription.Text = group.Description ?? string.Empty;
        chkIsActive.IsChecked = group.IsActive;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string name = txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowWarning("Название группы обязательно");
            return;
        }

        try
        {
            using var context = new AppDbContext();
            var group = _group == null ? new ServerGroup { CreatedAt = DateTime.UtcNow } : await context.ServerGroups.FindAsync(_group.Id);

            if (group == null)
            {
                ShowError("Группа не найдена");
                return;
            }

            group.Name = name;
            group.Description = txtDescription.Text.Trim();
            group.IsActive = chkIsActive.IsChecked ?? true;

            if (_group == null) context.ServerGroups.Add(group);
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

    private static void ShowWarning(string message) => MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
    private static void ShowError(string message) => MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
}
