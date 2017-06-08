using System;
using System.Drawing;
using System.Windows.Forms;

namespace SmartPumpControlRemote
{
    public partial class SortCommands : Form
    {
        public SortCommands()
        {
            Application.EnableVisualStyles();
            InitializeComponent();
        }
        public void AddItems(RunCmd.Cmd[] Items)
        {
            listBox1.Items.AddRange(Items);
        }

        public RunCmd.Cmd[] GetItems()
        {
            var items = new RunCmd.Cmd[listBox1.Items.Count];
            int index = 0;
            foreach (var item in listBox1.Items)
                items[index++] =(RunCmd.Cmd)item;
            return items;
        }

        int indexToMove;

        private void listBox1_MouseMove(object sender, MouseEventArgs e)
        {
            //если нажата левая кнопка мыши, начинаем Drag&Drop
            if (e.Button == MouseButtons.Left)
            {
                //индекс элемента, который мы перемещаем
                indexToMove = listBox1.IndexFromPoint(e.X, e.Y);
                listBox1.DoDragDrop(indexToMove, DragDropEffects.Move);
            }
        }

        private void listBox1_DragDrop(object sender, DragEventArgs e)
        {
            //индекс, куда перемещаем
            //listBox1.PointToClient(new Point(e.X, e.Y)) - необходимо
            //использовать поскольку в e храниться
            //положение мыши в экранных коородинатах, а эта
            //функция позволяет преобразовать в клиентские
            int newIndex = listBox1.IndexFromPoint(listBox1.PointToClient(new Point(e.X, e.Y)));
            //если вставка происходит в начало списка
            if (newIndex == -1)
            {
                //получаем перетаскиваемый элемент
                object itemToMove = listBox1.Items[indexToMove];
                //удаляем элемент
                listBox1.Items.RemoveAt(indexToMove);
                //добавляем в конец списка
                listBox1.Items.Add(itemToMove);
            }
            //вставляем где-то в середину списка
            else if (indexToMove != newIndex)
            {
                //получаем перетаскиваемый элемент
                object itemToMove = listBox1.Items[indexToMove];
                //удаляем элемент
                listBox1.Items.RemoveAt(indexToMove);
                //вставляем в конкретную позицию
                listBox1.Items.Insert(newIndex, itemToMove);
            }
        }

        private void listBox1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void button_ok_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void button_cancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();

        }
    }
}
