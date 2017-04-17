using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using SPC_Raspberry;

namespace SmartPumpControlRemote
{
    public partial class PayTypes : Form
    {
        public PayTypes()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Form1.Driver.Params["CardType"] = text;
            Form1.Driver.SaveParams();
            Close();
        }
        string text
        {
            get
            {
                var text = "";
                foreach(ListViewItem item in listView1.Items)
                {
                    text += ";" + item.SubItems[1].Text + "=" + item.SubItems[0].Text;
                }
                return text;
            }
        }
        private void PayTypes_Load(object sender, EventArgs e)
        {
            listView1.Items.Clear();
            var items = Form1.Driver.GetCardTypes().Split(';');
            foreach(var item in items)
            {
                //MessageBox.Show($"Test1 {item}");
                if (!string.IsNullOrWhiteSpace(item) && item.Contains('='))
                {
                    var key_value = item.Split('=');
                    //MessageBox.Show($"key {key_value[0]}, value {key_value[1]}");
                    if (!string.IsNullOrWhiteSpace(key_value[0]) && !string.IsNullOrWhiteSpace(key_value[1]))
                        listView1.Items.Add(new ListViewItem(new string[] { key_value[1].Trim(), key_value[0].Trim() } ));

                }
            }
           
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }


        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
                add_edit(listView1.SelectedItems[0]);
        }

        private void addToolStripMenuItem_Click(object sender, EventArgs e) => add_edit();

        private void add_edit(ListViewItem item = null)
        {
            if ( item != null && listView1.SelectedItems.Count == 1 && !string.IsNullOrWhiteSpace(item?.SubItems[0]?.ToString())
                && !string.IsNullOrWhiteSpace(item?.SubItems[1]?.ToString()))
            {
                if (listView1.SelectedItems[0].SubItems[1].Text == "99")
                {
                    MessageBox.Show("Редактирование вида оплаты Benzuber запрещено.", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                var add = new AddPayment() { ExCode = item?.SubItems[1]?.Text, PayName = item?.SubItems[0]?.Text, Text = $"Редактирование \"{item?.SubItems[0]?.Text}\"" };
                if (add.ShowDialog() == DialogResult.OK)
                {
                    listView1.SelectedItems[0].SubItems[0] = new ListViewItem.ListViewSubItem(listView1.SelectedItems[0], add.PayName);
                    listView1.SelectedItems[0].SubItems[1] = new ListViewItem.ListViewSubItem(listView1.SelectedItems[0], add.ExCode);
                }
            }
            else
            {
                var add = new AddPayment() { Text = $"Добавление вида оплаты" };
                if (add.ShowDialog() == DialogResult.OK)
                {
                    listView1.Items.Add(new ListViewItem(new string[] { add.PayName, add.ExCode }));
                }

            }
        }

        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e) => add_edit(listView1.SelectedItems[0]);

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                if (listView1.SelectedItems[0].SubItems[1].Text == "99")
                {
                    MessageBox.Show("Удаление вида оплаты Benzuber запрещено.", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
                if (MessageBox.Show($"Вы действительно хотите удалить\r\nвид оплаты:{listView1.SelectedItems[0].Text}", "Подтверждение удаления", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    listView1.Items.Remove(listView1.SelectedItems[0]);
                }
            }
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            editToolStripMenuItem.Enabled = (listView1.SelectedItems.Count>0);
            deleteToolStripMenuItem.Enabled = (listView1.SelectedItems.Count > 0);
        }
    }
}
