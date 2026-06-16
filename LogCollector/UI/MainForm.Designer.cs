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
            btnTestCollection = new Button();
            SuspendLayout();
            // 
            // btnTestCollection
            // 
            btnTestCollection.Location = new Point(474, 300);
            btnTestCollection.Name = "btnTestCollection";
            btnTestCollection.Size = new Size(183, 29);
            btnTestCollection.TabIndex = 0;
            btnTestCollection.Text = "btnTestCollection";
            btnTestCollection.UseVisualStyleBackColor = true;
            btnTestCollection.Click += btnTestCollection_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(btnTestCollection);
            Name = "MainForm";
            Text = "MainForm";
            ResumeLayout(false);
        }

        #endregion

        private Button btnTestCollection;
    }
}