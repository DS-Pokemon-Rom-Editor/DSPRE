namespace DSPRE.Editors
{
    partial class PokeDatabaseEditor
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
            this.mainTabControl = new System.Windows.Forms.TabControl();
            this.weatherTabPage = new System.Windows.Forms.TabPage();
            this.weatherGameTabControl = new System.Windows.Forms.TabControl();
            this.dpWeatherTab = new System.Windows.Forms.TabPage();
            this.dpWeatherListView = new System.Windows.Forms.ListView();
            this.addDPWeatherButton = new System.Windows.Forms.Button();
            this.editDPWeatherButton = new System.Windows.Forms.Button();
            this.ptWeatherTab = new System.Windows.Forms.TabPage();
            this.ptWeatherListView = new System.Windows.Forms.ListView();
            this.addPtWeatherButton = new System.Windows.Forms.Button();
            this.editPtWeatherButton = new System.Windows.Forms.Button();
            this.hgssWeatherTab = new System.Windows.Forms.TabPage();
            this.hgssWeatherListView = new System.Windows.Forms.ListView();
            this.addHGSSWeatherButton = new System.Windows.Forms.Button();
            this.editHGSSWeatherButton = new System.Windows.Forms.Button();
            this.cameraTabPage = new System.Windows.Forms.TabPage();
            this.cameraGameTabControl = new System.Windows.Forms.TabControl();
            this.dpptCameraTab = new System.Windows.Forms.TabPage();
            this.dpptCameraListView = new System.Windows.Forms.ListView();
            this.addDPPtCameraButton = new System.Windows.Forms.Button();
            this.editDPPtCameraButton = new System.Windows.Forms.Button();
            this.hgssCameraTab = new System.Windows.Forms.TabPage();
            this.hgssCameraListView = new System.Windows.Forms.ListView();
            this.addHGSSCameraButton = new System.Windows.Forms.Button();
            this.editHGSSCameraButton = new System.Windows.Forms.Button();
            this.musicTabPage = new System.Windows.Forms.TabPage();
            this.musicGameTabControl = new System.Windows.Forms.TabControl();
            this.dpMusicTab = new System.Windows.Forms.TabPage();
            this.dpMusicListView = new System.Windows.Forms.ListView();
            this.addDPMusicButton = new System.Windows.Forms.Button();
            this.editDPMusicButton = new System.Windows.Forms.Button();
            this.ptMusicTab = new System.Windows.Forms.TabPage();
            this.ptMusicListView = new System.Windows.Forms.ListView();
            this.addPtMusicButton = new System.Windows.Forms.Button();
            this.editPtMusicButton = new System.Windows.Forms.Button();
            this.hgssMusicTab = new System.Windows.Forms.TabPage();
            this.hgssMusicListView = new System.Windows.Forms.ListView();
            this.addHGSSMusicButton = new System.Windows.Forms.Button();
            this.editHGSSMusicButton = new System.Windows.Forms.Button();
            this.pokemonTabPage = new System.Windows.Forms.TabPage();
            this.pokemonListView = new System.Windows.Forms.ListView();
            this.editPokemonButton = new System.Windows.Forms.Button();
            this.areaTabPage = new System.Windows.Forms.TabPage();
            this.areaInfoLabel = new System.Windows.Forms.Label();
            this.bottomPanel = new System.Windows.Forms.Panel();
            this.saveButton = new System.Windows.Forms.Button();
            this.loadButton = new System.Windows.Forms.Button();
            this.applyButton = new System.Windows.Forms.Button();
            this.closeButton = new System.Windows.Forms.Button();
            this.infoLabel = new System.Windows.Forms.Label();
            this.mainTabControl.SuspendLayout();
            this.weatherTabPage.SuspendLayout();
            this.weatherGameTabControl.SuspendLayout();
            this.dpWeatherTab.SuspendLayout();
            this.ptWeatherTab.SuspendLayout();
            this.hgssWeatherTab.SuspendLayout();
            this.cameraTabPage.SuspendLayout();
            this.cameraGameTabControl.SuspendLayout();
            this.dpptCameraTab.SuspendLayout();
            this.hgssCameraTab.SuspendLayout();
            this.musicTabPage.SuspendLayout();
            this.musicGameTabControl.SuspendLayout();
            this.dpMusicTab.SuspendLayout();
            this.ptMusicTab.SuspendLayout();
            this.hgssMusicTab.SuspendLayout();
            this.pokemonTabPage.SuspendLayout();
            this.areaTabPage.SuspendLayout();
            this.bottomPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // mainTabControl
            // 
            this.mainTabControl.Controls.Add(this.weatherTabPage);
            this.mainTabControl.Controls.Add(this.cameraTabPage);
            this.mainTabControl.Controls.Add(this.musicTabPage);
            this.mainTabControl.Controls.Add(this.areaTabPage);
            this.mainTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainTabControl.Location = new System.Drawing.Point(0, 60);
            this.mainTabControl.Name = "mainTabControl";
            this.mainTabControl.SelectedIndex = 0;
            this.mainTabControl.Size = new System.Drawing.Size(784, 470);
            this.mainTabControl.TabIndex = 0;
            // 
            // weatherTabPage
            // 
            this.weatherTabPage.Controls.Add(this.weatherGameTabControl);
            this.weatherTabPage.Location = new System.Drawing.Point(4, 22);
            this.weatherTabPage.Name = "weatherTabPage";
            this.weatherTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.weatherTabPage.Size = new System.Drawing.Size(776, 444);
            this.weatherTabPage.TabIndex = 0;
            this.weatherTabPage.Text = "Weather";
            this.weatherTabPage.UseVisualStyleBackColor = true;
            // 
            // weatherGameTabControl
            // 
            this.weatherGameTabControl.Controls.Add(this.dpWeatherTab);
            this.weatherGameTabControl.Controls.Add(this.ptWeatherTab);
            this.weatherGameTabControl.Controls.Add(this.hgssWeatherTab);
            this.weatherGameTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.weatherGameTabControl.Location = new System.Drawing.Point(3, 3);
            this.weatherGameTabControl.Name = "weatherGameTabControl";
            this.weatherGameTabControl.SelectedIndex = 0;
            this.weatherGameTabControl.Size = new System.Drawing.Size(770, 438);
            this.weatherGameTabControl.TabIndex = 0;
            // 
            // dpWeatherTab
            // 
            this.dpWeatherTab.Controls.Add(this.dpWeatherListView);
            this.dpWeatherTab.Controls.Add(this.addDPWeatherButton);
            this.dpWeatherTab.Controls.Add(this.editDPWeatherButton);
            this.dpWeatherTab.Location = new System.Drawing.Point(4, 22);
            this.dpWeatherTab.Name = "dpWeatherTab";
            this.dpWeatherTab.Padding = new System.Windows.Forms.Padding(3);
            this.dpWeatherTab.Size = new System.Drawing.Size(762, 412);
            this.dpWeatherTab.TabIndex = 0;
            this.dpWeatherTab.Text = "Diamond/Pearl";
            this.dpWeatherTab.UseVisualStyleBackColor = true;
            // 
            // dpWeatherListView
            // 
            this.dpWeatherListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dpWeatherListView.HideSelection = false;
            this.dpWeatherListView.Location = new System.Drawing.Point(6, 6);
            this.dpWeatherListView.Name = "dpWeatherListView";
            this.dpWeatherListView.Size = new System.Drawing.Size(750, 370);
            this.dpWeatherListView.TabIndex = 0;
            this.dpWeatherListView.UseCompatibleStateImageBehavior = false;
            this.dpWeatherListView.DoubleClick += new System.EventHandler(this.editDPWeatherButton_Click);
            // 
            // addDPWeatherButton
            // 
            this.addDPWeatherButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.addDPWeatherButton.Location = new System.Drawing.Point(519, 383);
            this.addDPWeatherButton.Name = "addDPWeatherButton";
            this.addDPWeatherButton.Size = new System.Drawing.Size(75, 23);
            this.addDPWeatherButton.TabIndex = 2;
            this.addDPWeatherButton.Text = "Add Entry";
            this.addDPWeatherButton.UseVisualStyleBackColor = true;
            this.addDPWeatherButton.Click += new System.EventHandler(this.addDPWeatherButton_Click);
            // 
            // editDPWeatherButton
            // 
            this.editDPWeatherButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.editDPWeatherButton.Location = new System.Drawing.Point(600, 383);
            this.editDPWeatherButton.Name = "editDPWeatherButton";
            this.editDPWeatherButton.Size = new System.Drawing.Size(75, 23);
            this.editDPWeatherButton.TabIndex = 1;
            this.editDPWeatherButton.Text = "Edit";
            this.editDPWeatherButton.UseVisualStyleBackColor = true;
            this.editDPWeatherButton.Click += new System.EventHandler(this.editDPWeatherButton_Click);
            // 
            // ptWeatherTab
            // 
            this.ptWeatherTab.Controls.Add(this.ptWeatherListView);
            this.ptWeatherTab.Controls.Add(this.addPtWeatherButton);
            this.ptWeatherTab.Controls.Add(this.editPtWeatherButton);
            this.ptWeatherTab.Location = new System.Drawing.Point(4, 22);
            this.ptWeatherTab.Name = "ptWeatherTab";
            this.ptWeatherTab.Padding = new System.Windows.Forms.Padding(3);
            this.ptWeatherTab.Size = new System.Drawing.Size(762, 412);
            this.ptWeatherTab.TabIndex = 1;
            this.ptWeatherTab.Text = "Platinum";
            this.ptWeatherTab.UseVisualStyleBackColor = true;
            // 
            // ptWeatherListView
            // 
            this.ptWeatherListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ptWeatherListView.HideSelection = false;
            this.ptWeatherListView.Location = new System.Drawing.Point(6, 6);
            this.ptWeatherListView.Name = "ptWeatherListView";
            this.ptWeatherListView.Size = new System.Drawing.Size(750, 370);
            this.ptWeatherListView.TabIndex = 0;
            this.ptWeatherListView.UseCompatibleStateImageBehavior = false;
            this.ptWeatherListView.DoubleClick += new System.EventHandler(this.editPtWeatherButton_Click);
            // 
            // addPtWeatherButton
            // 
            this.addPtWeatherButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.addPtWeatherButton.Location = new System.Drawing.Point(519, 383);
            this.addPtWeatherButton.Name = "addPtWeatherButton";
            this.addPtWeatherButton.Size = new System.Drawing.Size(75, 23);
            this.addPtWeatherButton.TabIndex = 2;
            this.addPtWeatherButton.Text = "Add Entry";
            this.addPtWeatherButton.UseVisualStyleBackColor = true;
            this.addPtWeatherButton.Click += new System.EventHandler(this.addPtWeatherButton_Click);
            // 
            // editPtWeatherButton
            // 
            this.editPtWeatherButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.editPtWeatherButton.Location = new System.Drawing.Point(600, 383);
            this.editPtWeatherButton.Name = "editPtWeatherButton";
            this.editPtWeatherButton.Size = new System.Drawing.Size(75, 23);
            this.editPtWeatherButton.TabIndex = 1;
            this.editPtWeatherButton.Text = "Edit";
            this.editPtWeatherButton.UseVisualStyleBackColor = true;
            this.editPtWeatherButton.Click += new System.EventHandler(this.editPtWeatherButton_Click);
            // 
            // hgssWeatherTab
            // 
            this.hgssWeatherTab.Controls.Add(this.hgssWeatherListView);
            this.hgssWeatherTab.Controls.Add(this.addHGSSWeatherButton);
            this.hgssWeatherTab.Controls.Add(this.editHGSSWeatherButton);
            this.hgssWeatherTab.Location = new System.Drawing.Point(4, 22);
            this.hgssWeatherTab.Name = "hgssWeatherTab";
            this.hgssWeatherTab.Padding = new System.Windows.Forms.Padding(3);
            this.hgssWeatherTab.Size = new System.Drawing.Size(762, 412);
            this.hgssWeatherTab.TabIndex = 2;
            this.hgssWeatherTab.Text = "HeartGold/SoulSilver";
            this.hgssWeatherTab.UseVisualStyleBackColor = true;
            // 
            // hgssWeatherListView
            // 
            this.hgssWeatherListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.hgssWeatherListView.HideSelection = false;
            this.hgssWeatherListView.Location = new System.Drawing.Point(6, 6);
            this.hgssWeatherListView.Name = "hgssWeatherListView";
            this.hgssWeatherListView.Size = new System.Drawing.Size(750, 370);
            this.hgssWeatherListView.TabIndex = 0;
            this.hgssWeatherListView.UseCompatibleStateImageBehavior = false;
            this.hgssWeatherListView.DoubleClick += new System.EventHandler(this.editHGSSWeatherButton_Click);
            // 
            // addHGSSWeatherButton
            // 
            this.addHGSSWeatherButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.addHGSSWeatherButton.Location = new System.Drawing.Point(519, 383);
            this.addHGSSWeatherButton.Name = "addHGSSWeatherButton";
            this.addHGSSWeatherButton.Size = new System.Drawing.Size(75, 23);
            this.addHGSSWeatherButton.TabIndex = 2;
            this.addHGSSWeatherButton.Text = "Add Entry";
            this.addHGSSWeatherButton.UseVisualStyleBackColor = true;
            this.addHGSSWeatherButton.Click += new System.EventHandler(this.addHGSSWeatherButton_Click);
            // 
            // editHGSSWeatherButton
            // 
            this.editHGSSWeatherButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.editHGSSWeatherButton.Location = new System.Drawing.Point(600, 383);
            this.editHGSSWeatherButton.Name = "editHGSSWeatherButton";
            this.editHGSSWeatherButton.Size = new System.Drawing.Size(75, 23);
            this.editHGSSWeatherButton.TabIndex = 1;
            this.editHGSSWeatherButton.Text = "Edit";
            this.editHGSSWeatherButton.UseVisualStyleBackColor = true;
            this.editHGSSWeatherButton.Click += new System.EventHandler(this.editHGSSWeatherButton_Click);
            // 
            // cameraTabPage
            // 
            this.cameraTabPage.Controls.Add(this.cameraGameTabControl);
            this.cameraTabPage.Location = new System.Drawing.Point(4, 22);
            this.cameraTabPage.Name = "cameraTabPage";
            this.cameraTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.cameraTabPage.Size = new System.Drawing.Size(776, 444);
            this.cameraTabPage.TabIndex = 1;
            this.cameraTabPage.Text = "Camera Angles";
            this.cameraTabPage.UseVisualStyleBackColor = true;
            // 
            // cameraGameTabControl
            // 
            this.cameraGameTabControl.Controls.Add(this.dpptCameraTab);
            this.cameraGameTabControl.Controls.Add(this.hgssCameraTab);
            this.cameraGameTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cameraGameTabControl.Location = new System.Drawing.Point(3, 3);
            this.cameraGameTabControl.Name = "cameraGameTabControl";
            this.cameraGameTabControl.SelectedIndex = 0;
            this.cameraGameTabControl.Size = new System.Drawing.Size(770, 438);
            this.cameraGameTabControl.TabIndex = 0;
            // 
            // dpptCameraTab
            // 
            this.dpptCameraTab.Controls.Add(this.dpptCameraListView);
            this.dpptCameraTab.Controls.Add(this.addDPPtCameraButton);
            this.dpptCameraTab.Controls.Add(this.editDPPtCameraButton);
            this.dpptCameraTab.Location = new System.Drawing.Point(4, 22);
            this.dpptCameraTab.Name = "dpptCameraTab";
            this.dpptCameraTab.Padding = new System.Windows.Forms.Padding(3);
            this.dpptCameraTab.Size = new System.Drawing.Size(762, 412);
            this.dpptCameraTab.TabIndex = 0;
            this.dpptCameraTab.Text = "Diamond/Pearl/Platinum";
            this.dpptCameraTab.UseVisualStyleBackColor = true;
            // 
            // dpptCameraListView
            // 
            this.dpptCameraListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dpptCameraListView.HideSelection = false;
            this.dpptCameraListView.Location = new System.Drawing.Point(6, 6);
            this.dpptCameraListView.Name = "dpptCameraListView";
            this.dpptCameraListView.Size = new System.Drawing.Size(750, 370);
            this.dpptCameraListView.TabIndex = 0;
            this.dpptCameraListView.UseCompatibleStateImageBehavior = false;
            this.dpptCameraListView.DoubleClick += new System.EventHandler(this.editDPPtCameraButton_Click);
            // 
            // addDPPtCameraButton
            // 
            this.addDPPtCameraButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.addDPPtCameraButton.Location = new System.Drawing.Point(519, 383);
            this.addDPPtCameraButton.Name = "addDPPtCameraButton";
            this.addDPPtCameraButton.Size = new System.Drawing.Size(75, 23);
            this.addDPPtCameraButton.TabIndex = 2;
            this.addDPPtCameraButton.Text = "Add Entry";
            this.addDPPtCameraButton.UseVisualStyleBackColor = true;
            this.addDPPtCameraButton.Click += new System.EventHandler(this.addDPPtCameraButton_Click);
            // 
            // editDPPtCameraButton
            // 
            this.editDPPtCameraButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.editDPPtCameraButton.Location = new System.Drawing.Point(600, 383);
            this.editDPPtCameraButton.Name = "editDPPtCameraButton";
            this.editDPPtCameraButton.Size = new System.Drawing.Size(75, 23);
            this.editDPPtCameraButton.TabIndex = 1;
            this.editDPPtCameraButton.Text = "Edit";
            this.editDPPtCameraButton.UseVisualStyleBackColor = true;
            this.editDPPtCameraButton.Click += new System.EventHandler(this.editDPPtCameraButton_Click);
            // 
            // hgssCameraTab
            // 
            this.hgssCameraTab.Controls.Add(this.hgssCameraListView);
            this.hgssCameraTab.Controls.Add(this.addHGSSCameraButton);
            this.hgssCameraTab.Controls.Add(this.editHGSSCameraButton);
            this.hgssCameraTab.Location = new System.Drawing.Point(4, 22);
            this.hgssCameraTab.Name = "hgssCameraTab";
            this.hgssCameraTab.Padding = new System.Windows.Forms.Padding(3);
            this.hgssCameraTab.Size = new System.Drawing.Size(762, 412);
            this.hgssCameraTab.TabIndex = 1;
            this.hgssCameraTab.Text = "HeartGold/SoulSilver";
            this.hgssCameraTab.UseVisualStyleBackColor = true;
            // 
            // hgssCameraListView
            // 
            this.hgssCameraListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.hgssCameraListView.HideSelection = false;
            this.hgssCameraListView.Location = new System.Drawing.Point(6, 6);
            this.hgssCameraListView.Name = "hgssCameraListView";
            this.hgssCameraListView.Size = new System.Drawing.Size(750, 370);
            this.hgssCameraListView.TabIndex = 0;
            this.hgssCameraListView.UseCompatibleStateImageBehavior = false;
            this.hgssCameraListView.DoubleClick += new System.EventHandler(this.editHGSSCameraButton_Click);
            // 
            // addHGSSCameraButton
            // 
            this.addHGSSCameraButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.addHGSSCameraButton.Location = new System.Drawing.Point(519, 383);
            this.addHGSSCameraButton.Name = "addHGSSCameraButton";
            this.addHGSSCameraButton.Size = new System.Drawing.Size(75, 23);
            this.addHGSSCameraButton.TabIndex = 2;
            this.addHGSSCameraButton.Text = "Add Entry";
            this.addHGSSCameraButton.UseVisualStyleBackColor = true;
            this.addHGSSCameraButton.Click += new System.EventHandler(this.addHGSSCameraButton_Click);
            // 
            // editHGSSCameraButton
            // 
            this.editHGSSCameraButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.editHGSSCameraButton.Location = new System.Drawing.Point(600, 383);
            this.editHGSSCameraButton.Name = "editHGSSCameraButton";
            this.editHGSSCameraButton.Size = new System.Drawing.Size(75, 23);
            this.editHGSSCameraButton.TabIndex = 1;
            this.editHGSSCameraButton.Text = "Edit";
            this.editHGSSCameraButton.UseVisualStyleBackColor = true;
            this.editHGSSCameraButton.Click += new System.EventHandler(this.editHGSSCameraButton_Click);
            // 
            // musicTabPage
            // 
            this.musicTabPage.Controls.Add(this.musicGameTabControl);
            this.musicTabPage.Location = new System.Drawing.Point(4, 22);
            this.musicTabPage.Name = "musicTabPage";
            this.musicTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.musicTabPage.Size = new System.Drawing.Size(776, 444);
            this.musicTabPage.TabIndex = 2;
            this.musicTabPage.Text = "Music";
            this.musicTabPage.UseVisualStyleBackColor = true;
            // 
            // musicGameTabControl
            // 
            this.musicGameTabControl.Controls.Add(this.dpMusicTab);
            this.musicGameTabControl.Controls.Add(this.ptMusicTab);
            this.musicGameTabControl.Controls.Add(this.hgssMusicTab);
            this.musicGameTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.musicGameTabControl.Location = new System.Drawing.Point(3, 3);
            this.musicGameTabControl.Name = "musicGameTabControl";
            this.musicGameTabControl.SelectedIndex = 0;
            this.musicGameTabControl.Size = new System.Drawing.Size(770, 438);
            this.musicGameTabControl.TabIndex = 0;
            // 
            // dpMusicTab
            // 
            this.dpMusicTab.Controls.Add(this.dpMusicListView);
            this.dpMusicTab.Controls.Add(this.addDPMusicButton);
            this.dpMusicTab.Controls.Add(this.editDPMusicButton);
            this.dpMusicTab.Location = new System.Drawing.Point(4, 22);
            this.dpMusicTab.Name = "dpMusicTab";
            this.dpMusicTab.Padding = new System.Windows.Forms.Padding(3);
            this.dpMusicTab.Size = new System.Drawing.Size(762, 412);
            this.dpMusicTab.TabIndex = 0;
            this.dpMusicTab.Text = "Diamond/Pearl";
            this.dpMusicTab.UseVisualStyleBackColor = true;
            // 
            // dpMusicListView
            // 
            this.dpMusicListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dpMusicListView.HideSelection = false;
            this.dpMusicListView.Location = new System.Drawing.Point(6, 6);
            this.dpMusicListView.Name = "dpMusicListView";
            this.dpMusicListView.Size = new System.Drawing.Size(750, 370);
            this.dpMusicListView.TabIndex = 0;
            this.dpMusicListView.UseCompatibleStateImageBehavior = false;
            this.dpMusicListView.DoubleClick += new System.EventHandler(this.editDPMusicButton_Click);
            // 
            // addDPMusicButton
            // 
            this.addDPMusicButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.addDPMusicButton.Location = new System.Drawing.Point(519, 383);
            this.addDPMusicButton.Name = "addDPMusicButton";
            this.addDPMusicButton.Size = new System.Drawing.Size(75, 23);
            this.addDPMusicButton.TabIndex = 2;
            this.addDPMusicButton.Text = "Add Entry";
            this.addDPMusicButton.UseVisualStyleBackColor = true;
            this.addDPMusicButton.Click += new System.EventHandler(this.addDPMusicButton_Click);
            // 
            // editDPMusicButton
            // 
            this.editDPMusicButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.editDPMusicButton.Location = new System.Drawing.Point(600, 383);
            this.editDPMusicButton.Name = "editDPMusicButton";
            this.editDPMusicButton.Size = new System.Drawing.Size(75, 23);
            this.editDPMusicButton.TabIndex = 1;
            this.editDPMusicButton.Text = "Edit";
            this.editDPMusicButton.UseVisualStyleBackColor = true;
            this.editDPMusicButton.Click += new System.EventHandler(this.editDPMusicButton_Click);
            // 
            // ptMusicTab
            // 
            this.ptMusicTab.Controls.Add(this.ptMusicListView);
            this.ptMusicTab.Controls.Add(this.addPtMusicButton);
            this.ptMusicTab.Controls.Add(this.editPtMusicButton);
            this.ptMusicTab.Location = new System.Drawing.Point(4, 22);
            this.ptMusicTab.Name = "ptMusicTab";
            this.ptMusicTab.Padding = new System.Windows.Forms.Padding(3);
            this.ptMusicTab.Size = new System.Drawing.Size(762, 412);
            this.ptMusicTab.TabIndex = 1;
            this.ptMusicTab.Text = "Platinum";
            this.ptMusicTab.UseVisualStyleBackColor = true;
            // 
            // ptMusicListView
            // 
            this.ptMusicListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ptMusicListView.HideSelection = false;
            this.ptMusicListView.Location = new System.Drawing.Point(6, 6);
            this.ptMusicListView.Name = "ptMusicListView";
            this.ptMusicListView.Size = new System.Drawing.Size(750, 370);
            this.ptMusicListView.TabIndex = 0;
            this.ptMusicListView.UseCompatibleStateImageBehavior = false;
            this.ptMusicListView.DoubleClick += new System.EventHandler(this.editPtMusicButton_Click);
            // 
            // addPtMusicButton
            // 
            this.addPtMusicButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.addPtMusicButton.Location = new System.Drawing.Point(519, 383);
            this.addPtMusicButton.Name = "addPtMusicButton";
            this.addPtMusicButton.Size = new System.Drawing.Size(75, 23);
            this.addPtMusicButton.TabIndex = 2;
            this.addPtMusicButton.Text = "Add Entry";
            this.addPtMusicButton.UseVisualStyleBackColor = true;
            this.addPtMusicButton.Click += new System.EventHandler(this.addPtMusicButton_Click);
            // 
            // editPtMusicButton
            // 
            this.editPtMusicButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.editPtMusicButton.Location = new System.Drawing.Point(600, 383);
            this.editPtMusicButton.Name = "editPtMusicButton";
            this.editPtMusicButton.Size = new System.Drawing.Size(75, 23);
            this.editPtMusicButton.TabIndex = 1;
            this.editPtMusicButton.Text = "Edit";
            this.editPtMusicButton.UseVisualStyleBackColor = true;
            this.editPtMusicButton.Click += new System.EventHandler(this.editPtMusicButton_Click);
            // 
            // hgssMusicTab
            // 
            this.hgssMusicTab.Controls.Add(this.hgssMusicListView);
            this.hgssMusicTab.Controls.Add(this.addHGSSMusicButton);
            this.hgssMusicTab.Controls.Add(this.editHGSSMusicButton);
            this.hgssMusicTab.Location = new System.Drawing.Point(4, 22);
            this.hgssMusicTab.Name = "hgssMusicTab";
            this.hgssMusicTab.Padding = new System.Windows.Forms.Padding(3);
            this.hgssMusicTab.Size = new System.Drawing.Size(762, 412);
            this.hgssMusicTab.TabIndex = 2;
            this.hgssMusicTab.Text = "HeartGold/SoulSilver";
            this.hgssMusicTab.UseVisualStyleBackColor = true;
            // 
            // hgssMusicListView
            // 
            this.hgssMusicListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.hgssMusicListView.HideSelection = false;
            this.hgssMusicListView.Location = new System.Drawing.Point(6, 6);
            this.hgssMusicListView.Name = "hgssMusicListView";
            this.hgssMusicListView.Size = new System.Drawing.Size(750, 370);
            this.hgssMusicListView.TabIndex = 0;
            this.hgssMusicListView.UseCompatibleStateImageBehavior = false;
            this.hgssMusicListView.DoubleClick += new System.EventHandler(this.editHGSSMusicButton_Click);
            // 
            // addHGSSMusicButton
            // 
            this.addHGSSMusicButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.addHGSSMusicButton.Location = new System.Drawing.Point(519, 383);
            this.addHGSSMusicButton.Name = "addHGSSMusicButton";
            this.addHGSSMusicButton.Size = new System.Drawing.Size(75, 23);
            this.addHGSSMusicButton.TabIndex = 2;
            this.addHGSSMusicButton.Text = "Add Entry";
            this.addHGSSMusicButton.UseVisualStyleBackColor = true;
            this.addHGSSMusicButton.Click += new System.EventHandler(this.addHGSSMusicButton_Click);
            // 
            // editHGSSMusicButton
            // 
            this.editHGSSMusicButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.editHGSSMusicButton.Location = new System.Drawing.Point(600, 383);
            this.editHGSSMusicButton.Name = "editHGSSMusicButton";
            this.editHGSSMusicButton.Size = new System.Drawing.Size(75, 23);
            this.editHGSSMusicButton.TabIndex = 1;
            this.editHGSSMusicButton.Text = "Edit";
            this.editHGSSMusicButton.UseVisualStyleBackColor = true;
            this.editHGSSMusicButton.Click += new System.EventHandler(this.editHGSSMusicButton_Click);
            // 
            // pokemonTabPage
            // 
            this.pokemonTabPage.Controls.Add(this.pokemonListView);
            this.pokemonTabPage.Controls.Add(this.editPokemonButton);
            this.pokemonTabPage.Location = new System.Drawing.Point(4, 22);
            this.pokemonTabPage.Name = "pokemonTabPage";
            this.pokemonTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.pokemonTabPage.Size = new System.Drawing.Size(776, 444);
            this.pokemonTabPage.TabIndex = 3;
            this.pokemonTabPage.Text = "Pokémon Names";
            this.pokemonTabPage.UseVisualStyleBackColor = true;
            // 
            // pokemonListView
            // 
            this.pokemonListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pokemonListView.HideSelection = false;
            this.pokemonListView.Location = new System.Drawing.Point(6, 6);
            this.pokemonListView.Name = "pokemonListView";
            this.pokemonListView.Size = new System.Drawing.Size(764, 402);
            this.pokemonListView.TabIndex = 0;
            this.pokemonListView.UseCompatibleStateImageBehavior = false;
            this.pokemonListView.DoubleClick += new System.EventHandler(this.editPokemonButton_Click);
            // 
            // editPokemonButton
            // 
            this.editPokemonButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.editPokemonButton.Location = new System.Drawing.Point(695, 415);
            this.editPokemonButton.Name = "editPokemonButton";
            this.editPokemonButton.Size = new System.Drawing.Size(75, 23);
            this.editPokemonButton.TabIndex = 1;
            this.editPokemonButton.Text = "Edit";
            this.editPokemonButton.UseVisualStyleBackColor = true;
            this.editPokemonButton.Click += new System.EventHandler(this.editPokemonButton_Click);
            // 
            // areaTabPage
            // 
            this.areaTabPage.Controls.Add(this.areaInfoLabel);
            this.areaTabPage.Location = new System.Drawing.Point(4, 22);
            this.areaTabPage.Name = "areaTabPage";
            this.areaTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.areaTabPage.Size = new System.Drawing.Size(776, 444);
            this.areaTabPage.TabIndex = 4;
            this.areaTabPage.Text = "Area Data";
            this.areaTabPage.UseVisualStyleBackColor = true;
            // 
            // areaInfoLabel
            // 
            this.areaInfoLabel.AutoSize = true;
            this.areaInfoLabel.Location = new System.Drawing.Point(20, 20);
            this.areaInfoLabel.Name = "areaInfoLabel";
            this.areaInfoLabel.Size = new System.Drawing.Size(167, 13);
            this.areaInfoLabel.TabIndex = 0;
            this.areaInfoLabel.Text = "Area data will be displayed here.";
            // 
            // bottomPanel
            // 
            this.bottomPanel.Controls.Add(this.closeButton);
            this.bottomPanel.Controls.Add(this.applyButton);
            this.bottomPanel.Controls.Add(this.loadButton);
            this.bottomPanel.Controls.Add(this.saveButton);
            this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomPanel.Location = new System.Drawing.Point(0, 530);
            this.bottomPanel.Name = "bottomPanel";
            this.bottomPanel.Size = new System.Drawing.Size(784, 40);
            this.bottomPanel.TabIndex = 1;
            // 
            // saveButton
            // 
            this.saveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.saveButton.Location = new System.Drawing.Point(457, 8);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(100, 25);
            this.saveButton.TabIndex = 0;
            this.saveButton.Text = "Save As...";
            this.saveButton.UseVisualStyleBackColor = true;
            this.saveButton.Click += new System.EventHandler(this.saveButton_Click);
            // 
            // loadButton
            // 
            this.loadButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.loadButton.Location = new System.Drawing.Point(563, 8);
            this.loadButton.Name = "loadButton";
            this.loadButton.Size = new System.Drawing.Size(100, 25);
            this.loadButton.TabIndex = 1;
            this.loadButton.Text = "Load...";
            this.loadButton.UseVisualStyleBackColor = true;
            this.loadButton.Click += new System.EventHandler(this.loadButton_Click);
            // 
            // applyButton
            // 
            this.applyButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.applyButton.Location = new System.Drawing.Point(351, 8);
            this.applyButton.Name = "applyButton";
            this.applyButton.Size = new System.Drawing.Size(100, 25);
            this.applyButton.TabIndex = 2;
            this.applyButton.Text = "Apply Changes";
            this.applyButton.UseVisualStyleBackColor = true;
            this.applyButton.Click += new System.EventHandler(this.applyButton_Click);
            // 
            // closeButton
            // 
            this.closeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.closeButton.Location = new System.Drawing.Point(669, 8);
            this.closeButton.Name = "closeButton";
            this.closeButton.Size = new System.Drawing.Size(100, 25);
            this.closeButton.TabIndex = 3;
            this.closeButton.Text = "Close";
            this.closeButton.UseVisualStyleBackColor = true;
            this.closeButton.Click += new System.EventHandler(this.closeButton_Click);
            // 
            // infoLabel
            // 
            this.infoLabel.BackColor = System.Drawing.SystemColors.Info;
            this.infoLabel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.infoLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.infoLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.infoLabel.Location = new System.Drawing.Point(0, 0);
            this.infoLabel.Name = "infoLabel";
            this.infoLabel.Padding = new System.Windows.Forms.Padding(5);
            this.infoLabel.Size = new System.Drawing.Size(784, 60);
            this.infoLabel.TabIndex = 2;
            this.infoLabel.Text = "PokeDatabase Editor - Edit game data used by editors (Weather, Camera Angles, Mu" +
    "sic, Pokémon Names, etc.)\r\nDouble-click entries to edit. Save your changes as a" +
    " custom database or apply them directly to the active session.";
            // 
            // PokeDatabaseEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 570);
            this.Controls.Add(this.mainTabControl);
            this.Controls.Add(this.bottomPanel);
            this.Controls.Add(this.infoLabel);
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.Name = "PokeDatabaseEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "PokeDatabase Editor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PokeDatabaseEditor_FormClosing);
            this.mainTabControl.ResumeLayout(false);
            this.weatherTabPage.ResumeLayout(false);
            this.weatherGameTabControl.ResumeLayout(false);
            this.dpWeatherTab.ResumeLayout(false);
            this.ptWeatherTab.ResumeLayout(false);
            this.hgssWeatherTab.ResumeLayout(false);
            this.cameraTabPage.ResumeLayout(false);
            this.cameraGameTabControl.ResumeLayout(false);
            this.dpptCameraTab.ResumeLayout(false);
            this.hgssCameraTab.ResumeLayout(false);
            this.musicTabPage.ResumeLayout(false);
            this.musicGameTabControl.ResumeLayout(false);
            this.dpMusicTab.ResumeLayout(false);
            this.ptMusicTab.ResumeLayout(false);
            this.hgssMusicTab.ResumeLayout(false);
            this.pokemonTabPage.ResumeLayout(false);
            this.areaTabPage.ResumeLayout(false);
            this.areaTabPage.PerformLayout();
            this.bottomPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl mainTabControl;
        private System.Windows.Forms.TabPage weatherTabPage;
        private System.Windows.Forms.TabPage cameraTabPage;
        private System.Windows.Forms.TabPage musicTabPage;
        private System.Windows.Forms.TabPage pokemonTabPage;
        private System.Windows.Forms.TabPage areaTabPage;
        private System.Windows.Forms.Panel bottomPanel;
        private System.Windows.Forms.Button saveButton;
        private System.Windows.Forms.Button loadButton;
        private System.Windows.Forms.Button applyButton;
        private System.Windows.Forms.Button closeButton;
        private System.Windows.Forms.Label infoLabel;

        // Weather tab controls
        private System.Windows.Forms.TabControl weatherGameTabControl;
        private System.Windows.Forms.TabPage dpWeatherTab;
        private System.Windows.Forms.TabPage ptWeatherTab;
        private System.Windows.Forms.TabPage hgssWeatherTab;
        private System.Windows.Forms.ListView dpWeatherListView;
        private System.Windows.Forms.ListView ptWeatherListView;
        private System.Windows.Forms.ListView hgssWeatherListView;
        private System.Windows.Forms.Button addDPWeatherButton;
        private System.Windows.Forms.Button editDPWeatherButton;
        private System.Windows.Forms.Button addPtWeatherButton;
        private System.Windows.Forms.Button editPtWeatherButton;
        private System.Windows.Forms.Button addHGSSWeatherButton;
        private System.Windows.Forms.Button editHGSSWeatherButton;

        // Camera tab controls
        private System.Windows.Forms.TabControl cameraGameTabControl;
        private System.Windows.Forms.TabPage dpptCameraTab;
        private System.Windows.Forms.TabPage hgssCameraTab;
        private System.Windows.Forms.ListView dpptCameraListView;
        private System.Windows.Forms.ListView hgssCameraListView;
        private System.Windows.Forms.Button addDPPtCameraButton;
        private System.Windows.Forms.Button editDPPtCameraButton;
        private System.Windows.Forms.Button addHGSSCameraButton;
        private System.Windows.Forms.Button editHGSSCameraButton;

        // Music tab controls
        private System.Windows.Forms.TabControl musicGameTabControl;
        private System.Windows.Forms.TabPage dpMusicTab;
        private System.Windows.Forms.TabPage ptMusicTab;
        private System.Windows.Forms.TabPage hgssMusicTab;
        private System.Windows.Forms.ListView dpMusicListView;
        private System.Windows.Forms.ListView ptMusicListView;
        private System.Windows.Forms.ListView hgssMusicListView;
        private System.Windows.Forms.Button addDPMusicButton;
        private System.Windows.Forms.Button editDPMusicButton;
        private System.Windows.Forms.Button addPtMusicButton;
        private System.Windows.Forms.Button editPtMusicButton;
        private System.Windows.Forms.Button addHGSSMusicButton;
        private System.Windows.Forms.Button editHGSSMusicButton;

        // Pokemon tab controls
        private System.Windows.Forms.ListView pokemonListView;
        private System.Windows.Forms.Button editPokemonButton;

        // Area tab controls
        private System.Windows.Forms.Label areaInfoLabel;

        // Event handlers for edit buttons
        private void editDPWeatherButton_Click(object sender, System.EventArgs e)
        {
            EditSelectedItem(dpWeatherListView, (id, val) => workingData.DPWeatherDict[id] = val);
        }

        private void editPtWeatherButton_Click(object sender, System.EventArgs e)
        {
            EditSelectedItem(ptWeatherListView, (id, val) => workingData.PtWeatherDict[id] = val);
        }

        private void editHGSSWeatherButton_Click(object sender, System.EventArgs e)
        {
            EditSelectedItem(hgssWeatherListView, (id, val) => workingData.HGSSWeatherDict[id] = val);
        }

        private void editDPPtCameraButton_Click(object sender, System.EventArgs e)
        {
            EditSelectedItem(dpptCameraListView, (id, val) => workingData.DPPtCameraDict[id] = val);
        }

        private void editHGSSCameraButton_Click(object sender, System.EventArgs e)
        {
            EditSelectedItem(hgssCameraListView, (id, val) => workingData.HGSSCameraDict[id] = val);
        }

        private void editDPMusicButton_Click(object sender, System.EventArgs e)
        {
            EditSelectedMusicItem(dpMusicListView, (id, val) => workingData.DPMusicDict[id] = val);
        }

        private void editPtMusicButton_Click(object sender, System.EventArgs e)
        {
            EditSelectedMusicItem(ptMusicListView, (id, val) => workingData.PtMusicDict[id] = val);
        }

        private void editHGSSMusicButton_Click(object sender, System.EventArgs e)
        {
            EditSelectedMusicItem(hgssMusicListView, (id, val) => workingData.HGSSMusicDict[id] = val);
        }

        // Add Entry button handlers
        private void addDPWeatherButton_Click(object sender, System.EventArgs e)
        {
            AddNewEntry(dpWeatherListView, workingData.DPWeatherDict, "Weather");
        }

        private void addPtWeatherButton_Click(object sender, System.EventArgs e)
        {
            AddNewEntry(ptWeatherListView, workingData.PtWeatherDict, "Weather");
        }

        private void addHGSSWeatherButton_Click(object sender, System.EventArgs e)
        {
            AddNewEntry(hgssWeatherListView, workingData.HGSSWeatherDict, "Weather");
        }

        private void addDPPtCameraButton_Click(object sender, System.EventArgs e)
        {
            AddNewEntry(dpptCameraListView, workingData.DPPtCameraDict, "Camera");
        }

        private void addHGSSCameraButton_Click(object sender, System.EventArgs e)
        {
            AddNewEntry(hgssCameraListView, workingData.HGSSCameraDict, "Camera");
        }

        private void addDPMusicButton_Click(object sender, System.EventArgs e)
        {
            AddNewMusicEntry(dpMusicListView, workingData.DPMusicDict, "DP");
        }

        private void addPtMusicButton_Click(object sender, System.EventArgs e)
        {
            AddNewMusicEntry(ptMusicListView, workingData.PtMusicDict, "Pt");
        }

        private void addHGSSMusicButton_Click(object sender, System.EventArgs e)
        {
            AddNewMusicEntry(hgssMusicListView, workingData.HGSSMusicDict, "HGSS");
        }

        private void editPokemonButton_Click(object sender, System.EventArgs e)
        {
            EditSelectedItem(pokemonListView, (id, val) => workingData.PokemonDict[id] = val);
        }

        private void closeButton_Click(object sender, System.EventArgs e)
        {
            this.Close();
        }
    }
}
