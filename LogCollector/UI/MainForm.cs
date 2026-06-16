using LogCollector.BLL;
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
using static LogCollector.BLL.LogCollectionService;

namespace LogCollector.UI
{
    public partial class MainForm : Form
    {
        private readonly LogCollectionService _logService;
        private CancellationTokenSource _cts;

        public MainForm()
        {
            InitializeComponent();

            var sshHandler = new SshFileHandler();
            _logService = new LogCollectionService(sshHandler);
        }

        private async void btnStartCollection_Click(object sender, EventArgs e)
        {
            btnStartCollection.Enabled = false;
            listBoxLog.Items.Clear();
            lblStatus.Text = "Выполняется...";

            _cts = new CancellationTokenSource();

            try
            {
                var testServer = new Server
                {
                    Id = 99,
                    HostName = "DockerTestServer",
                    IpAddress = "127.0.0.1", 
                    Port = 2222,             
                    Login = "testuser",
                    Password = "testpassword"
                };

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string tempDir = Path.Combine(baseDir, "TempLogs");
                string outputDir = Path.Combine(baseDir, "OutputLogs");

                Directory.CreateDirectory(tempDir);
                Directory.CreateDirectory(outputDir);

                var progress = new Progress<string>(message =>
                {
                    listBoxLog.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                    listBoxLog.TopIndex = listBoxLog.Items.Count - 1;
                });

                var result = await _logService.CollectLogsAsync(
                    server: testServer,
                    startDate: DateTime.Now.AddDays(-1),
                    endDate: DateTime.Now,
                    tempDirectory: tempDir,
                    outputDirectory: outputDir,
                    progress: progress,
                    cancellationToken: _cts.Token);

                lblStatus.Text = $"Статус: {result.Status}";
                if (result.Status == CollectionStatus.Success)
                {
                    lblStatus.ForeColor = System.Drawing.Color.Green;
                    listBoxLog.Items.Add($"✅ Успех! Файл сохранен: {result.ResultFilePath}");
                }
                else
                {
                    lblStatus.ForeColor = System.Drawing.Color.Red;
                    listBoxLog.Items.Add($"❌ {result.Message}");
                }
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = "Отменено";
                listBoxLog.Items.Add("⚠️ Операция отменена пользователем.");
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Ошибка";
                lblStatus.ForeColor = System.Drawing.Color.Red;
                listBoxLog.Items.Add($"💥 Критическая ошибка: {ex.Message}");
            }
            finally
            {
                btnStartCollection.Enabled = true;
                _cts?.Dispose();
            }
        }

        private void listBoxLog_SelectedIndexChanged(object sender, EventArgs e)
        {
            
        }

        private void lblStatus_Click(object sender, EventArgs e)
        {

        }
    }
}
