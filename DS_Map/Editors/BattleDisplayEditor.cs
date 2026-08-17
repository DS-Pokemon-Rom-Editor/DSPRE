using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using static DSPRE.RomInfo;

namespace DSPRE.Editors {
    /// <summary>
    /// Battle Display tab: per-species battle-sprite Y offset, shadow X offset/size, per-gender sprite
    /// heights, and party-icon palette + graphic, with a live preview positioning the actual front/back
    /// sprites and shadow.
    /// </summary>
    public partial class BattleDisplayEditor : Form, IEditorWithUnsavedChanges {
        private static readonly string formName = "Battle Display";

        private readonly PokemonEditor parentEditor;
        private readonly string[] pokenames;

        private int currentLoadedId = -1;
        public bool dirty = false;

        #region IEditorWithUnsavedChanges
        public bool HasUnsavedChanges => dirty;
        public string UnsavedChangesDescription => $"{formName} (Mon {currentLoadedId})";
        void IEditorWithUnsavedChanges.SaveChanges() => SaveChangesInternal();
        public void DiscardChanges() => SetDirty(false);
        #endregion

        #region Data layer: per-family battle-offset storage
        // Storage by family:
        //   HGSS -> one 89 B combined record/mon in pokemonSpriteOffsets (/a/1/8/0); last 3 bytes = Y/X/size.
        //   Plat -> same combined record in pl_poke_data.narc + height.narc (per-gender heights).
        //   DP   -> poke_yofs / poke_shadow_ofx / poke_shadow (1 B/mon each) + height.narc.
        private struct BattleRec {
            public int FrontY, ShadowX, ShadowSize, Movement;
            public bool HasMovement, HasHeights;
            public int BackF, BackM, FrontF, FrontM;   // height.narc, signed
        }

        private interface IBattleOffsetSource {
            bool TryLoad(int id, out BattleRec rec);
            void Save(int id, in BattleRec rec);
        }

        /// <summary>Reads/writes per-mon records from a NARC that unpacks to either a single blob (record
        /// at id*recLen) or one file per mon (file "NNNN" = the record). Caches in memory; writes back to
        /// disk immediately (same convention as the rest of this editor's data classes).</summary>
        private sealed class OffsetNarc {
            private readonly DirNames _dir;
            private readonly int _recLen;
            private bool _ready, _multi;
            private byte[] _blob;
            private string _path;

            public OffsetNarc(DirNames dir, int recLen) { _dir = dir; _recLen = recLen; }

            private void Ensure() {
                if (_ready) return;
                _ready = true;
                DSUtils.TryUnpackNarcs(new List<DirNames> { _dir });
                _path = gameDirs[_dir].unpackedDir;
                var files = Directory.Exists(_path) ? Directory.GetFiles(_path) : Array.Empty<string>();
                _multi = files.Length > 1;
                _blob = (!_multi && files.Length == 1) ? File.ReadAllBytes(files[0]) : null;
            }

            private string FilePath(int id) => Path.Combine(_path, id.ToString("D4"));

            public byte[] GetRecord(int id) {
                Ensure();
                if (_multi) {
                    string f = FilePath(id);
                    return File.Exists(f) ? File.ReadAllBytes(f) : null;
                }
                if (_blob == null) return null;
                int off = id * _recLen;
                if (off < 0 || off + _recLen > _blob.Length) return null;
                var r = new byte[_recLen];
                Array.Copy(_blob, off, r, 0, _recLen);
                return r;
            }

            public void PutRecord(int id, byte[] rec) {
                Ensure();
                if (_multi) { File.WriteAllBytes(FilePath(id), rec); return; }
                if (_blob == null) return;
                int off = id * _recLen;
                if (off < 0 || off + rec.Length > _blob.Length) return;
                Array.Copy(rec, 0, _blob, off, rec.Length);
                File.WriteAllBytes(FilePath(0), _blob);   // single-file blob is "0000"
            }
        }

        /// <summary>height.narc (DP + Platinum): 4 signed 1-byte values per mon, file order F-back,
        /// M-back, F-front, M-front, so mon N's slot s is the (N*4 + s)th file.</summary>
        private sealed class HeightNarc {
            private const int FB = 0, MB = 1, FF = 2, MF = 3;
            private readonly OffsetNarc _n = new OffsetNarc(DirNames.pokeHeight, 1);

