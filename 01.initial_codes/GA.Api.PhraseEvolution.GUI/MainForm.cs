using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using GA.Api.Events;
using GA.Api.Graphic;
using GA.Api.Types;
using GA.Api.Types.Enums;

namespace GA.Api.PhraseEvolution.GUI
{
    public partial class MainForm : Form
    {
        private static readonly Random rnd = new Random();
        private static string letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ 1234567890,.";
        private static string phrase = "Those who live in glass houses should not throw stones";

        private GeneticSolver gs = null;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            InitComboboxes();
            InitChart();
        }

        private void InitComboboxes()
        {
            cmbCrossoverType.SelectedIndex = 1;
            cmbElitismRate.SelectedIndex = 2;
            cmbMutationType.SelectedIndex = 0;
            cmbPopulationSize.SelectedIndex = 5;
        }

        private void InitChart()
        {
            chart1.Series.Clear();
            chart1.Series.Add("BestFitness");
            chart1.Series.Add("AverageFitness");
            chart1.Series["BestFitness"].ChartType = SeriesChartType.Spline;
            chart1.Series["AverageFitness"].ChartType = SeriesChartType.Spline;
            chart1.Series["BestFitness"].Points.Clear();
            chart1.Series["AverageFitness"].Points.Clear();
        }

        private void Clear()
        {
            resultlist.Items.Clear();
            chart1.Series["BestFitness"].Points.Clear();
            chart1.Series["AverageFitness"].Points.Clear();
        }

        private void Decode(Chromosome c)
        {
            var s = new StringBuilder();

            foreach (var d in c.Data)
            {
                s.Append(d.ToString());
            }

            resultlist.Items.Add($"{s.ToString()}");
            int visibleItems = resultlist.ClientSize.Height / resultlist.ItemHeight;
            resultlist.TopIndex = System.Math.Max(resultlist.Items.Count - visibleItems + 1, 0);
        }

        private void AddPointsToChart(int iteration, double best_fitness, double average_fitness)
        {
            chart1.Series["BestFitness"].Points.Add(new DataPoint(iteration, best_fitness));
            chart1.Series["AverageFitness"].Points.Add(new DataPoint(iteration, average_fitness));
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            Clear();

            int pop_size_factor = Int32.Parse(cmbPopulationSize.Items[cmbPopulationSize.SelectedIndex].ToString());

            CrossoverType crossover_type = CrossoverType.OnePointCrossover;

            switch (cmbCrossoverType.SelectedIndex)
            {
                case 0: crossover_type = CrossoverType.OnePointCrossover; break;
                case 1: crossover_type = CrossoverType.UniformCrossover; break;
                case 2: crossover_type = CrossoverType.PMX; break;
            }

            double elitism_rate = Double.Parse(cmbElitismRate.SelectedItem.ToString(), CultureInfo.InvariantCulture);

            gs = new GeneticSolver(1024 * pop_size_factor, 1000, elitism_rate / 10, 0.01, crossover_type);
            gs.GeneratorFunction = PhraseGenerator;
            gs.FitnessFunction = CalculateFitness;
            gs.IterationCompleted += Gs_IterationCompleted;
            gs.SolutionFound += Gs_SolutionFound;

            gs.Evolve();
        }

        public Chromosome PhraseGenerator()
        {
            var characters = new List<object>();

            for (int i = 0; i < phrase.Length; i++)
            {
                var character_index = rnd.Next(0, letters.Length);
                characters.Add(letters[character_index]);
            }

            return new Chromosome(characters);
        }

        public static double CalculateFitness(Chromosome chromosome)
        {
            var f = 0.0f;

            for (int i = 0; i < phrase.Length; i++)
            {
                f += System.Math.Abs(phrase[i] - (char)chromosome.Data[i]);
            }

            return f;
        }

        private void Gs_SolutionFound(object sender, SolutionFoundEventArgs e)
        {
            Chromosome c = e.Solution;

            var s = new StringBuilder();

            foreach (var d in c.Data)
            {
                s.Append(d.ToString());
            }

            Decode(c);

            MessageBox.Show($"Iteration Count : { e.IterationCount } | {s.ToString()}");
        }

        private void Gs_IterationCompleted(object sender, IterationCompletedEventArgs e)
        {
            AddPointsToChart(e.IterationCount, e.BestChromosome.Fitness, e.AverageFitness);
            Decode(e.BestChromosome);

            PopulationChart.Refresh();
            Application.DoEvents();
        }

        private void PopulationChart_Paint(object sender, PaintEventArgs e)
        {
            if (gs == null) return;
            if (gs.Population.Chromosomes.Count == 0) return;

            List<object> domain_values = new List<object>();

            foreach (char ch in letters)
                domain_values.Add(ch);

            var gene_bars = GraphicHelper.GeneratePoolGraph(gs.Population, domain_values, PopulationChart.Width, PopulationChart.Height, 0.6f, 0.4f);

            foreach (KeyValuePair<RectangleF, Brush> kvp in gene_bars)
            {
                e.Graphics.FillRectangle(kvp.Value, kvp.Key);
            }
        }
    }
}
