using System.Windows.Forms;

namespace SmartPumpControlRemote
{
    public partial class enter_val : UserControl
    {
        public enter_val()
        {
            InitializeComponent();
        }
        public string SelectedValue
        {
            get
            {
                return comboBox1.Text;
            }
            set
            {
                if (value != null)
                {
                    comboBox1.Text = value;
                }
            }
        }
        public string Info
        {
            get
            {
                return label.Text;
            }
            set
            {
                label.Text = value;
            }
        }
       
        public string[] Values
        { 
            set
            {
                if (value != null && value.Length>0)
                {
                    comboBox1.Items.AddRange(value);
                    comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
                    //comboBox1.SelectedIndex = 0;
                    SelectedValue = value[0];
                    
                }
                else
                {
                    comboBox1.Items.Clear();
                    comboBox1.DropDownStyle = ComboBoxStyle.DropDown;
                }
            }
        }
    }
}