            public bool TryLoad(int id, out int backF, out int backM, out int frontF, out int frontM) {
                backF = backM = frontF = frontM = 0;
                var a = _n.GetRecord(id * 4 + FB); var b = _n.GetRecord(id * 4 + MB);
                var c = _n.GetRecord(id * 4 + FF); var d = _n.GetRecord(id * 4 + MF);
                if (a == null || b == null || c == null || d == null || a.Length < 1 || b.Length < 1 || c.Length < 1 || d.Length < 1) return false;
                backF = (sbyte)a[0]; backM = (sbyte)b[0]; frontF = (sbyte)c[0]; frontM = (sbyte)d[0];
                return true;
            }

            public void Save(int id, in BattleRec rec) {
                Put(id * 4 + FB, rec.BackF); Put(id * 4 + MB, rec.BackM); Put(id * 4 + FF, rec.FrontF); Put(id * 4 + MF, rec.FrontM);
            }
            private void Put(int idx, int v) { var r = _n.GetRecord(idx); if (r == null || r.Length < 1) return; r[0] = (byte)v; _n.PutRecord(idx, r); }
        }

        /// <summary>HGSS / Platinum: one combined record per mon; the 3 fields are the LAST 3 bytes (size
        /// last), an optional movement byte at a fixed offset, and (Platinum) per-gender heights.</summary>
        private sealed class CombinedTailSource : IBattleOffsetSource {
            private readonly OffsetNarc _narc;
            private readonly bool _hasMovement;
            private readonly int _movementOffset;
            private readonly HeightNarc _heights;
            public CombinedTailSource(DirNames dir, int recLen, bool hasMovement, int movementOffset, bool withHeights)
            { _narc = new OffsetNarc(dir, recLen); _hasMovement = hasMovement; _movementOffset = movementOffset; _heights = withHeights ? new HeightNarc() : null; }

            public bool TryLoad(int id, out BattleRec rec) {
                rec = default;
                var r = _narc.GetRecord(id);
                if (r == null || r.Length < 3) return false;
                int n = r.Length;
                rec.FrontY = (sbyte)r[n - 3]; rec.ShadowX = (sbyte)r[n - 2]; rec.ShadowSize = r[n - 1];
                if (_hasMovement && _movementOffset >= 0 && _movementOffset < n) { rec.Movement = r[_movementOffset]; rec.HasMovement = true; }
                if (_heights != null && _heights.TryLoad(id, out int bf, out int bm, out int ff, out int fm))
                { rec.BackF = bf; rec.BackM = bm; rec.FrontF = ff; rec.FrontM = fm; rec.HasHeights = true; }
                return true;
            }

            public void Save(int id, in BattleRec rec) {
                var r = _narc.GetRecord(id);
                if (r == null || r.Length < 3) return;
                int n = r.Length;
                r[n - 3] = (byte)(sbyte)rec.FrontY; r[n - 2] = (byte)(sbyte)rec.ShadowX; r[n - 1] = (byte)rec.ShadowSize;
                if (rec.HasMovement && _movementOffset >= 0 && _movementOffset < n) r[_movementOffset] = (byte)rec.Movement;
                _narc.PutRecord(id, r);
                if (_heights != null && rec.HasHeights) _heights.Save(id, in rec);
            }
        }

        /// <summary>Diamond / Pearl: front Y, shadow X and shadow size each live in their own single-byte-
        /// per-mon NARC, plus per-gender heights (height.narc).</summary>
        private sealed class SeparateByteSource : IBattleOffsetSource {
            private readonly OffsetNarc _y, _sx, _sz;
            private readonly HeightNarc _heights = new HeightNarc();
            public SeparateByteSource(DirNames yDir, DirNames sxDir, DirNames szDir)
            { _y = new OffsetNarc(yDir, 1); _sx = new OffsetNarc(sxDir, 1); _sz = new OffsetNarc(szDir, 1); }

