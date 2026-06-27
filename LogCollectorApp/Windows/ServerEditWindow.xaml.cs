using LogCollectorApp.Data;
using LogCollectorApp.Helpers;
using LogCollectorApp.Models;
using System;
using System.Windows;

namespace LogCollectorApp.Windows;

public partial class ServerEditWindow : Window
{
    private readonly Server? _server;
    private readonly long _groupId;
    public Server? EditedServer { get; private set; }

    public ServerEditWindow(Server? server, long groupId)
    {
        InitializeComponent();
        _server = server;
        _groupId = groupId;

        if (server == null) return;
        txtName.Text = server.Name;
        txtIpAddress.Text = server.IpAddress;
        txtPort.Text = server.SshPort.ToString();
        chkIsActive.IsChecked = server.IsActive;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string name = txtName.Text.Trim();
        string ip = txtIpAddress.Text.Trim();

        if (!int.TryParse(txtPort.Text, out int port) || port is < 1 or > 65535)
        {
            ShowWarning("Неверный формат порта");
            return;
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ip))
        {
            ShowWarning("Название и IP-адрес обязательны");
            return;
        }

        if (!IpAddressDbConverter.IsValid(ip))
        {
            ShowWarning($"Некорректный IP-адрес: {ip}");
            return;
        }

        try
        {
            using var context = new AppDbContext();
            var server = _server == null ? new Server { GroupId = _groupId, CreatedAt = DateTime.UtcNow } : await context.Servers.FindAsync(_server.Id);

            if (server == null)
            {
                ShowError("Сервер не найден");
                return;
            }

            server.Name = name;
            server.IpAddress = ip;
            server.SshPort = port;
            server.IsActive = chkIsActive.IsChecked ?? true;

            if (_server == null) context.Servers.Add(server);
            await context.SaveChangesAsync();

            EditedServer = server;
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
