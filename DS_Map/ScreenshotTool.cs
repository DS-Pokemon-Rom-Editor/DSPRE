#if DEBUG
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace DSPRE
{
    /// <summary>
    /// Debug-only tool for capturing screenshots of forms.
    /// Ctrl+Click on any form to capture it.
    /// </summary>
    public static class ScreenshotTool
    {
        private static string OutputFolder => Path.Combine(
            Application.StartupPath,
            "Screenshots"
        );

        /// <summary>
        /// Enables Ctrl+Click screenshot capture on the specified form.
        /// Call this in the form's constructor or Load event.
        /// </summary>
        public static void EnableFor(Form form)
        {
            form.MouseClick += Form_MouseClick;
            form.KeyDown += (s, e) => { }; // Ensure KeyPreview works
            form.KeyPreview = true;
        }

        /// <summary>
        /// Enables Ctrl+Click screenshot capture on all currently open forms
        /// and any forms opened in the future.
        /// </summary>
        public static void EnableGlobally()
        {
            // Hook into existing forms
            foreach (Form form in Application.OpenForms)
            {
                EnableFor(form);
            }

            // For new forms, we need to use a message filter
            Application.AddMessageFilter(new ScreenshotMessageFilter());
        }

        private static void Form_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Control.ModifierKeys == Keys.Control)
            {
                Form form = sender as Form;
                if (form != null)
                {
                    CaptureForm(form);
                }
            }
        }

        /// <summary>
        /// Captures a screenshot of the specified form.
        /// </summary>
        public static void CaptureForm(Form form)
        {
            try
            {
                Directory.CreateDirectory(OutputFolder);

                string safeName = GetSafeFileName(form.Text);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = $"{safeName}_{timestamp}.png";
                string fullPath = Path.Combine(OutputFolder, filename);

                using (Bitmap bmp = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                    bmp.Save(fullPath, ImageFormat.Png);
                }

                AppLogger.Debug($"Screenshot saved: {fullPath}");
                
                // Show a brief notification
                ToolTip tooltip = new ToolTip();
                tooltip.Show($"Screenshot saved:\n{filename}", form, 10, 10, 2000);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to capture screenshot: {ex.Message}");
                MessageBox.Show($"Failed to capture screenshot:\n{ex.Message}", 
                    "Screenshot Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Captures all currently open forms.
        /// </summary>
        public static void CaptureAllForms()
        {
            foreach (Form form in Application.OpenForms)
            {
                CaptureForm(form);
            }
        }

        private static string GetSafeFileName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "Untitled";
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            string safeName = text;
            
            foreach (char c in invalidChars)
            {
                safeName = safeName.Replace(c, '_');
            }

            // Trim and limit length
            safeName = safeName.Trim();
            if (safeName.Length > 50)
            {
                safeName = safeName.Substring(0, 50);
            }

            return safeName;
        }

        /// <summary>
        /// Message filter to capture Ctrl+Click on any form, including child controls.
        /// </summary>
        private class ScreenshotMessageFilter : IMessageFilter
        {
            private const int WM_LBUTTONDOWN = 0x0201;

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg == WM_LBUTTONDOWN && Control.ModifierKeys == Keys.Control)
                {
                    Control control = Control.FromHandle(m.HWnd);
                    if (control != null)
                    {
                        Form form = control.FindForm();
                        if (form != null)
                        {
                            CaptureForm(form);
                            return true; // Message handled
                        }
                    }
                }
                return false; // Let the message through
            }
        }
    }
}
#endif