            public bool TryLoad(int id, out BattleRec rec) {
                rec = default;
                var ry = _y.GetRecord(id); var rx = _sx.GetRecord(id); var rz = _sz.GetRecord(id);
                if (ry == null || rx == null || rz == null || ry.Length < 1 || rx.Length < 1 || rz.Length < 1) return false;
                rec.FrontY = (sbyte)ry[0]; rec.ShadowX = (sbyte)rx[0]; rec.ShadowSize = rz[0];
                if (_heights.TryLoad(id, out int bf, out int bm, out int ff, out int fm))
                { rec.BackF = bf; rec.BackM = bm; rec.FrontF = ff; rec.FrontM = fm; rec.HasHeights = true; }
                return true;
            }

            public void Save(int id, in BattleRec rec) {
                WriteByte(_y, id, (byte)(sbyte)rec.FrontY);
                WriteByte(_sx, id, (byte)(sbyte)rec.ShadowX);
                WriteByte(_sz, id, (byte)rec.ShadowSize);
                if (rec.HasHeights) _heights.Save(id, in rec);
            }
            private static void WriteByte(OffsetNarc narc, int id, byte v) { var r = narc.GetRecord(id); if (r == null || r.Length < 1) return; r[0] = v; narc.PutRecord(id, r); }
        }

        private IBattleOffsetSource _src;
        private bool _srcTried;
        private void EnsureSource() {
            if (_srcTried) return;
            _srcTried = true;
            try {
                switch (RomInfo.gameFamily) {
                    case GameFamilies.HGSS: _src = new CombinedTailSource(DirNames.pokemonSpriteOffsets, 89, hasMovement: true, movementOffset: 1, withHeights: false); break;
                    case GameFamilies.Plat: _src = new CombinedTailSource(DirNames.pokemonSpriteOffsets, 89, hasMovement: true, movementOffset: 1, withHeights: true); break;
                    case GameFamilies.DP: _src = new SeparateByteSource(DirNames.pokeYofs, DirNames.pokeShadowOfx, DirNames.pokeShadow); break;
                    default: _src = null; break;
                }
            } catch { _src = null; }
        }
        #endregion

        #region Loaded state
        private bool loading;
        private bool hasSpriteData, hasMovementType, hasHeights;
        private int spriteY, shadowX, shadowSize, movementType;
        private int frontHeightM, frontHeightF, backHeightM, backHeightF;
        private int oFrontHeightM, oFrontHeightF, oBackHeightM, oBackHeightF;   // baseline at load, for the delta preview
        private int frameIndex = 0;
        private int partyPaletteIndex = 0;
        private Bitmap pendingIconGraphic;   // staged import, written on Save
        // This tab's own decoded battle sprites for currentLoadedId. Slots: 0=FBack, 1=MBack, 2=FFront, 3=MFront.
        private Bitmap[] battleSprites = new Bitmap[4];
        #endregion

        public BattleDisplayEditor(Control parent, PokemonEditor pokeEditor) {
            this.parentEditor = pokeEditor;
            this.pokenames = RomInfo.GetPokemonNames();

            BuildUi();

            this.Text = formName;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = parent.Size;
            this.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;

            Helpers.DisableHandlers();
            IndexBox.Items.Clear();
            for (int i = 0; i < pokenames.Length; i++) IndexBox.Items.Add($"{i:D3} {pokenames[i]}");
            IndexBox.RefreshMasterList();
            Helpers.EnableHandlers();

            ChangeLoadedFile(1);
        }

        public bool CheckDiscardChanges() {
            if (!dirty) return true;
            DialogResult result = MessageBox.Show(
                $"{formName}\nThere are unsaved changes.\nDiscard and proceed?",
                $"{formName} - Unsaved changes", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes) return true;
            IndexBox.SelectedIndex = currentLoadedId;
            return false;
        }

        private void SetDirty(bool status) {
            dirty = status;
            this.Text = formName + (status ? "*" : "");
            parentEditor.UpdateTabPageNames();
        }

        public void ChangeLoadedFile(int toLoad) {
            currentLoadedId = toLoad;

            Helpers.DisableHandlers();
            IndexBox.SelectedIndex = toLoad;
            Helpers.EnableHandlers();

            LoadMon(toLoad);
            SetDirty(false);
        }

