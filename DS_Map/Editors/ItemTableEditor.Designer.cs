namespace DSPRE.Editors
{
    partial class ItemTableEditor
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
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabPagePickup = new System.Windows.Forms.TabPage();
            this.pickupTableEditor = new DSPRE.Editors.PickupTableEditor();
            this.tabPageHiddenItems = new System.Windows.Forms.TabPage();
            this.hiddenItemsEditor = new DSPRE.Editors.HiddenItemsEditor();
            this.tabControl.SuspendLayout();
            this.tabPagePickup.SuspendLayout();
            this.tabPageHiddenItems.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabPagePickup);
            this.tabControl.Controls.Add(this.tabPageHiddenItems);
            this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl.Location = new System.Drawing.Point(0, 0);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(1024, 650);
            this.tabControl.TabIndex = 0;
            this.tabControl.SelectedIndexChanged += new System.EventHandler(this.tabControl_SelectedIndexChanged);
            // 
            // tabPagePickup
            // 
            this.tabPagePickup.Controls.Add(this.pickupTableEditor);
            this.tabPagePickup.Location = new System.Drawing.Point(4, 22);
            this.tabPagePickup.Name = "tabPagePickup";
            this.tabPagePickup.Padding = new System.Windows.Forms.Padding(3);
            this.tabPagePickup.Size = new System.Drawing.Size(1016, 624);
            this.tabPagePickup.TabIndex = 0;
            this.tabPagePickup.Text = "Pickup Table";
            this.tabPagePickup.UseVisualStyleBackColor = true;
            this.tabPagePickup.Enter += new System.EventHandler(this.tabPagePickup_Enter);
            // 
            // pickupTableEditor
            // 
            this.pickupTableEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pickupTableEditor.Location = new System.Drawing.Point(3, 3);
            this.pickupTableEditor.Name = "pickupTableEditor";
            this.pickupTableEditor.Size = new System.Drawing.Size(1010, 618);
            this.pickupTableEditor.TabIndex = 0;
            // 
            // tabPageHiddenItems
            // 
            this.tabPageHiddenItems.Controls.Add(this.hiddenItemsEditor);
            this.tabPageHiddenItems.Location = new System.Drawing.Point(4, 22);
            this.tabPageHiddenItems.Name = "tabPageHiddenItems";
            this.tabPageHiddenItems.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageHiddenItems.Size = new System.Drawing.Size(1016, 624);
            this.tabPageHiddenItems.TabIndex = 1;
            this.tabPageHiddenItems.Text = "Hidden Items Table";
            this.tabPageHiddenItems.UseVisualStyleBackColor = true;
            this.tabPageHiddenItems.Enter += new System.EventHandler(this.tabPageHiddenItems_Enter);
            // 
            // hiddenItemsEditor
            // 
            this.hiddenItemsEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hiddenItemsEditor.Location = new System.Drawing.Point(3, 3);
            this.hiddenItemsEditor.Name = "hiddenItemsEditor";
            this.hiddenItemsEditor.Size = new System.Drawing.Size(1010, 618);
            this.hiddenItemsEditor.TabIndex = 0;
            // 
            // ItemTableEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tabControl);
            this.Name = "ItemTableEditor";
            this.Size = new System.Drawing.Size(1024, 650);
            this.tabControl.ResumeLayout(false);
            this.tabPagePickup.ResumeLayout(false);
            this.tabPageHiddenItems.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabPagePickup;
        private PickupTableEditor pickupTableEditor;
        private System.Windows.Forms.TabPage tabPageHiddenItems;
        private HiddenItemsEditor hiddenItemsEditor;
    }
}
