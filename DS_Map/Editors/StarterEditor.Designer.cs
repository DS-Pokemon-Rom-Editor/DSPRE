namespace DSPRE.Editors
{
    partial class StarterEditor
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.labelStarter1 = new System.Windows.Forms.Label();
            this.comboBoxStarter1 = new System.Windows.Forms.ComboBox();
            this.pictureBoxStarter1 = new System.Windows.Forms.PictureBox();
            this.labelStarter2 = new System.Windows.Forms.Label();
            this.comboBoxStarter2 = new System.Windows.Forms.ComboBox();
            this.pictureBoxStarter2 = new System.Windows.Forms.PictureBox();
            this.labelStarter3 = new System.Windows.Forms.Label();
            this.comboBoxStarter3 = new System.Windows.Forms.ComboBox();
            this.pictureBoxStarter3 = new System.Windows.Forms.PictureBox();
            this.labelHeldItem = new System.Windows.Forms.Label();
            this.comboBoxHeldItem = new System.Windows.Forms.ComboBox();
            this.pictureBoxHeldItem = new System.Windows.Forms.PictureBox();
            this.labelLevel = new System.Windows.Forms.Label();
            this.numericLevel = new System.Windows.Forms.NumericUpDown();
            this.buttonSave = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.numericLevel)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxStarter1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxStarter2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxStarter3)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxHeldItem)).BeginInit();
            this.SuspendLayout();
            //
            // labelStarter1
            //
            this.labelStarter1.AutoSize = true;
            this.labelStarter1.Location = new System.Drawing.Point(15, 18);
            this.labelStarter1.Name = "labelStarter1";
            this.labelStarter1.Size = new System.Drawing.Size(58, 15);
            this.labelStarter1.TabIndex = 0;
            this.labelStarter1.Text = "Starter 1:";
            //
            // comboBoxStarter1
            //
            this.comboBoxStarter1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxStarter1.FormattingEnabled = true;
            this.comboBoxStarter1.Location = new System.Drawing.Point(100, 15);
            this.comboBoxStarter1.Name = "comboBoxStarter1";
            this.comboBoxStarter1.Size = new System.Drawing.Size(190, 23);
            this.comboBoxStarter1.TabIndex = 1;
            this.comboBoxStarter1.SelectedIndexChanged += new System.EventHandler(this.comboBoxStarter_SelectedIndexChanged);
            //
            // pictureBoxStarter1
            //
            this.pictureBoxStarter1.Location = new System.Drawing.Point(300, 8);
            this.pictureBoxStarter1.Name = "pictureBoxStarter1";
            this.pictureBoxStarter1.Size = new System.Drawing.Size(40, 40);
            this.pictureBoxStarter1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxStarter1.TabIndex = 2;
            this.pictureBoxStarter1.TabStop = false;
            //
            // labelStarter2
            //
            this.labelStarter2.AutoSize = true;
            this.labelStarter2.Location = new System.Drawing.Point(15, 62);
            this.labelStarter2.Name = "labelStarter2";
            this.labelStarter2.Size = new System.Drawing.Size(58, 15);
            this.labelStarter2.TabIndex = 3;
            this.labelStarter2.Text = "Starter 2:";
            //
            // comboBoxStarter2
            //
            this.comboBoxStarter2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxStarter2.FormattingEnabled = true;
            this.comboBoxStarter2.Location = new System.Drawing.Point(100, 59);
            this.comboBoxStarter2.Name = "comboBoxStarter2";
            this.comboBoxStarter2.Size = new System.Drawing.Size(190, 23);
            this.comboBoxStarter2.TabIndex = 4;
            this.comboBoxStarter2.SelectedIndexChanged += new System.EventHandler(this.comboBoxStarter_SelectedIndexChanged);
            //
            // pictureBoxStarter2
            //
            this.pictureBoxStarter2.Location = new System.Drawing.Point(300, 52);
            this.pictureBoxStarter2.Name = "pictureBoxStarter2";
            this.pictureBoxStarter2.Size = new System.Drawing.Size(40, 40);
            this.pictureBoxStarter2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxStarter2.TabIndex = 5;
            this.pictureBoxStarter2.TabStop = false;
            //
            // labelStarter3
            //
            this.labelStarter3.AutoSize = true;
            this.labelStarter3.Location = new System.Drawing.Point(15, 106);
            this.labelStarter3.Name = "labelStarter3";
            this.labelStarter3.Size = new System.Drawing.Size(58, 15);
            this.labelStarter3.TabIndex = 6;
            this.labelStarter3.Text = "Starter 3:";
            //
            // comboBoxStarter3
            //
            this.comboBoxStarter3.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxStarter3.FormattingEnabled = true;
            this.comboBoxStarter3.Location = new System.Drawing.Point(100, 103);
            this.comboBoxStarter3.Name = "comboBoxStarter3";
            this.comboBoxStarter3.Size = new System.Drawing.Size(190, 23);
            this.comboBoxStarter3.TabIndex = 7;
            this.comboBoxStarter3.SelectedIndexChanged += new System.EventHandler(this.comboBoxStarter_SelectedIndexChanged);
            //
            // pictureBoxStarter3
            //
            this.pictureBoxStarter3.Location = new System.Drawing.Point(300, 96);
            this.pictureBoxStarter3.Name = "pictureBoxStarter3";
            this.pictureBoxStarter3.Size = new System.Drawing.Size(40, 40);
            this.pictureBoxStarter3.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxStarter3.TabIndex = 8;
            this.pictureBoxStarter3.TabStop = false;
            //
            // labelHeldItem
            //
            this.labelHeldItem.AutoSize = true;
            this.labelHeldItem.Location = new System.Drawing.Point(15, 150);
            this.labelHeldItem.Name = "labelHeldItem";
            this.labelHeldItem.Size = new System.Drawing.Size(65, 15);
            this.labelHeldItem.TabIndex = 9;
            this.labelHeldItem.Text = "Held Item:";
            //
            // comboBoxHeldItem
            //
            this.comboBoxHeldItem.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxHeldItem.FormattingEnabled = true;
            this.comboBoxHeldItem.Location = new System.Drawing.Point(100, 147);
            this.comboBoxHeldItem.Name = "comboBoxHeldItem";
            this.comboBoxHeldItem.Size = new System.Drawing.Size(190, 23);
            this.comboBoxHeldItem.TabIndex = 10;
            this.comboBoxHeldItem.SelectedIndexChanged += new System.EventHandler(this.comboBoxHeldItem_SelectedIndexChanged);
            //
            // pictureBoxHeldItem
            //
            this.pictureBoxHeldItem.Location = new System.Drawing.Point(300, 140);
            this.pictureBoxHeldItem.Name = "pictureBoxHeldItem";
            this.pictureBoxHeldItem.Size = new System.Drawing.Size(40, 40);
            this.pictureBoxHeldItem.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxHeldItem.TabIndex = 11;
            this.pictureBoxHeldItem.TabStop = false;
            //
            // labelLevel
            //
            this.labelLevel.AutoSize = true;
            this.labelLevel.Location = new System.Drawing.Point(15, 194);
            this.labelLevel.Name = "labelLevel";
            this.labelLevel.Size = new System.Drawing.Size(38, 15);
            this.labelLevel.TabIndex = 12;
            this.labelLevel.Text = "Level:";
            //
            // numericLevel
            //
            this.numericLevel.Location = new System.Drawing.Point(100, 191);
            this.numericLevel.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numericLevel.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numericLevel.Name = "numericLevel";
            this.numericLevel.Size = new System.Drawing.Size(90, 23);
            this.numericLevel.TabIndex = 13;
            this.numericLevel.Value = new decimal(new int[] { 5, 0, 0, 0 });
            this.numericLevel.ValueChanged += new System.EventHandler(this.numericLevel_ValueChanged);
            //
            // buttonSave
            //
            this.buttonSave.Location = new System.Drawing.Point(100, 233);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(140, 30);
            this.buttonSave.TabIndex = 14;
            this.buttonSave.Text = "Save Changes";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            //
            // StarterEditor
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(400, 285);
            this.Controls.Add(this.buttonSave);
            this.Controls.Add(this.numericLevel);
            this.Controls.Add(this.labelLevel);
            this.Controls.Add(this.pictureBoxHeldItem);
            this.Controls.Add(this.comboBoxHeldItem);
            this.Controls.Add(this.labelHeldItem);
            this.Controls.Add(this.pictureBoxStarter3);
            this.Controls.Add(this.comboBoxStarter3);
            this.Controls.Add(this.labelStarter3);
            this.Controls.Add(this.pictureBoxStarter2);
            this.Controls.Add(this.comboBoxStarter2);
            this.Controls.Add(this.labelStarter2);
            this.Controls.Add(this.pictureBoxStarter1);
            this.Controls.Add(this.comboBoxStarter1);
            this.Controls.Add(this.labelStarter1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "StarterEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Starter Pokemon Editor";
            ((System.ComponentModel.ISupportInitialize)(this.numericLevel)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxStarter1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxStarter2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxStarter3)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxHeldItem)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelStarter1;
        private System.Windows.Forms.ComboBox comboBoxStarter1;
        private System.Windows.Forms.PictureBox pictureBoxStarter1;
        private System.Windows.Forms.Label labelStarter2;
        private System.Windows.Forms.ComboBox comboBoxStarter2;
        private System.Windows.Forms.PictureBox pictureBoxStarter2;
        private System.Windows.Forms.Label labelStarter3;
        private System.Windows.Forms.ComboBox comboBoxStarter3;
        private System.Windows.Forms.PictureBox pictureBoxStarter3;
        private System.Windows.Forms.Label labelHeldItem;
        private System.Windows.Forms.ComboBox comboBoxHeldItem;
        private System.Windows.Forms.PictureBox pictureBoxHeldItem;
        private System.Windows.Forms.Label labelLevel;
        private System.Windows.Forms.NumericUpDown numericLevel;
        private System.Windows.Forms.Button buttonSave;
    }
}
