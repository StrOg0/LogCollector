using LogCollectorApp.Services;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace LogCollectorApp.Windows;

public partial class OutputPathWindow : Window
{
    public string OutputPath { get; private set; } = string.Empty;

    public OutputPathWindow()
    {
        InitializeComponent();
        txtPath.Text = LocalSettingsManager.Load().OutputPath ?? Path.Combine(Directory.GetCurrentDirectory(), "Output");
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Выберите папку для сохранения логов" };
        if (dialog.ShowDialog() == true) txtPath.Text = dialog.FolderName;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string path = txtPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show("Укажите путь к папке", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        OutputPath = path;
        var settings = LocalSettingsManager.Load();
        settings.OutputPath = path;
        LocalSettingsManager.Save(settings);
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
