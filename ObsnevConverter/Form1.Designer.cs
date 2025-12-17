namespace ObsnevConverter
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            button1 = new Button();
            button2 = new Button();
            comboBox1 = new ComboBox();
            listBox1 = new ListBox();
            listBox2 = new ListBox();
            button3 = new Button();
            button4 = new Button();
            button5 = new Button();
            button6 = new Button();
            groupBox1 = new GroupBox();
            checkBox1 = new CheckBox();
            dataGridView2 = new DataGridView();
            label1 = new Label();
            label2 = new Label();
            button7 = new Button();
            label3 = new Label();
            label4 = new Label();
            groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).BeginInit();
            SuspendLayout();
            // 
            // button1
            // 
            button1.Location = new Point(32, 183);
            button1.Name = "button1";
            button1.Size = new Size(157, 29);
            button1.TabIndex = 0;
            button1.Text = "Seleccionar Ruta";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // button2
            // 
            button2.Location = new Point(470, 282);
            button2.Name = "button2";
            button2.Size = new Size(47, 29);
            button2.TabIndex = 1;
            button2.Text = "<-";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // comboBox1
            // 
            comboBox1.FormattingEnabled = true;
            comboBox1.Location = new Point(11, 26);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(151, 28);
            comboBox1.TabIndex = 2;
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
            // 
            // listBox1
            // 
            listBox1.FormattingEnabled = true;
            listBox1.Location = new Point(233, 12);
            listBox1.Name = "listBox1";
            listBox1.SelectionMode = SelectionMode.MultiExtended;
            listBox1.Size = new Size(217, 264);
            listBox1.TabIndex = 3;
            listBox1.SelectedIndexChanged += listBox1_SelectedIndexChanged;
            listBox1.DoubleClick += listBox1_DoubleClick;
            // 
            // listBox2
            // 
            listBox2.FormattingEnabled = true;
            listBox2.Location = new Point(470, 12);
            listBox2.Name = "listBox2";
            listBox2.Size = new Size(219, 264);
            listBox2.TabIndex = 4;
            // 
            // button3
            // 
            button3.Location = new Point(765, 37);
            button3.Name = "button3";
            button3.Size = new Size(157, 29);
            button3.TabIndex = 5;
            button3.Text = "Procesar";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click;
            // 
            // button4
            // 
            button4.Location = new Point(399, 282);
            button4.Name = "button4";
            button4.Size = new Size(51, 29);
            button4.TabIndex = 6;
            button4.Text = "->";
            button4.UseVisualStyleBackColor = true;
            button4.Click += button4_Click;
            // 
            // button5
            // 
            button5.Location = new Point(11, 72);
            button5.Name = "button5";
            button5.Size = new Size(94, 29);
            button5.TabIndex = 9;
            button5.Text = "Test";
            button5.UseVisualStyleBackColor = true;
            button5.Click += button5_Click;
            // 
            // button6
            // 
            button6.Location = new Point(11, 120);
            button6.Name = "button6";
            button6.Size = new Size(94, 29);
            button6.TabIndex = 10;
            button6.Text = "Config";
            button6.UseVisualStyleBackColor = true;
            button6.Click += button6_Click;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(checkBox1);
            groupBox1.Controls.Add(comboBox1);
            groupBox1.Controls.Add(button5);
            groupBox1.Controls.Add(button6);
            groupBox1.Location = new Point(27, 12);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(168, 155);
            groupBox1.TabIndex = 11;
            groupBox1.TabStop = false;
            groupBox1.Text = "Conexión";
            // 
            // checkBox1
            // 
            checkBox1.AutoSize = true;
            checkBox1.Location = new Point(111, 77);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new Size(51, 24);
            checkBox1.TabIndex = 11;
            checkBox1.Text = "OK";
            checkBox1.UseVisualStyleBackColor = true;
            // 
            // dataGridView2
            // 
            dataGridView2.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView2.Location = new Point(27, 319);
            dataGridView2.Name = "dataGridView2";
            dataGridView2.RowHeadersWidth = 51;
            dataGridView2.Size = new Size(966, 279);
            dataGridView2.TabIndex = 12;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Arial Rounded MT Bold", 24F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label1.ForeColor = SystemColors.Desktop;
            label1.Location = new Point(737, 114);
            label1.Name = "label1";
            label1.Size = new Size(212, 46);
            label1.TabIndex = 14;
            label1.Text = "12345678";
            label1.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Segoe UI", 20F);
            label2.ForeColor = SystemColors.Desktop;
            label2.Location = new Point(789, 187);
            label2.Name = "label2";
            label2.Size = new Size(109, 46);
            label2.TabIndex = 15;
            label2.Text = "label2";
            label2.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // button7
            // 
            button7.Enabled = false;
            button7.Location = new Point(796, 252);
            button7.Name = "button7";
            button7.Size = new Size(94, 29);
            button7.TabIndex = 16;
            button7.Text = "button7";
            button7.UseVisualStyleBackColor = true;
            button7.Visible = false;
            button7.Click += button7_Click;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(233, 286);
            label3.Name = "label3";
            label3.Size = new Size(156, 20);
            label3.TabIndex = 17;
            label3.Text = "Ficheros .csv en la ruta";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(533, 287);
            label4.Name = "label4";
            label4.Size = new Size(136, 20);
            label4.TabIndex = 18;
            label4.Text = "Ficheros a procesar";
            label4.Click += label4_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1005, 610);
            Controls.Add(label4);
            Controls.Add(label3);
            Controls.Add(button7);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(dataGridView2);
            Controls.Add(groupBox1);
            Controls.Add(button4);
            Controls.Add(button3);
            Controls.Add(listBox2);
            Controls.Add(listBox1);
            Controls.Add(button2);
            Controls.Add(button1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Form1";
            Text = "Procesador ETL";
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button button1;
        private Button button2;
        private ComboBox comboBox1;
        private ListBox listBox1;
        private ListBox listBox2;
        private Button button3;
        private Button button4;
        private Button button5;
        private Button button6;
        private GroupBox groupBox1;
        private CheckBox checkBox1;
        private DataGridView dataGridView2;
        private Label label1;
        private Label label2;
        private Button button7;
        private Label label3;
        private Label label4;
    }
}
