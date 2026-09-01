using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Forms;

namespace DSPRE {
    public static class Extensions {
        public static void SetAllItemsChecked(this CheckedListBox clb, bool status) {
            for (int i = 0; i < clb.Items.Count; i++) {
                clb.SetItemChecked(i, status);
            }
        }
        // NOTE: the UI-free extensions (SubArray, ContainsNumber, IgnoreCaseEquals, Move, Reverse,
        // ToByteArrayChooseSize, PurgeSpecial, GetNumberStyle, IndexOfFirstNumber) moved to the core
        // CoreExtensions class (Ekona project, same namespace), this class keeps only WinForms helpers.
        public static void FadeIn(this Form o, int framelength = 16, int frames = 10) {
            //Object is not fully invisible. Fade it in
            while (o != null && !o.IsDisposed && o.Opacity < 1.0) {
                Thread.Sleep(framelength);
                o.Opacity += (1.0 / frames);
            }
            o.Opacity = 1; //make fully visible
        }

        public static void FadeOut(this Form o, int framelength = 16, int frames = 10) {
            //Object is fully visible. Fade it out
            while (o != null && o.Opacity > 0.0) {
                Thread.Sleep(framelength);
                o.Opacity -= (1.0 / frames);
            }
            o.Opacity = 0; //make fully invisible
            AppLogger.Debug("Fadeout done");
        }

        public static List<string> ToStringsList (this ScintillaNET.LineCollection lc, bool allowEmpty = true, bool trim = false) {
            IEnumerable<string> temp = lc.Select(x => x.Text);
            
            if (trim) {
                temp = temp.Select(x => x.Trim());
            }
            
            if (!allowEmpty) {
                temp = temp.Where(x => !string.IsNullOrEmpty(x));
            }
            
            return temp.ToList();
        }

        //public static Dictionary<TValue, TKey> Reverse<TKey, TValue>(this IDictionary<TKey, TValue> source) {
        //    var dictionary = new Dictionary<TValue, TKey>();
        //    foreach (var entry in source) {
        //        if (!dictionary.ContainsKey(entry.Value)) {
        //            dictionary.Add(entry.Value, entry.Key);
        //        }
        //    }
        //    return dictionary;
        //}

        public static Bitmap Resize(this Bitmap source, int width, int height) {
            if (source.Width == width && source.Height == height) {
                return source;
            }

            Bitmap result = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(result)) {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(source, 0, 0, width, height);
            }
            return result;
        }
        public static Bitmap Resize(this Bitmap source, float factor) => source.Resize((int)(source.Width * factor), (int)(source.Height * factor));
    }

    public class ListBox2 : ListBox {
        public new void RefreshItem(int index) {
            base.RefreshItem(index);
        }
    }

    // TODO (Avalonia migration - step 33): Replace with an Avalonia NativeControlHost wrapping OpenTK.
    // For now this is a plain Panel stub so the project compiles without the Tao dependency.
    public class SimpleOpenGlControl2 : Panel {
        // --- Tao.Platform.Windows.SimpleOpenGlControl stub properties (ignored at runtime) ---
        public byte AccumBits        { get; set; }
        public bool AutoCheckErrors  { get; set; }
        public bool AutoFinish       { get; set; }
        public bool AutoMakeCurrent  { get; set; } = true;
        public bool AutoSwapBuffers  { get; set; } = true;
        public byte ColorBits        { get; set; } = 32;
        public byte DepthBits        { get; set; } = 64;
        public byte StencilBits      { get; set; }
        // Load event wired up by Designer
        public new event EventHandler Load;
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Load?.Invoke(this, EventArgs.Empty); }
        // --- Rendering stubs ---
        public SimpleOpenGlControl2() { BackColor = System.Drawing.Color.Black; }
        public void InitializeContexts() { /* stub */ }
        public void MakeCurrent()        { /* stub */ }
        public void SwapBuffers()        { /* stub */ }
    }
}
