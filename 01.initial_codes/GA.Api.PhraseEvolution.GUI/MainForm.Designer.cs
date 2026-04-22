namespace GA.Api.PhraseEvolution.GUI
{
    partial class MainForm
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
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea2 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend2 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series2 = new System.Windows.Forms.DataVisualization.Charting.Series();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.cmbPopulationSize = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.cmbElitismRate = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.cmbCrossoverType = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.cmbMutationType = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.chart1 = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.PopulationChart = new System.Windows.Forms.PictureBox();
            this.resultlist = new System.Windows.Forms.ListBox();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.chart1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.PopulationChart)).BeginInit();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.btnStart);
            this.groupBox1.Controls.Add(this.cmbPopulationSize);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.cmbElitismRate);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.cmbCrossoverType);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.cmbMutationType);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Location = new System.Drawing.Point(12, 2);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(136, 474);
            this.groupBox1.TabIndex = 4;
            this.groupBox1.TabStop = false;
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(6, 231);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(121, 43);
            this.btnStart.TabIndex = 8;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // cmbPopulationSize
            // 
            this.cmbPopulationSize.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPopulationSize.FormattingEnabled = true;
            this.cmbPopulationSize.Items.AddRange(new object[] {
            "1",
            "2",
            "4",
            "8",
            "16",
            "32",
            "64",
            "128"});
            this.cmbPopulationSize.Location = new System.Drawing.Point(6, 196);
            this.cmbPopulationSize.Name = "cmbPopulationSize";
            this.cmbPopulationSize.Size = new System.Drawing.Size(121, 21);
            this.cmbPopulationSize.TabIndex = 7;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(6, 174);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(113, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "Population Size (1024)";
            // 
            // cmbElitismRate
            // 
            this.cmbElitismRate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbElitismRate.FormattingEnabled = true;
            this.cmbElitismRate.Items.AddRange(new object[] {
            "0,1",
            "0,2",
            "0,3",
            "0,4",
            "0,5",
            "0,6",
            "0,7",
            "0,8",
            "0,9",
            "1,0"});
            this.cmbElitismRate.Location = new System.Drawing.Point(6, 92);
            this.cmbElitismRate.Name = "cmbElitismRate";
            this.cmbElitismRate.Size = new System.Drawing.Size(121, 21);
            this.cmbElitismRate.TabIndex = 5;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 70);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(62, 13);
            this.label3.TabIndex = 4;
            this.label3.Text = "Elitism Rate";
            // 
            // cmbCrossoverType
            // 
            this.cmbCrossoverType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCrossoverType.FormattingEnabled = true;
            this.cmbCrossoverType.Items.AddRange(new object[] {
            "OnePointCrossover",
            "UniformCrossover",
            "PMX"});
            this.cmbCrossoverType.Location = new System.Drawing.Point(6, 40);
            this.cmbCrossoverType.Name = "cmbCrossoverType";
            this.cmbCrossoverType.Size = new System.Drawing.Size(121, 21);
            this.cmbCrossoverType.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 18);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(81, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Crossover Type";
            // 
            // cmbMutationType
            // 
            this.cmbMutationType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbMutationType.FormattingEnabled = true;
            this.cmbMutationType.Items.AddRange(new object[] {
            "Swap",
            "Scramble",
            "Inverse"});
            this.cmbMutationType.Location = new System.Drawing.Point(6, 144);
            this.cmbMutationType.Name = "cmbMutationType";
            this.cmbMutationType.Size = new System.Drawing.Size(121, 21);
            this.cmbMutationType.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 122);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(75, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Mutation Type";
            // 
            // chart1
            // 
            this.chart1.BorderlineColor = System.Drawing.Color.Black;
            this.chart1.BorderlineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Solid;
            chartArea2.Name = "ChartArea1";
            this.chart1.ChartAreas.Add(chartArea2);
            legend2.Name = "Legend1";
            this.chart1.Legends.Add(legend2);
            this.chart1.Location = new System.Drawing.Point(154, 176);
            this.chart1.Name = "chart1";
            series2.ChartArea = "ChartArea1";
            series2.Legend = "Legend1";
            series2.Name = "Series1";
            this.chart1.Series.Add(series2);
            this.chart1.Size = new System.Drawing.Size(361, 300);
            this.chart1.TabIndex = 5;
            this.chart1.Text = "chart1";
            // 
            // PopulationChart
            // 
            this.PopulationChart.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.PopulationChart.Location = new System.Drawing.Point(521, 176);
            this.PopulationChart.Name = "PopulationChart";
            this.PopulationChart.Size = new System.Drawing.Size(357, 300);
            this.PopulationChart.TabIndex = 6;
            this.PopulationChart.TabStop = false;
            this.PopulationChart.Paint += new System.Windows.Forms.PaintEventHandler(this.PopulationChart_Paint);
            // 
            // resultlist
            // 
            this.resultlist.FormattingEnabled = true;
            this.resultlist.Location = new System.Drawing.Point(154, 10);
            this.resultlist.Name = "resultlist";
            this.resultlist.Size = new System.Drawing.Size(724, 160);
            this.resultlist.TabIndex = 7;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(895, 490);
            this.Controls.Add(this.resultlist);
            this.Controls.Add(this.PopulationChart);
            this.Controls.Add(this.chart1);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Phrase Evolution GUI";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.chart1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.PopulationChart)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ComboBox cmbPopulationSize;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox cmbElitismRate;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox cmbCrossoverType;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cmbMutationType;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.DataVisualization.Charting.Chart chart1;
        private System.Windows.Forms.PictureBox PopulationChart;
        private System.Windows.Forms.ListBox resultlist;
        private System.Windows.Forms.Button btnStart;
    }
}

