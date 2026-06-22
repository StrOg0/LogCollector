namespace LogCollector.UI
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            btnStartCollection = new Button();
            listBoxLog = new ListBox();
            lblStatus = new Label();
            cmbGroup = new ComboBox();
            serverGroupLbl = new Label();
            txtDateTimeStart = new TextBox();
            startDateLbl = new Label();
            endDateLbl = new Label();
            txtDateTimeEnd = new TextBox();
            txtStatus = new Label();
            dbServers = new DataGridView();
            ((System.ComponentModel.ISupportInitialize)dbServers).BeginInit();
            SuspendLayout();
            // 
            // btnStartCollection
            // 
            btnStartCollection.Location = new Point(39, 226);
            btnStartCollection.Name = "btnStartCollection";
            btnStartCollection.Size = new Size(208, 29);
            btnStartCollection.TabIndex = 0;
            btnStartCollection.Text = "Начать сбор логов";
            btnStartCollection.UseVisualStyleBackColor = true;
            btnStartCollection.Click += btnStartCollection_Click;
            // 
            // listBoxLog
            // 
            listBoxLog.FormattingEnabled = true;
            listBoxLog.Location = new Point(319, 45);
            listBoxLog.Name = "listBoxLog";
            listBoxLog.Size = new Size(452, 104);
            listBoxLog.TabIndex = 1;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(695, 169);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(66, 20);
            lblStatus.TabIndex = 2;
            lblStatus.Text = "lblStatus";
            // 
            // cmbGroup
            // 
            cmbGroup.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbGroup.FormattingEnabled = true;
            cmbGroup.Location = new Point(40, 45);
            cmbGroup.Name = "cmbGroup";
            cmbGroup.Size = new Size(200, 28);
            cmbGroup.TabIndex = 3;
            cmbGroup.SelectedIndexChanged += cmbGroup_SelectedIndexChanged;
            // 
            // serverGroupLbl
            // 
            serverGroupLbl.AutoSize = true;
            serverGroupLbl.Location = new Point(40, 22);
            serverGroupLbl.Name = "serverGroupLbl";
            serverGroupLbl.Size = new Size(131, 20);
            serverGroupLbl.TabIndex = 4;
            serverGroupLbl.Text = "Группа серверов:";
            // 
            // txtDateTimeStart
            // 
            txtDateTimeStart.Location = new Point(40, 106);
            txtDateTimeStart.Name = "txtDateTimeStart";
            txtDateTimeStart.PlaceholderText = "ДД.ММ.ГГГГ ЧЧ:ММ";
            txtDateTimeStart.Size = new Size(207, 27);
            txtDateTimeStart.TabIndex = 5;
            // 
            // startDateLbl
            // 
            startDateLbl.AutoSize = true;
            startDateLbl.Location = new Point(40, 83);
            startDateLbl.Name = "startDateLbl";
            startDateLbl.Size = new Size(117, 20);
            startDateLbl.TabIndex = 6;
            startDateLbl.Text = "Начало поиска:";
            // 
            // endDateLbl
            // 
            endDateLbl.AutoSize = true;
            endDateLbl.Location = new Point(40, 146);
            endDateLbl.Name = "endDateLbl";
            endDateLbl.Size = new Size(109, 20);
            endDateLbl.TabIndex = 7;
            endDateLbl.Text = "Конец поиска:";
            // 
            // txtDateTimeEnd
            // 
            txtDateTimeEnd.Location = new Point(40, 169);
            txtDateTimeEnd.Name = "txtDateTimeEnd";
            txtDateTimeEnd.PlaceholderText = "ДД.ММ.ГГГГ ЧЧ:ММ";
            txtDateTimeEnd.Size = new Size(208, 27);
            txtDateTimeEnd.TabIndex = 8;
            // 
            // txtStatus
            // 
            txtStatus.AutoSize = true;
            txtStatus.Location = new Point(39, 552);
            txtStatus.Name = "txtStatus";
            txtStatus.Size = new Size(199, 20);
            txtStatus.TabIndex = 9;
            txtStatus.Text = "Выберите группу серверов";
            // 
            // dbServers
            // 
            dbServers.AllowUserToAddRows = false;
            dbServers.AllowUserToDeleteRows = false;
            dataGridViewCellStyle1.BackColor = Color.WhiteSmoke;
            dbServers.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dbServers.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dbServers.Location = new Point(40, 283);
            dbServers.Name = "dbServers";
            dbServers.RowHeadersWidth = 51;
            dbServers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dbServers.Size = new Size(665, 238);
            dbServers.TabIndex = 11;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(857, 581);
            Controls.Add(dbServers);
            Controls.Add(txtStatus);
            Controls.Add(txtDateTimeEnd);
            Controls.Add(endDateLbl);
            Controls.Add(startDateLbl);
            Controls.Add(txtDateTimeStart);
            Controls.Add(serverGroupLbl);
            Controls.Add(cmbGroup);
            Controls.Add(lblStatus);
            Controls.Add(listBoxLog);
            Controls.Add(btnStartCollection);
            Name = "MainForm";
            Text = "MainForm";
            ((System.ComponentModel.ISupportInitialize)dbServers).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnStartCollection;
        private ListBox listBoxLog;
        private Label lblStatus;
        private ComboBox cmbGroup;
        private Label serverGroupLbl;
        private TextBox txtDateTimeStart;
        private Label startDateLbl;
        private Label endDateLbl;
        private TextBox txtDateTimeEnd;
        private Label txtStatus;
        private DataGridView dbServers;
    }
}