        private void LoadMon(int id) {
            loading = true;
            try {
                hasSpriteData = hasMovementType = hasHeights = false;
                spriteY = shadowX = shadowSize = movementType = 0;
                frontHeightM = frontHeightF = backHeightM = backHeightF = 0;
                oFrontHeightM = oFrontHeightF = oBackHeightM = oBackHeightF = 0;

                EnsureSource();
                if (_src != null && _src.TryLoad(id, out BattleRec rec)) {
                    spriteY = rec.FrontY; shadowX = rec.ShadowX; shadowSize = rec.ShadowSize; movementType = rec.Movement;
                    backHeightF = rec.BackF; backHeightM = rec.BackM; frontHeightF = rec.FrontF; frontHeightM = rec.FrontM;
                    oFrontHeightM = rec.FrontM; oFrontHeightF = rec.FrontF; oBackHeightM = rec.BackM; oBackHeightF = rec.BackF;
                    hasMovementType = rec.HasMovement; hasHeights = rec.HasHeights;
                    hasSpriteData = true;
                }

                pendingIconGraphic = null;
                partyPaletteIndex = Math.Max(0, Math.Min(2, DSUtils.GetMonIconPaletteId(id)));

                battleSprites = PokemonSpriteEditor.LoadBattleSpritesFor(id);

                bool maleHasSprites = battleSprites[1] != null || battleSprites[3] != null;
                bool femaleHasSprites = battleSprites[0] != null || battleSprites[2] != null;
                if (!maleHasSprites && femaleHasSprites) genderFemaleRadio.Checked = true;
                else if (maleHasSprites) genderMaleRadio.Checked = true;

                RefreshFieldsFromState();
                RefreshIconPreview();
                RefreshPreview();
            } finally { loading = false; }
        }

