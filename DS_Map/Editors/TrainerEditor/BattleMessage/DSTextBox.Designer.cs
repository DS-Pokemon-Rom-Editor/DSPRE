namespace DSPRE.Editors
{
    partial class DSTextBox
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DSTextBox));
            this.scintilla = new ScintillaNET.Scintilla();
            this.pictureBox = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // scintilla
            // 
            this.scintilla.CaretWidth = 3;
            this.scintilla.HScrollBar = false;
            this.scintilla.Location = new System.Drawing.Point(14, 7);
            this.scintilla.Margin = new System.Windows.Forms.Padding(0);
            this.scintilla.Margins.Capacity = 1;
            this.scintilla.Name = "scintilla";
            this.scintilla.Size = new System.Drawing.Size(480, 77);
            this.scintilla.TabIndex = 1;
            this.scintilla.Text = "Message";
            this.scintilla.WrapMode = ScintillaNET.WrapMode.Word;
            this.scintilla.WrapVisualFlags = ScintillaNET.WrapVisualFlags.End;
            // 
            // pictureBox
            // 
            this.pictureBox.InitialImage = ((System.Drawing.Image)(resources.GetObject("pictureBox.InitialImage")));
            this.pictureBox.Location = new System.Drawing.Point(0, 0);
            this.pictureBox.Margin = new System.Windows.Forms.Padding(0);
            this.pictureBox.Name = "pictureBox";
            this.pictureBox.Size = new System.Drawing.Size(512, 92);
            this.pictureBox.TabIndex = 0;
            this.pictureBox.TabStop = false;
            // 
            // DSTextBox
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.scintilla);
            this.Controls.Add(this.pictureBox);
            this.Name = "DSTextBox";
            this.Size = new System.Drawing.Size(512, 92);
            this.ControlRemoved += new System.Windows.Forms.ControlEventHandler(this.DSTextBox_ControlRemoved);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        public System.Windows.Forms.PictureBox pictureBox;
        public ScintillaNET.Scintilla scintilla;
    }
}
