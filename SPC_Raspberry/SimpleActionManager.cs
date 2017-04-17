using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace SmartPumpControlRemote
{
    public partial class SimpleActionManager : Form
    {
        public SimpleActionManager()
        {
            Application.EnableVisualStyles();
            InitializeComponent();
            tid = Shell.ReqestTID();

            //MessageBox.Show(tid);
            listView1.Enabled = !string.IsNullOrWhiteSpace(tid);
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (string.IsNullOrWhiteSpace(tid))
                Close();
        }
        private string tid = "";
        private void SimpleActionManager_Load(object sender, EventArgs e)
        {
            updateList();
        }
        private void updateList()
        {
            listView1.Items.Clear();
            var actions = Shell.GetActions(tid);
            foreach (var action in actions)
                listView1.Items.Add(action, action, "");

        }





        private void listView1_DoubleClick(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            new PayTypes().ShowDialog();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            new Connections().ShowDialog();
        }

        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems?.Count > 0)
            {
                Shell.SetAction(listView1.SelectedItems[0]?.SubItems?[0].Text, tid);
                updateList();
            }
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            editToolStripMenuItem.Enabled = (listView1.SelectedItems.Count > 0);
            deleteToolStripMenuItem.Enabled = (listView1.SelectedItems.Count > 0);
            runToolStripMenuItem.Enabled = (listView1.SelectedItems.Count > 0);
        }

        private void addToolStripMenuItem_Click(object sender, EventArgs e)
        {
                Shell.SetAction("", tid);
                updateList();

        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems?.Count > 0)
            {
                Shell.DeleteAction(listView1.SelectedItems[0]?.SubItems?[0].Text, tid);
                updateList();
            }
        }

        private void runToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems?.Count > 0)
            {
                Shell.RunAction(listView1.SelectedItems[0]?.SubItems?[0].Text, tid);
                updateList();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }

  
    }
}
