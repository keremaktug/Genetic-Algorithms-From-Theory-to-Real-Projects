using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using GA.Api.Events;
using GA.Api.Graphic;
using GA.Api.Types;
using GA.Api.Types.Enums;

namespace GA.Api.CardsProblem
{
    public partial class MainForm : Form
    {
        private static readonly Random rnd = new Random(32767);
        private static List<object> CardNumbers = new List<object>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
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
            cmbCrossoverType.SelectedIndex = 2;
            cmbElitismRate.SelectedIndex = 2;
            cmbMutationType.SelectedIndex = 0;
            cmbPopulationSize.SelectedIndex = 3;
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

            gs = gs = new GeneticSolver(100, 1000, elitism_rate / 10, 0.1, crossover_type)
            {
                GeneratorFunction = CardsGenerator,
                FitnessFunction = CalculateFitness
            };

            gs.IterationCompleted += Gs_IterationCompleted;
            gs.SolutionFound += Gs_SolutionFound;

            gs.Evolve();
        }

        public static Chromosome CardsGenerator()
        {
            var card_indices = new List<int>();

            for (int i = 0; i < CardNumbers.Count; i++) card_indices.Add(i);

            var cards_list = card_indices.OrderBy(x => rnd.Next()).ToList();

            var ch = new Chromosome();

            for (int i = 0; i < cards_list.Count; i++)
            {
                ch.Data.Add(CardNumbers[cards_list[i]]);
            }

            return ch;
        }

        public static double CalculateFitness(Chromosome chromosome)
        {
            var card1 = (int)chromosome.Data[0];
            var card2 = (int)chromosome.Data[1];
            var card3 = (int)chromosome.Data[2];
            var card4 = (int)chromosome.Data[3];
            var card5 = (int)chromosome.Data[4];
            var card6 = (int)chromosome.Data[5];
            var card7 = (int)chromosome.Data[6];
            var card8 = (int)chromosome.Data[7];
            var card9 = (int)chromosome.Data[8];
            var card10 = (int)chromosome.Data[9];

            return System.Math.Abs(36 - (card1 + card2 + card3 + card4 + card5)) + System.Math.Abs(360 - (card6 * card7 * card8 * card9 * card10));
        }

        private void Gs_SolutionFound(object sender, SolutionFoundEventArgs e)
        {
            resultlist.Items.Add("= Solution Found ==============");
            
            int val1 = (int)e.Solution.Data[0];
            int val2 = (int)e.Solution.Data[1];
            int val3 = (int)e.Solution.Data[2];
            int val4 = (int)e.Solution.Data[3];
            int val5 = (int)e.Solution.Data[4];
            int val6 = (int)e.Solution.Data[5];
            int val7 = (int)e.Solution.Data[6];
            int val8 = (int)e.Solution.Data[7];
            int val9 = (int)e.Solution.Data[8];
            int val10 = (int)e.Solution.Data[9];

            resultlist.Items.Add($"{val1} + {val2} + {val3} + {val4} + {val5} = {val1 + val2 + val3 + val4 + val5}");
            resultlist.Items.Add($"{val6} * {val7} * {val8} * {val9} * {val10} = {val6 * val7 * val8 * val9 * val10}");
        }

        private void Gs_IterationCompleted(object sender, IterationCompletedEventArgs e)
        {
            resultlist.Items.Add("= Iteration Completed =========");

            int val1 = (int)e.BestChromosome.Data[0];
            int val2 = (int)e.BestChromosome.Data[1];
            int val3 = (int)e.BestChromosome.Data[2];
            int val4 = (int)e.BestChromosome.Data[3];
            int val5 = (int)e.BestChromosome.Data[4];
            int val6 = (int)e.BestChromosome.Data[5];
            int val7 = (int)e.BestChromosome.Data[6];
            int val8 = (int)e.BestChromosome.Data[7];
            int val9 = (int)e.BestChromosome.Data[8];
            int val10 = (int)e.BestChromosome.Data[9];

            resultlist.Items.Add($"{val1} + {val2} + {val3} + {val4} + {val5} = {val1 + val2 + val3 + val4 + val5}");
            resultlist.Items.Add($"{val6} * {val7} * {val8} * {val9} * {val10} = {val6 * val7 * val8 * val9 * val10}");

            AddPointsToChart(e.IterationCount, e.BestChromosome.Fitness, e.AverageFitness);

            PopulationChart.Refresh();
            Application.DoEvents();
        }

        private void PopulationChart_Paint(object sender, PaintEventArgs e)
        {
            if (gs == null) return;
            if (gs.Population.Chromosomes.Count == 0) return;

            List<object> domain_values = new List<object>();

            for(int i = 1; i <= 10; i++)
                domain_values.Add(i);

            var gene_bars = GraphicHelper.GeneratePoolGraph(gs.Population, domain_values, PopulationChart.Width, PopulationChart.Height, 1f, 1f);

            foreach (KeyValuePair<RectangleF, Brush> kvp in gene_bars)
            {
                e.Graphics.FillRectangle(kvp.Value, kvp.Key);
            }
        }
    }
}