        private static decimal ClampDec(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

        private void RefreshFieldsFromState() {
            spriteYNumeric.Value = ClampDec(spriteY, -128, 127);
            shadowXNumeric.Value = ClampDec(shadowX, -128, 127);
            shadowSizeCombo.SelectedIndex = Math.Max(0, Math.Min(3, shadowSize));
            movementTypeNumeric.Value = ClampDec(movementType, 0, 255);
            movementGroup.Visible = hasMovementType;

            frontHeightMNumeric.Value = ClampDec(frontHeightM, -128, 127);
            frontHeightFNumeric.Value = ClampDec(frontHeightF, -128, 127);
            backHeightMNumeric.Value = ClampDec(backHeightM, -128, 127);
            backHeightFNumeric.Value = ClampDec(backHeightF, -128, 127);
            heightsGroup.Visible = hasHeights;

            bool en = hasSpriteData;
            spriteYNumeric.Enabled = shadowXNumeric.Enabled = shadowSizeCombo.Enabled = en;
            frontHeightMNumeric.Enabled = frontHeightFNumeric.Enabled = backHeightMNumeric.Enabled = backHeightFNumeric.Enabled = en;
            noDataLabel.Visible = !en;

            partyPaletteCombo.SelectedIndex = partyPaletteIndex;

            int maxFrame = Math.Max(PokemonSpriteEditor.GetBattleFrameCount(battleSprites[2]), PokemonSpriteEditor.GetBattleFrameCount(battleSprites[3])) - 1;
            frameNumeric.Maximum = Math.Max(0, maxFrame);
            if (frameIndex > (int)frameNumeric.Maximum) frameIndex = 0;
            frameNumeric.Value = frameIndex;
        }

        private void RefreshIconPreview() {
            if (currentLoadedId <= 0) { iconPreviewBox.Image = null; return; }
            try {
                iconPreviewBox.Image = pendingIconGraphic != null
                    ? (Image)pendingIconGraphic
                    : DSUtils.GetPokePic(currentLoadedId, 64, 64, partyPaletteIndex);
            } catch { iconPreviewBox.Image = null; }
        }

        private void RefreshPreview() => previewPanel.Invalidate();

        private const int Scale = 2;
        private void PreviewPanel_Paint(object sender, PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;

            g.DrawImage(Properties.Resources.BattleBackground, 0, 0, 256 * Scale, 192 * Scale);

            if (!hasSpriteData) return;

            bool female = genderFemaleRadio.Checked;

            if (shadowSize > 0) {
                int w = shadowSize == 1 ? 20 : shadowSize == 2 ? 30 : 40;
                int left = (shadowSize == 1 ? 179 : shadowSize == 2 ? 174 : 167) + shadowX;
                int top = shadowSize == 3 ? 82 : 83;
                using (Brush b = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                    g.FillEllipse(b, left * Scale, top * Scale, w * Scale, (w / 2) * Scale);
            }

            int frontH = female ? frontHeightF : frontHeightM, frontO = female ? oFrontHeightF : oFrontHeightM;
            int backH = female ? backHeightF : backHeightM, backO = female ? oBackHeightF : oBackHeightM;

            double enemyTop = 24 - spriteY + (hasHeights ? (frontH - frontO) : 0);
            double playerTop = hasHeights ? 84 + (backH - backO) : 84 - spriteY;

            Bitmap enemy = PokemonSpriteEditor.CropBattleFrame(battleSprites[female ? 2 : 3], frameIndex);
            Bitmap player = PokemonSpriteEditor.CropBattleFrame(battleSprites[female ? 0 : 1], frameIndex);

            if (enemy != null) g.DrawImage(enemy, 152 * Scale, (float)(enemyTop * Scale), 80 * Scale, 80 * Scale);
            if (player != null) g.DrawImage(player, 23 * Scale, (float)(playerTop * Scale), 80 * Scale, 80 * Scale);
        }

        private void SaveChangesInternal() {
            if (!dirty) return;

            if (_src != null && hasSpriteData) {
                var rec = new BattleRec {
                    FrontY = spriteY,
                    ShadowX = shadowX,
                    ShadowSize = shadowSize,
                    Movement = movementType,
                    HasMovement = hasMovementType,
                    BackF = backHeightF,
                    BackM = backHeightM,
                    FrontF = frontHeightF,
                    FrontM = frontHeightM,
                    HasHeights = hasHeights,
                };
                _src.Save(currentLoadedId, in rec);
            }

            if (pendingIconGraphic != null) {
                string error = DSUtils.SetMonIconGraphic(currentLoadedId, partyPaletteIndex, pendingIconGraphic);
                if (error != null) {
                    MessageBox.Show(error, "Save Icon Graphic", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                } else {
                    pendingIconGraphic = null;
                }
            }

            SetDirty(false);
        }

        #region UI
        public InputComboBox IndexBox;
        private NumericUpDown spriteYNumeric, shadowXNumeric, movementTypeNumeric;
        private ComboBox shadowSizeCombo, partyPaletteCombo;
        private GroupBox movementGroup, heightsGroup;
        private NumericUpDown frontHeightMNumeric, frontHeightFNumeric, backHeightMNumeric, backHeightFNumeric;
        private NumericUpDown frameNumeric;
        private RadioButton genderMaleRadio, genderFemaleRadio;
        private PictureBox iconPreviewBox;
        private Panel previewPanel;
        private Label noDataLabel;

        private void BuildUi() {
            SuspendLayout();

            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(8),
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 256 * Scale + 24));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var topBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            IndexBox = new InputComboBox { Width = 260 };
            IndexBox.SelectedIndexChanged += IndexBox_SelectedIndexChanged;
            var saveBtn = new Button { Text = "Save Changes", AutoSize = true };
            saveBtn.Click += (s, e) => SaveChangesInternal();
            topBar.Controls.Add(new Label { Text = "Pokémon:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
            topBar.Controls.Add(IndexBox);
            topBar.Controls.Add(saveBtn);

            // Left column: live preview + gender/frame picker.
            var leftPanel = new Panel { Dock = DockStyle.Fill };
            previewPanel = new Panel {
                Size = new Size(256 * Scale, 192 * Scale),
                BorderStyle = BorderStyle.FixedSingle,
                Top = 0, Left = 0,
            };
            previewPanel.Paint += PreviewPanel_Paint;

            var frameBar = new FlowLayoutPanel { Top = previewPanel.Height + 8, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            genderMaleRadio = new RadioButton { Text = "Male", Checked = true, AutoSize = true };
            genderFemaleRadio = new RadioButton { Text = "Female", AutoSize = true };
            genderMaleRadio.CheckedChanged += (s, e) => RefreshPreview();
            genderFemaleRadio.CheckedChanged += (s, e) => RefreshPreview();
            frameNumeric = new NumericUpDown { Minimum = 0, Maximum = 1, Width = 50 };
            frameNumeric.ValueChanged += (s, e) => { if (loading) return; frameIndex = (int)frameNumeric.Value; RefreshPreview(); };
            noDataLabel = new Label { Text = "No battle-sprite position data for this entry.", AutoSize = true, ForeColor = Color.OrangeRed, Visible = false };

            frameBar.Controls.Add(genderMaleRadio);
            frameBar.Controls.Add(genderFemaleRadio);
            frameBar.Controls.Add(new Label { Text = "  Frame:", AutoSize = true, Margin = new Padding(12, 4, 3, 3) });
            frameBar.Controls.Add(frameNumeric);

            leftPanel.Controls.Add(previewPanel);
            leftPanel.Controls.Add(frameBar);
            noDataLabel.Top = frameBar.Bottom + 4;
            leftPanel.Controls.Add(noDataLabel);

            // Right column: position / shadow / height fields + icon editing.
            var rightPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

            var posGroup = new GroupBox { Text = "Battle Sprite && Shadow", AutoSize = true, Width = 320 };
            var posGrid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(6) };
            spriteYNumeric = new NumericUpDown { Minimum = -128, Maximum = 127, Width = 70 };
            spriteYNumeric.ValueChanged += (s, e) => { if (loading) return; spriteY = (int)spriteYNumeric.Value; SetDirty(true); RefreshPreview(); };
            shadowXNumeric = new NumericUpDown { Minimum = -128, Maximum = 127, Width = 70 };
            shadowXNumeric.ValueChanged += (s, e) => { if (loading) return; shadowX = (int)shadowXNumeric.Value; SetDirty(true); RefreshPreview(); };
            shadowSizeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
            shadowSizeCombo.Items.AddRange(new object[] { "None", "Small", "Medium", "Large" });
            shadowSizeCombo.SelectedIndexChanged += (s, e) => { if (loading) return; shadowSize = shadowSizeCombo.SelectedIndex; SetDirty(true); RefreshPreview(); };
            AddRow(posGrid, "Front sprite Y offset", spriteYNumeric);
            AddRow(posGrid, "Shadow X offset", shadowXNumeric);
            AddRow(posGrid, "Shadow size", shadowSizeCombo);
            posGroup.Controls.Add(posGrid);

            movementGroup = new GroupBox { Text = "Movement (raw byte)", AutoSize = true, Width = 320 };
            var moveGrid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(6) };
            movementTypeNumeric = new NumericUpDown { Minimum = 0, Maximum = 255, Width = 70 };
            movementTypeNumeric.ValueChanged += (s, e) => { if (loading) return; movementType = (int)movementTypeNumeric.Value; SetDirty(true); };
            AddRow(moveGrid, "Movement byte", movementTypeNumeric);
            movementGroup.Controls.Add(moveGrid);

            heightsGroup = new GroupBox { Text = "Per-Gender Sprite Heights", AutoSize = true, Width = 320 };
            var heightsGrid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(6) };
            frontHeightMNumeric = new NumericUpDown { Minimum = -128, Maximum = 127, Width = 70 };
            frontHeightMNumeric.ValueChanged += (s, e) => { if (loading) return; frontHeightM = (int)frontHeightMNumeric.Value; SetDirty(true); RefreshPreview(); };
            frontHeightFNumeric = new NumericUpDown { Minimum = -128, Maximum = 127, Width = 70 };
            frontHeightFNumeric.ValueChanged += (s, e) => { if (loading) return; frontHeightF = (int)frontHeightFNumeric.Value; SetDirty(true); RefreshPreview(); };
            backHeightMNumeric = new NumericUpDown { Minimum = -128, Maximum = 127, Width = 70 };
            backHeightMNumeric.ValueChanged += (s, e) => { if (loading) return; backHeightM = (int)backHeightMNumeric.Value; SetDirty(true); RefreshPreview(); };
            backHeightFNumeric = new NumericUpDown { Minimum = -128, Maximum = 127, Width = 70 };
            backHeightFNumeric.ValueChanged += (s, e) => { if (loading) return; backHeightF = (int)backHeightFNumeric.Value; SetDirty(true); RefreshPreview(); };
            AddRow(heightsGrid, "Front height (Male)", frontHeightMNumeric);
            AddRow(heightsGrid, "Front height (Female)", frontHeightFNumeric);
            AddRow(heightsGrid, "Back height (Male)", backHeightMNumeric);
            AddRow(heightsGrid, "Back height (Female)", backHeightFNumeric);
            heightsGroup.Controls.Add(heightsGrid);

            var iconGroup = new GroupBox { Text = "Party Icon", AutoSize = true, Width = 320 };
            var iconLayout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(6) };
            iconPreviewBox = new PictureBox { Width = 64, Height = 64, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
            partyPaletteCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
            partyPaletteCombo.Items.AddRange(new object[] { "Palette 0", "Palette 1", "Palette 2" });
            partyPaletteCombo.SelectedIndexChanged += (s, e) => { if (loading) return; partyPaletteIndex = partyPaletteCombo.SelectedIndex; SetDirty(true); RefreshIconPreview(); };
            var iconButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            var importBtn = new Button { Text = "Import PNG", AutoSize = true };
            importBtn.Click += ImportIconButton_Click;
            var exportBtn = new Button { Text = "Export PNG", AutoSize = true };
            exportBtn.Click += ExportIconButton_Click;
            iconButtons.Controls.Add(importBtn);
            iconButtons.Controls.Add(exportBtn);

            iconLayout.Controls.Add(iconPreviewBox, 0, 0);
            iconLayout.SetRowSpan(iconPreviewBox, 2);
            iconLayout.Controls.Add(new Label { Text = "Palette bank:", AutoSize = true, Margin = new Padding(3, 6, 3, 3) }, 1, 0);
            iconLayout.Controls.Add(partyPaletteCombo, 1, 0);
            iconLayout.Controls.Add(iconButtons, 1, 1);
            iconGroup.Controls.Add(iconLayout);

            rightPanel.Controls.Add(posGroup);
            rightPanel.Controls.Add(movementGroup);
            rightPanel.Controls.Add(heightsGroup);
            rightPanel.Controls.Add(iconGroup);

            root.Controls.Add(leftPanel, 0, 0);
            root.Controls.Add(rightPanel, 1, 0);

            Controls.Add(root);
            Controls.Add(topBar);

            ResumeLayout(false);
        }

        private static void AddRow(TableLayoutPanel grid, string label, Control input) {
            int row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 6, 3, 3) }, 0, row);
            grid.Controls.Add(input, 1, row);
        }

        private void IndexBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) return;
            parentEditor.TrySyncIndices((ComboBox)sender);
            Helpers.DisableHandlers();
            if (CheckDiscardChanges()) ChangeLoadedFile(IndexBox.SelectedIndex);
            Helpers.EnableHandlers();
        }

        private void ImportIconButton_Click(object sender, EventArgs e) {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "PNG Image|*.png", Title = "Import Icon Graphic" }) {
                if (ofd.ShowDialog() != DialogResult.OK) return;

                Bitmap img;
                try { img = new Bitmap(ofd.FileName); }
                catch (Exception ex) { MessageBox.Show("Could not load image: " + ex.Message, "Import Icon Graphic", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

                string error = DSUtils.ValidateMonIconGraphic(currentLoadedId, img);
                if (error != null) { MessageBox.Show(error, "Import Icon Graphic", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

                pendingIconGraphic = img;
                SetDirty(true);
                RefreshIconPreview();
            }
        }

        private void ExportIconButton_Click(object sender, EventArgs e) {
            if (currentLoadedId <= 0) return;
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "PNG Image|*.png", FileName = $"mon{currentLoadedId:D3}_icon.png" }) {
                if (sfd.ShowDialog() != DialogResult.OK) return;
                try {
                    Bitmap img = pendingIconGraphic ?? DSUtils.GetMonIconGraphicBitmap(currentLoadedId, partyPaletteIndex);
                    img.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);
                } catch (Exception ex) {
                    MessageBox.Show("Export failed: " + ex.Message, "Export Icon Graphic", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        #endregion
    }
}
