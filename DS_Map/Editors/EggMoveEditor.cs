using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DSPRE
{

    struct EggMoveEntry
    {
        public int speciesID;
        public List<int> moveIDs;
        public EggMoveEntry(int speciesID, List<int> moveIDs)
        {
            this.speciesID = speciesID;
            this.moveIDs = moveIDs;
        }

        public int GetSizeInBytes()
        {
            // speciesID + moveIDs (2 bytes each)
            return 2 + (2 * moveIDs.Count);
        }
    }


    public partial class EggMoveEditor : Form
    {
        private readonly int MAX_EGG_MOVES = 16; // Max number of egg moves per species
        private readonly int MAX_TABLE_SIZE; // in DPPt size is limited, in HGSS it's not

        private List<EggMoveEntry> eggMoveData = new List<EggMoveEntry>();

        public EggMoveEditor()
        {
            InitializeComponent();
        }

        private void saveDataButton_Click(object sender, EventArgs e)
        {

        }
    }
}
