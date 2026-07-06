using System;
using System.Windows.Forms;

namespace DSPRE
{
    /// <summary>WinForms grid helper for <see cref="GameCamera"/> (kept out of the core class).</summary>
    public static class GameCameraGridExtensions
    {
        public static void ShowInGridView(this GameCamera cam, DataGridView dgv, int rowIndex)
        {
            if (rowIndex > dgv.Rows.Count - 1)
            {
                dgv.Rows.Add();
            }

            int colIndex = 0;

            dgv.Rows[rowIndex].HeaderCell.Value = String.Format("{0}", dgv.Rows[rowIndex].Index);

            dgv.Rows[rowIndex].Cells[colIndex++].Value = cam.distance;
            dgv.Rows[rowIndex].Cells[colIndex++].Value = cam.vertRot;
            dgv.Rows[rowIndex].Cells[colIndex++].Value = cam.horiRot;
            dgv.Rows[rowIndex].Cells[colIndex++].Value = cam.zRot;

            dgv.Rows[rowIndex].Cells[colIndex++].Value = cam.perspMode == GameCamera.ORTHO;

            dgv.Rows[rowIndex].Cells[colIndex++].Value = cam.fov;
            dgv.Rows[rowIndex].Cells[colIndex++].Value = cam.nearClip;
            dgv.Rows[rowIndex].Cells[colIndex++].Value = cam.farClip;

            if (colIndex < dgv.Columns.Count - 3)
            {
                if (cam.xOffset != null)
                {
                    dgv.Rows[rowIndex].Cells[colIndex++].Value = cam.xOffset;
                }

                if (cam.yOffset != null)
                {
                    dgv.Rows[rowIndex].Cells[colIndex++].Value = cam.yOffset;
                }

                if (cam.zOffset != null)
                {
                    dgv.Rows[rowIndex].Cells[colIndex++].Value = cam.zOffset;
                }
            }
        }
    }
}
