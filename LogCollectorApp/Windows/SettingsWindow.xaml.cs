using LogCollectorApp.Services;
using System;
using System.Windows;

namespace LogCollectorApp.Windows;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        LoadCredentials();
    }

    private void LoadCredentials()
    {
        try
        {
            var settings = LocalSettingsManager.Load();
            txtUsername.Text = settings.SshUsername ?? string.Empty;
            txtPassword.Password = settings.SshPassword ?? string.Empty;
        }
        catch (Exception ex)
        {
            ShowError("Ошибка загрузки локальных настроек:\n" + ex.Message);
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string username = txtUsername.Text.Trim();
        string password = txtPassword.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show("Логин и пароль не могут быть пустыми", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var settings = LocalSettingsManager.Load();
            settings.SshUsername = username;
            settings.SshPassword = password;
            LocalSettingsManager.Save(settings);

            MessageBox.Show("SSH-настройки сохранены локально", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError("Ошибка сохранения локальных настроек:\n" + ex.Message);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static void ShowError(string message) => MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
}
