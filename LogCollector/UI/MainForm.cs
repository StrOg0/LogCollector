using LogCollector.BLL;
using LogCollectorApp.DAl;
using LogCollectorApp.Models;
using Org.BouncyCastle.Tls;
using static LogCollector.BLL.LogCollectionService;

namespace LogCollector.UI
{
    public partial class MainForm : Form
    {
        private readonly LogCollectionService _logService;
        private CancellationTokenSource _cts;

        private IDatabaseRepository? _databaseRepository;
        private List<ServerGroup>? _groups;
        private List<Server>? _currentServers;

        public MainForm()
        {
            InitializeComponent();

            var sshHandler = new SshFileHandler();
            var archiveManager = new ArchiveManager();
            var logSearch = new LogSearchModule();
            _logService = new LogCollectionService(sshHandler, archiveManager, logSearch);

            _databaseRepository = new DatabaseRepository();

            LoadGroups();

            cmbGroup.SelectedIndexChanged += cmbGroup_SelectedIndexChanged;

            SetupDataGridViewColumns();

            dbServers.CurrentCellDirtyStateChanged += (s, ev) =>
            {
                // Если изменилась ячейка с чекбоксом, сразу фиксируем изменения
                if (dbServers.IsCurrentCellDirty && dbServers.CurrentCell is DataGridViewCheckBoxCell)
                {
                    dbServers.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
        }

        private void SetupDataGridViewColumns()
        {
            dbServers.AutoGenerateColumns = false;
            dbServers.Columns.Clear();

            // 1. Колонка с чекбоксом "Выбрать"
            var colSelect = new DataGridViewCheckBoxColumn
            {
                DataPropertyName = "IsSelected",
                HeaderText = "Выбрать",
                Width = 80,
                FalseValue = false,
                TrueValue = true
            };
            dbServers.Columns.Add(colSelect);

            // 2. Колонка "Имя сервера"
            dbServers.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Name",
                HeaderText = "Имя сервера",
                Width = 200,
                ReadOnly = true
            });

            // 3. Колонка "IP-адрес"
            dbServers.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "IpAddress",
                HeaderText = "IP-адрес",
                Width = 150,
                ReadOnly = true
            });

            // 4. Колонка "Порт SSH"
            dbServers.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "SshPort",
                HeaderText = "Порт SSH",
                Width = 100,
                ReadOnly = true
            });

            // 5. Колонка "Активен"
            var colActive = new DataGridViewCheckBoxColumn
            {
                DataPropertyName = "IsActive",
                HeaderText = "Активен",
                Width = 80,
                ReadOnly = true
            };
            dbServers.Columns.Add(colActive);
        }

        private async void LoadGroups()
        {
            try
            {
                _groups = await _databaseRepository!.GetAllGroupsAsync();
                cmbGroup.DataSource = _groups;
                cmbGroup.DisplayMember = "Name";
                cmbGroup.ValueMember = "Id";

                txtStatus.Text = $"Загружено групп: {_groups.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки групп: {ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool _isLoadingServers = false; // Флаг для предотвращения параллельных вызовов

        private async void cmbGroup_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoadingServers) return;

            if (cmbGroup.SelectedItem == null) return;

            var selectedGroup = cmbGroup.SelectedItem as ServerGroup;
            if (selectedGroup == null) return;

            try
            {
                _isLoadingServers = true;

                this.Cursor = Cursors.WaitCursor;

                _currentServers = await _databaseRepository!.GetServersByGroupAsync(selectedGroup.Id);

                dbServers.DataSource = _currentServers;

                txtStatus.Text = $"Выбрана группа: {selectedGroup.Name}. Отметьте серверы для сбора логов.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки серверов: {ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isLoadingServers = false;
                this.Cursor = Cursors.Default;
            }
        }

        private async void btnStartCollection_Click(object sender, EventArgs e)
        {
            btnStartCollection.Enabled = false;
            listBoxLog.Items.Clear();
            lblStatus.Text = "Выполняется...";

            _cts = new CancellationTokenSource();

            //Парсинг даты
            string startInput = txtDateTimeStart.Text.Trim();
            string endInput = txtDateTimeEnd.Text.Trim();

            if (!DateTime.TryParseExact(startInput, "dd.MM.yyyy HH:mm",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime startDate))
            {
                MessageBox.Show("Неверный формат начального времени.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!DateTime.TryParseExact(endInput, "dd.MM.yyyy HH:mm",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime endDate))
            {
                MessageBox.Show("Неверный формат конечного времени.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (endDate <= startDate)
            {
                MessageBox.Show("Конечное время должно быть больше начального!", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            //Получение серверов и группы
            var selectedServers = _currentServers?.Where(s => s.IsSelected).ToList();

            if (selectedServers == null || selectedServers.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы один сервер для сбора логов!", "Внимание",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedGroup = cmbGroup.SelectedItem as ServerGroup;
            if (selectedGroup == null)
            {
                return;
            }

            txtStatus.Text = $"Начинаем сбор логов с {startDate:dd.MM.yyyy HH:mm} по {endDate:dd.MM.yyyy HH:mm}...";

            try
            {
                //var testServer = new Server
                //{
                //    Id = 99,
                //    GroupId = selectedGroup.Id,
                //    Name = "DockerTestServer",
                //    IpAddress = "127.0.0.1",
                //    SshPort = 2222,
                //    IsActive = true
                //};

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string outputDir = Path.Combine(baseDir, "OutputLogs");

                Directory.CreateDirectory(outputDir);

                var progress = new Progress<string>(message =>
                {
                    listBoxLog.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                    listBoxLog.TopIndex = listBoxLog.Items.Count - 1;
                });

                int successCount = 0;
                int errorCount = 0;
                var resultFiles = new List<string>();
                var errorMessages = new List<string>();

                foreach (var server in selectedServers)
                {
                    try
                    {
                        txtStatus.Text = $"Обработка: {server.Name}...";

                        string tempDir = Path.Combine(Path.GetTempPath(), $"log_collector_{server.Id}_{DateTime.Now:yyyyMMdd_HHmmss}");
                        Directory.CreateDirectory(tempDir);

                        var result = await _logService.CollectLogsAsync(
                            server: server,
                            startDate: startDate,
                            endDate: endDate,
                            tempDirectory: tempDir,
                            outputDirectory: outputDir,
                            progress: progress,
                            cancellationToken: _cts.Token);

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


                        if (ex.InnerException != null)
                        {
                            errorMessages.Add($"Inner exception: {ex.InnerException.Message}");
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

                message += $"\n\nРезультаты в папке:\n{outputDir}";

                MessageBox.Show(message, "Результат", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (Directory.Exists(outputDir))
                {
                    System.Diagnostics.Process.Start("explorer.exe", outputDir);
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

        private void toolTip1_Popup(object sender, PopupEventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}
