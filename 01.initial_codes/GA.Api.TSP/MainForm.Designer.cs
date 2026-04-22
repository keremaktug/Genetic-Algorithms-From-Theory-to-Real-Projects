namespace GA.Api.TSP
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
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend1 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series1 = new System.Windows.Forms.DataVisualization.Charting.Series();
            this.map = new System.Windows.Forms.PictureBox();
            this.PopulationChart = new System.Windows.Forms.PictureBox();
            this.chart1 = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.cmbPopulationSize = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.cmbElitismRate = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.cmbCrossoverType = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.cmbMutationType = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.btnEvolve = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.map)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.PopulationChart)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.chart1)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // map
            // 
            this.map.BackColor = System.Drawing.Color.White;
            this.map.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.map.Location = new System.Drawing.Point(166, 2);
            this.map.Name = "map";
            this.map.Size = new System.Drawing.Size(355, 341);
            this.map.TabIndex = 1;
            this.map.TabStop = false;
            this.map.Paint += new System.Windows.Forms.PaintEventHandler(this.map_Paint);
            // 
            // PopulationChart
            // 
            this.PopulationChart.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.PopulationChart.Location = new System.Drawing.Point(531, 2);
            this.PopulationChart.Name = "PopulationChart";
            this.PopulationChart.Size = new System.Drawing.Size(355, 341);
            this.PopulationChart.TabIndex = 5;
            this.PopulationChart.TabStop = false;
            this.PopulationChart.Paint += new System.Windows.Forms.PaintEventHandler(this.PopulationChart_Paint);
            // 
            // chart1
            // 
            this.chart1.BorderlineColor = System.Drawing.Color.Black;
            this.chart1.BorderlineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Solid;
            chartArea1.Name = "ChartArea1";
            this.chart1.ChartAreas.Add(chartArea1);
            legend1.Name = "Legend1";
            this.chart1.Legends.Add(legend1);
            this.chart1.Location = new System.Drawing.Point(6, 349);
            this.chart1.Name = "chart1";
            series1.ChartArea = "ChartArea1";
            series1.Legend = "Legend1";
            series1.Name = "Series1";
            this.chart1.Series.Add(series1);
            this.chart1.Size = new System.Drawing.Size(880, 132);
            this.chart1.TabIndex = 6;
            this.chart1.Text = "chart1";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.cmbPopulationSize);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.cmbElitismRate);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.cmbCrossoverType);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.cmbMutationType);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.btnEvolve);
            this.groupBox1.Location = new System.Drawing.Point(6, 2);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(154, 341);
            this.groupBox1.TabIndex = 17;
            this.groupBox1.TabStop = false;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(16, 307);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(73, 13);
            this.label1.TabIndex = 25;
            this.label1.Text = "Total Length :";
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
            this.cmbPopulationSize.Location = new System.Drawing.Point(16, 215);
            this.cmbPopulationSize.Name = "cmbPopulationSize";
            this.cmbPopulationSize.Size = new System.Drawing.Size(121, 21);
            this.cmbPopulationSize.TabIndex = 24;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(16, 190);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(113, 13);
            this.label4.TabIndex = 23;
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
            this.cmbElitismRate.Location = new System.Drawing.Point(16, 99);
            this.cmbElitismRate.Name = "cmbElitismRate";
            this.cmbElitismRate.Size = new System.Drawing.Size(121, 21);
            this.cmbElitismRate.TabIndex = 22;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(16, 74);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(62, 13);
            this.label3.TabIndex = 21;
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
            this.cmbCrossoverType.Location = new System.Drawing.Point(16, 41);
            this.cmbCrossoverType.Name = "cmbCrossoverType";
            this.cmbCrossoverType.Size = new System.Drawing.Size(121, 21);
            this.cmbCrossoverType.TabIndex = 20;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(16, 16);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(81, 13);
            this.label2.TabIndex = 19;
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
            this.cmbMutationType.Location = new System.Drawing.Point(16, 157);
            this.cmbMutationType.Name = "cmbMutationType";
            this.cmbMutationType.Size = new System.Drawing.Size(121, 21);
            this.cmbMutationType.TabIndex = 18;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(16, 132);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(75, 13);
            this.label5.TabIndex = 17;
            this.label5.Text = "Mutation Type";
            // 
            // btnEvolve
            // 
            this.btnEvolve.Location = new System.Drawing.Point(16, 248);
            this.btnEvolve.Name = "btnEvolve";
            this.btnEvolve.Size = new System.Drawing.Size(123, 47);
            this.btnEvolve.TabIndex = 16;
            this.btnEvolve.Text = "Evolve";
            this.btnEvolve.UseVisualStyleBackColor = true;
            this.btnEvolve.Click += new System.EventHandler(this.btnEvolve_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(895, 490);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.chart1);
            this.Controls.Add(this.PopulationChart);
            this.Controls.Add(this.map);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Traveling Salesman Problem";
            this.Load += new System.EventHandler(this.MainForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.map)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.PopulationChart)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.chart1)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox map;
        private System.Windows.Forms.PictureBox PopulationChart;
        private System.Windows.Forms.DataVisualization.Charting.Chart chart1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ComboBox cmbPopulationSize;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox cmbElitismRate;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox cmbCrossoverType;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cmbMutationType;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button btnEvolve;
        private System.Windows.Forms.Label label1;
    }
}

