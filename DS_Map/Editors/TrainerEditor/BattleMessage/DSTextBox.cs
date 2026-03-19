using ScintillaNET;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DSPRE.Editors
{
    public partial class DSTextBox : UserControl
    {
        private const int pixelOffsetLeft = 10;
        private const int pixelOffsetTop = 5;
        private const int pixelOffsetRight = 21;
        private const int pixelOffsetBottom = 7;

        private const int lineCharacterCount = 39;

        private PrivateFontCollection messageFontCollection;
        private GCHandle messageFontDataHandle;
        private bool hasMessageFontDataHandle;

        public DSTextBox()
        {
            InitializeComponent();
            Resize += DSTextBox_Resize;
            UpdateDisplayScale();
            SetUpMessageScintillaFont();
        }

        public void UpdateDisplayScale()
        {
            // Get current scale
            double scaleX = this.Size.Width / 256.0;
            double scaleY = this.Size.Height / 46.0;

            // Snap scale to nearest whole multiple
            scaleX = Math.Round(scaleX);
            scaleY = Math.Round(scaleY);

            // Update Image size
            pictureBox.SendToBack();
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.Image = Properties.Resources.MessageBox;
            pictureBox.Size = new Size((int)(256 * scaleX), (int)(46 * scaleY));
            pictureBox.Update();

            int posX = (int) Math.Ceiling(pixelOffsetLeft * scaleX);
            int posY = (int) Math.Ceiling(pixelOffsetTop * scaleY);

            int width = (int) Math.Floor((this.Size.Width - (pixelOffsetLeft + pixelOffsetRight) * scaleX));
            int height = (int) Math.Floor((this.Size.Height - (pixelOffsetTop + pixelOffsetBottom) * scaleY));

            int zoom = (int) Math.Round(4.6 * scaleY);

            // Positon scintilla with the correct pixel offsets, and scale it to fit the size of the control
            scintilla.Location = new Point(posX, posY);
            scintilla.Size = new Size(width, height);
            scintilla.Zoom = zoom;
        }

        private void SetUpMessageScintillaFont()
        {
            try
            {
                byte[] fontData = Properties.Resources.pokemon_ds_font;
                if (fontData == null || fontData.Length == 0)
                {
                    return;
                }

                messageFontCollection = new PrivateFontCollection();
                messageFontDataHandle = GCHandle.Alloc(fontData, GCHandleType.Pinned);
                hasMessageFontDataHandle = true;

                messageFontCollection.AddMemoryFont(messageFontDataHandle.AddrOfPinnedObject(), fontData.Length);

                if (messageFontCollection.Families.Length == 0)
                {
                    return;
                }

                scintilla.Styles[Style.Default].Font = messageFontCollection.Families[0].Name;
                scintilla.StyleClearAll();
            }
            catch
            {
                // Fallback to default control font if custom font can't be loaded.
            }
        }

        private void DSTextBox_Resize(object sender, EventArgs e)
        {
            UpdateDisplayScale();
        }

        private void DSTextBox_ControlRemoved(object sender, ControlEventArgs e)
        {
            if (hasMessageFontDataHandle)
            {
                messageFontDataHandle.Free();
                hasMessageFontDataHandle = false;
            }

            if (messageFontCollection != null)
            {
                messageFontCollection.Dispose();
                messageFontCollection = null;
            }
        }
    }
}
