using System;
using System.Windows.Forms;

namespace SmartPumpControlRemote
{
    public partial class Settings : Form
    {
        public Settings()
        {
            InitializeComponent();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            new Connections().ShowDialog();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            new PayTypes().ShowDialog();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            new SimpleActionManager().ShowDialog();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
