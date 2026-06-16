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
            btnStartCollection = new Button();
            listBoxLog = new ListBox();
            lblStatus = new Label();
            SuspendLayout();
            // 
            // btnStartCollection
            // 
            btnStartCollection.Location = new Point(504, 155);
            btnStartCollection.Name = "btnStartCollection";
            btnStartCollection.Size = new Size(183, 29);
            btnStartCollection.TabIndex = 0;
            btnStartCollection.Text = "btnStartCollection";
            btnStartCollection.UseVisualStyleBackColor = true;
            btnStartCollection.Click += btnStartCollection_Click;
            // 
            // listBoxLog
            // 
            listBoxLog.FormattingEnabled = true;
            listBoxLog.Location = new Point(80, 80);
            listBoxLog.Name = "listBoxLog";
            listBoxLog.Size = new Size(298, 104);
            listBoxLog.TabIndex = 1;
            listBoxLog.SelectedIndexChanged += listBoxLog_SelectedIndexChanged;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(568, 80);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(66, 20);
            lblStatus.TabIndex = 2;
            lblStatus.Text = "lblStatus";
            lblStatus.Click += lblStatus_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(lblStatus);
            Controls.Add(listBoxLog);
            Controls.Add(btnStartCollection);
            Name = "MainForm";
            Text = "MainForm";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnStartCollection;
        private ListBox listBoxLog;
        private Label lblStatus;
    }
}