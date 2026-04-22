namespace GA.Api.Sudoku
{
    partial class MainForm
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
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend1 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series1 = new System.Windows.Forms.DataVisualization.Charting.Series();
            grid = new SudokuGrid();
            button1 = new Button();
            chart1 = new System.Windows.Forms.DataVisualization.Charting.Chart();
            lblAverageFitness = new Label();
            pictureBox1 = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)chart1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // grid
            // 
            grid.BackColor = Color.White;
            grid.Location = new Point(12, 12);
            grid.Name = "grid";
            grid.Size = new Size(450, 450);
            grid.TabIndex = 0;
            // 
            // button1
            // 
            button1.Location = new Point(428, 465);
            button1.Name = "button1";
            button1.Size = new Size(75, 23);
            button1.TabIndex = 2;
            button1.Text = "Evolve";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // chart1
            // 
            chartArea1.Name = "ChartArea1";
            chart1.ChartAreas.Add(chartArea1);
            legend1.Name = "Legend1";
            chart1.Legends.Add(legend1);
            chart1.Location = new Point(468, 239);
            chart1.Name = "chart1";
            series1.ChartArea = "ChartArea1";
            series1.Legend = "Legend1";
            series1.Name = "Series1";
            chart1.Series.Add(series1);
            chart1.Size = new Size(415, 223);
            chart1.TabIndex = 4;
            chart1.Text = "chart1";
            // 
            // lblAverageFitness
            // 
            lblAverageFitness.AutoSize = true;
            lblAverageFitness.Location = new Point(12, 469);
            lblAverageFitness.Name = "lblAverageFitness";
            lblAverageFitness.Size = new Size(12, 15);
            lblAverageFitness.TabIndex = 5;
            lblAverageFitness.Text = "-";
            // 
            // pictureBox1
            // 
            pictureBox1.BackColor = SystemColors.ActiveCaption;
            pictureBox1.Location = new Point(468, 12);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(415, 221);
            pictureBox1.TabIndex = 6;
            pictureBox1.TabStop = false;
            pictureBox1.Paint += pictureBox1_Paint;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(895, 490);
            Controls.Add(pictureBox1);
            Controls.Add(lblAverageFitness);
            Controls.Add(chart1);
            Controls.Add(button1);
            Controls.Add(grid);
            MaximizeBox = false;
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Sudoku";
            Load += MainForm_Load;
            ((System.ComponentModel.ISupportInitialize)chart1).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private SudokuGrid grid;
        private Button button1;
        private System.Windows.Forms.DataVisualization.Charting.Chart chart1;
        private Label lblAverageFitness;
        private PictureBox pictureBox1;
    }
}