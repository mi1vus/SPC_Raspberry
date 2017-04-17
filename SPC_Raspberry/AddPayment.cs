using System;
using System.Windows.Forms;

namespace SmartPumpControlRemote
{
    public partial class AddPayment : Form
    {
        public AddPayment()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (ExCode == "99")
            {
                MessageBox.Show("Внешний код \"99\" зарезервирован для вида оплаты \"Benzuber.ru\"\r\nИспользуйте другой код", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                this.DialogResult = DialogResult.OK;
                Close();
            }
        }
        public string ExCode { get { return numericUpDown1.Value.ToString(); } set { numericUpDown1.Value = int.Parse(value); } }
        public string PayName { get { return textBox1.Text; } set { textBox1.Text = value; } }
    }
}
