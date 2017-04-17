namespace SmartPumpControlRemote
{
    partial class Connections
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label8 = new System.Windows.Forms.Label();
            this.service_timeout = new System.Windows.Forms.NumericUpDown();
            this.server_port = new System.Windows.Forms.NumericUpDown();
            this.label7 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.listView1 = new System.Windows.Forms.ListView();
            this.No = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Fuel = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.IntCode = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ExCode = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.benzuber_id = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.benzuber_server = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.benzuber_exchangeport = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.benzuber_enable = new System.Windows.Forms.CheckBox();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.service_timeout)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.server_port)).BeginInit();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.benzuber_exchangeport)).BeginInit();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button1.Location = new System.Drawing.Point(144, 407);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(100, 40);
            this.button1.TabIndex = 0;
            this.button1.Text = "Сохранить";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button2
            // 
            this.button2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button2.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button2.Location = new System.Drawing.Point(250, 407);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(100, 40);
            this.button2.TabIndex = 0;
            this.button2.Text = "Отмена";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.label8);
            this.groupBox1.Controls.Add(this.service_timeout);
            this.groupBox1.Controls.Add(this.server_port);
            this.groupBox1.Controls.Add(this.label7);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Location = new System.Drawing.Point(13, 13);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(334, 74);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Сервер терминалов SmartPumpControl";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(290, 46);
            this.label8.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(35, 13);
            this.label8.TabIndex = 2;
            this.label8.Text = "(Сек.)";
            // 
            // service_timeout
            // 
            this.service_timeout.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.service_timeout.Location = new System.Drawing.Point(237, 45);
            this.service_timeout.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.service_timeout.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.service_timeout.Name = "service_timeout";
            this.service_timeout.Size = new System.Drawing.Size(47, 20);
            this.service_timeout.TabIndex = 1;
            this.service_timeout.Value = new decimal(new int[] {
            300,
            0,
            0,
            0});
            // 
            // server_port
            // 
            this.server_port.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.server_port.Location = new System.Drawing.Point(237, 22);
            this.server_port.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.server_port.Name = "server_port";
            this.server_port.Size = new System.Drawing.Size(47, 20);
            this.server_port.TabIndex = 1;
            this.server_port.Value = new decimal(new int[] {
            1111,
            0,
            0,
            0});
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(6, 46);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(227, 13);
            this.label7.TabIndex = 0;
            this.label7.Text = "Таймаут выполнения сервисных операций:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 22);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(178, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Порт для входящих подключений:";
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Controls.Add(this.label5);
            this.groupBox2.Controls.Add(this.listView1);
            this.groupBox2.Controls.Add(this.benzuber_id);
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.benzuber_server);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.benzuber_exchangeport);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Enabled = false;
            this.groupBox2.Location = new System.Drawing.Point(10, 93);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(337, 307);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Подключение к сервису Benzuber.ru";
            // 
            // label6
            // 
            this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label6.ForeColor = System.Drawing.SystemColors.GrayText;
            this.label6.Location = new System.Drawing.Point(9, 278);
            this.label6.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(315, 26);
            this.label6.TabIndex = 9;
            this.label6.Text = "Для редактирования кода сервиса дважды нажмите на необходимый вид топлива ";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(6, 132);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(120, 13);
            this.label5.TabIndex = 8;
            this.label5.Text = "Кодировка продуктов:";
            // 
            // listView1
            // 
            this.listView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listView1.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.No,
            this.Fuel,
            this.IntCode,
            this.ExCode});
            this.listView1.FullRowSelect = true;
            this.listView1.Location = new System.Drawing.Point(9, 151);
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(316, 124);
            this.listView1.TabIndex = 7;
            this.listView1.UseCompatibleStateImageBehavior = false;
            this.listView1.View = System.Windows.Forms.View.Details;
            this.listView1.MouseDown += new System.Windows.Forms.MouseEventHandler(this.listView1_MouseDown);
            // 
            // No
            // 
            this.No.Text = "№";
            this.No.Width = 27;
            // 
            // Fuel
            // 
            this.Fuel.Text = "Вид топлива";
            this.Fuel.Width = 109;
            // 
            // IntCode
            // 
            this.IntCode.Text = "Код АСУ";
            this.IntCode.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.IntCode.Width = 72;
            // 
            // ExCode
            // 
            this.ExCode.Text = "Код Сервиса";
            this.ExCode.Width = 100;
            // 
            // benzuber_id
            // 
            this.benzuber_id.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.benzuber_id.Location = new System.Drawing.Point(95, 55);
            this.benzuber_id.Name = "benzuber_id";
            this.benzuber_id.Size = new System.Drawing.Size(230, 20);
            this.benzuber_id.TabIndex = 6;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(6, 58);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(53, 13);
            this.label4.TabIndex = 5;
            this.label4.Text = "Код АЗС:";
            // 
            // benzuber_server
            // 
            this.benzuber_server.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.benzuber_server.Location = new System.Drawing.Point(95, 81);
            this.benzuber_server.Name = "benzuber_server";
            this.benzuber_server.Size = new System.Drawing.Size(230, 20);
            this.benzuber_server.TabIndex = 6;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 84);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(86, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Адрес сервера:";
            // 
            // benzuber_exchangeport
            // 
            this.benzuber_exchangeport.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.benzuber_exchangeport.Location = new System.Drawing.Point(95, 107);
            this.benzuber_exchangeport.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.benzuber_exchangeport.Name = "benzuber_exchangeport";
            this.benzuber_exchangeport.Size = new System.Drawing.Size(230, 20);
            this.benzuber_exchangeport.TabIndex = 4;
            this.benzuber_exchangeport.Value = new decimal(new int[] {
            1102,
            0,
            0,
            0});
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 109);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(80, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Порт сервера:";
            // 
            // benzuber_enable
            // 
            this.benzuber_enable.AutoSize = true;
            this.benzuber_enable.Location = new System.Drawing.Point(23, 111);
            this.benzuber_enable.Name = "benzuber_enable";
            this.benzuber_enable.Size = new System.Drawing.Size(243, 30);
            this.benzuber_enable.TabIndex = 2;
            this.benzuber_enable.Text = "Разрешить принимать заказы от сервиса \r\nBenzuber.ru ";
            this.benzuber_enable.UseVisualStyleBackColor = true;
            this.benzuber_enable.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // Connections
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(358, 456);
            this.Controls.Add(this.benzuber_enable);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Connections";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Параметры подключений";
            this.Load += new System.EventHandler(this.Connections_Load);
            this.Shown += new System.EventHandler(this.Connections_Shown);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.service_timeout)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.server_port)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.benzuber_exchangeport)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.NumericUpDown server_port;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.TextBox benzuber_id;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox benzuber_server;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown benzuber_exchangeport;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox benzuber_enable;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ColumnHeader No;
        private System.Windows.Forms.ColumnHeader Fuel;
        private System.Windows.Forms.ColumnHeader IntCode;
        private System.Windows.Forms.ColumnHeader ExCode;
        private System.Windows.Forms.NumericUpDown service_timeout;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
    }
}