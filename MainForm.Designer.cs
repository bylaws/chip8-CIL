
namespace Chip8_CIL
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.openButton = new System.Windows.Forms.ToolStripButton();
            this.pauseButton = new System.Windows.Forms.ToolStripButton();
            this.toggleSpeedLimitButton = new System.Windows.Forms.ToolStripButton();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.framebufferPictureBox = new System.Windows.Forms.PictureBox();
            this.logLevelLabel = new System.Windows.Forms.Label();
            this.logLevelComboBox = new System.Windows.Forms.ComboBox();
            this.logTextBox = new System.Windows.Forms.TextBox();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.framebufferPictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openButton,
            this.pauseButton,
            this.toggleSpeedLimitButton});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(1347, 31);
            this.toolStrip1.TabIndex = 0;
            this.toolStrip1.Text = "toolStrip1";
            this.toolStrip1.ItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.toolStrip1_ItemClicked);
            // 
            // openButton
            // 
            this.openButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.openButton.Image = ((System.Drawing.Image)(resources.GetObject("openButton.Image")));
            this.openButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.openButton.Name = "openButton";
            this.openButton.Size = new System.Drawing.Size(28, 28);
            this.openButton.Text = "openButton";
            this.openButton.Click += new System.EventHandler(this.openButton_Click);
            // 
            // pauseButton
            // 
            this.pauseButton.CheckOnClick = true;
            this.pauseButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.pauseButton.Image = ((System.Drawing.Image)(resources.GetObject("pauseButton.Image")));
            this.pauseButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.pauseButton.Name = "pauseButton";
            this.pauseButton.Size = new System.Drawing.Size(28, 28);
            this.pauseButton.Text = "pauseButton";
            this.pauseButton.Click += new System.EventHandler(this.pauseButton_Click);
            // 
            // toggleSpeedLimitButton
            // 
            this.toggleSpeedLimitButton.CheckOnClick = true;
            this.toggleSpeedLimitButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toggleSpeedLimitButton.Image = ((System.Drawing.Image)(resources.GetObject("toggleSpeedLimitButton.Image")));
            this.toggleSpeedLimitButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toggleSpeedLimitButton.Name = "toggleSpeedLimitButton";
            this.toggleSpeedLimitButton.Size = new System.Drawing.Size(28, 28);
            this.toggleSpeedLimitButton.Text = "toggleSpeedLimitButton";
            this.toggleSpeedLimitButton.Click += new System.EventHandler(this.toggleSpeedLimitButton_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitContainer1.Cursor = System.Windows.Forms.Cursors.VSplit;
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 31);
            this.splitContainer1.Margin = new System.Windows.Forms.Padding(2);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.framebufferPictureBox);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.logLevelLabel);
            this.splitContainer1.Panel2.Controls.Add(this.logLevelComboBox);
            this.splitContainer1.Panel2.Controls.Add(this.logTextBox);
            this.splitContainer1.Panel2.Cursor = System.Windows.Forms.Cursors.Default;
            this.splitContainer1.Size = new System.Drawing.Size(1347, 544);
            this.splitContainer1.SplitterDistance = 668;
            this.splitContainer1.SplitterWidth = 3;
            this.splitContainer1.TabIndex = 1;
            // 
            // framebufferPictureBox
            // 
            this.framebufferPictureBox.Cursor = System.Windows.Forms.Cursors.Default;
            this.framebufferPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.framebufferPictureBox.Location = new System.Drawing.Point(0, 0);
            this.framebufferPictureBox.Margin = new System.Windows.Forms.Padding(2);
            this.framebufferPictureBox.Name = "framebufferPictureBox";
            this.framebufferPictureBox.Size = new System.Drawing.Size(666, 542);
            this.framebufferPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.framebufferPictureBox.TabIndex = 0;
            this.framebufferPictureBox.TabStop = false;
            this.framebufferPictureBox.Click += new System.EventHandler(this.framebufferPictureBox_Click);
            this.framebufferPictureBox.Paint += new System.Windows.Forms.PaintEventHandler(this.framebufferPictureBox_Paint);
            // 
            // logLevelLabel
            // 
            this.logLevelLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.logLevelLabel.Location = new System.Drawing.Point(462, 5);
            this.logLevelLabel.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.logLevelLabel.Name = "logLevelLabel";
            this.logLevelLabel.Size = new System.Drawing.Size(60, 15);
            this.logLevelLabel.TabIndex = 2;
            this.logLevelLabel.Text = "Log Level";
            // 
            // logLevelComboBox
            // 
            this.logLevelComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.logLevelComboBox.FormattingEnabled = true;
            this.logLevelComboBox.ImeMode = System.Windows.Forms.ImeMode.Off;
            this.logLevelComboBox.Location = new System.Drawing.Point(526, 4);
            this.logLevelComboBox.Margin = new System.Windows.Forms.Padding(2);
            this.logLevelComboBox.Name = "logLevelComboBox";
            this.logLevelComboBox.Size = new System.Drawing.Size(129, 23);
            this.logLevelComboBox.TabIndex = 0;
            this.logLevelComboBox.SelectedIndexChanged += new System.EventHandler(this.logLevelComboBox_SelectedIndexChanged);
            // 
            // logTextBox
            // 
            this.logTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logTextBox.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.logTextBox.Location = new System.Drawing.Point(0, 0);
            this.logTextBox.Margin = new System.Windows.Forms.Padding(2);
            this.logTextBox.MaxLength = 32767000;
            this.logTextBox.Multiline = true;
            this.logTextBox.Name = "logTextBox";
            this.logTextBox.ReadOnly = true;
            this.logTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.logTextBox.Size = new System.Drawing.Size(674, 542);
            this.logTextBox.TabIndex = 3;
            this.logTextBox.WordWrap = false;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1347, 575);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "MainForm";
            this.Text = "Form1";
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.framebufferPictureBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton openButton;
        private System.Windows.Forms.ToolStripButton pauseButton;
        private System.Windows.Forms.ToolStripButton toggleSpeedLimitButton;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.PictureBox framebufferPictureBox;
        private System.Windows.Forms.TextBox logTextBox;
        private System.Windows.Forms.Label logLevelLabel;
        private System.Windows.Forms.ComboBox logLevelComboBox;
    }
}

