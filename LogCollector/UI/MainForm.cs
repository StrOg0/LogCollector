using LogCollector.App.BLL;
using LogCollector.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static LogCollector.App.BLL.LogCollectionService;

namespace LogCollector.UI
{
    public partial class MainForm : Form
    {
        private readonly LogCollectionService _service;
        private CancellationTokenSource _cts;

        public MainForm(LogCollectionService service)
        {
            InitializeComponent();
            _service = service;
        }

        private async void btnTestCollection_Click(object sender, EventArgs e)
        {
            // Блокируем кнопку на время выполнения
            btnTestCollection.Enabled = false;
            _cts = new CancellationTokenSource();

            try
            {
                // Создаем тестовый сервер (в реальности будем брать из БД)
                var testServer = new Server
                {
                    Id = 1,
                    HostName = "TestServer",
                    IpAddress = "127.0.0.1",
                    Port = 22,
                    Login = "твой_юзер", // ← замени
                    Password = "твой_пароль", // ← замени
                    IsActive = true
                };

                // Период (пока хардкод)
                var startDate = DateTime.Now.AddDays(-1);
                var endDate = DateTime.Now;

                // Временные папки
                string tempDir = @"C:\temp_logs";
                string outputDir = @"C:\log_results";

                Directory.CreateDirectory(tempDir);
                Directory.CreateDirectory(outputDir);

                // Прогресс
                var progress = new Progress<string>(message =>
                {
                    Debug.WriteLine(message);
                    // Если есть TextBox или RichTextBox для логов, можно выводить туда:
                    // txtLog.AppendText(message + Environment.NewLine);
                });

                Debug.WriteLine("=== Начало теста сбора логов ===");

                // Запускаем сбор
                var result = await _service.CollectLogsAsync(
                    testServer,
                    startDate,
                    endDate,
                    tempDir,
                    outputDir,
                    progress,
                    _cts.Token);

                Debug.WriteLine($"=== Результат: {result.Status} ===");
                Debug.WriteLine($"Сообщение: {result.Message}");

                MessageBox.Show(
                    $"Статус: {result.Status}\n" +
                    $"Сообщение: {result.Message}\n" +
                    $"Файл: {result.ResultFilePath}",
                    "Результат сбора",
                    MessageBoxButtons.OK,
                    result.Status == CollectionStatus.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ОШИБКА: {ex.Message}");
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnTestCollection.Enabled = true;
                _cts?.Dispose();
            }
        }
    }
}
