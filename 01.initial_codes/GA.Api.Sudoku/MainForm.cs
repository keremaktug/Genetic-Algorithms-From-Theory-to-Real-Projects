using GA.Api.Graphic;
using GA.Api.Math;
using GA.Api.Types;
using GA.Api.Types.Enums;
using System.Windows.Forms.DataVisualization.Charting;

namespace GA.Api.Sudoku
{
    public partial class MainForm : Form
    {
        GeneticSolver gs = null;

        Random rnd = new Random(32767);

        SudokuData data = new SudokuData(Puzzles.Data);

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            grid.Set(data);
            gs = new GeneticSolver(8 * 1024, 10000, 0.95, 0.5, CrossoverType.UniformCrossover);
            gs.MutationType = MutationType.Swap;
            gs.GeneratorFunction = VariablesGenerator;
            gs.FitnessFunction = CalculateFitness;
            gs.IterationCompleted += G_IterationCompleted;
            gs.SolutionFound += G_SolutionFound;

            Clear();
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////

        public Chromosome VariablesGenerator()
        {
            var variables = new List<object>();

            var values = data.ValuesWillBePlaced;

            RandomGenerator.Shuffle(values);

            for (int i = 0; i < values.Count; i++)
            {
                variables.Add(values[i]);
            }

            return new Chromosome(variables);
        }

        public double CalculateFitness(Chromosome chromosome)
        {
            var r = 0.0f;

            var decoded = Decode(chromosome);

            for (int i = 0; i < 9; i++)
            {
                var row_dup = decoded.GetRowDuplicationCount(i);
                r += row_dup;
            }

            for (int i = 0; i < 9; i++)
            {
                var col_dup = decoded.GetColDuplicationCount(i);
                r += col_dup;
            }

            for (int i = 0; i < 9; i++)
            {
                var box_dup = decoded.GetBoxDuplicationCount(i);
                r += box_dup;
            }

            return r;
        }

        public SudokuData Decode(Chromosome chromosome)
        {
            var decoded = new SudokuData();

            int k = 0;

            for (int row = 0; row < 9; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    var p_data = data.Get(row, col);

                    if (p_data.IsEmpty)
                    {
                        decoded.Set(row, col, new SudokuValue((int)chromosome.Data[k], true));
                        k++;
                    }
                    else
                    {
                        decoded.Set(row, col, new SudokuValue(p_data.Value, false));
                    }
                }
            }

            return decoded;
        }

        private void G_IterationCompleted(object? sender, Events.IterationCompletedEventArgs e)
        {
            var decoded = Decode(e.BestChromosome);

            for (int row = 0; row < 9; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    if (decoded.Get(row, col).IsEmpty)
                    {
                        grid.SetValue(row, col, decoded.Get(row, col).Value);
                    }
                }
            }

            lblAverageFitness.Text = $"Avg : {e.AverageFitness.ToString("###.####")} Best {e.BestChromosome.Fitness}";
            AddPointsToChart(e.IterationCount, e.BestChromosome.Fitness, e.AverageFitness);

            pictureBox1.Refresh();
            Application.DoEvents();
        }

        private void G_SolutionFound(object? sender, Events.SolutionFoundEventArgs e)
        {
            MessageBox.Show("Solution found");

            var decoded = Decode(e.Solution);

            grid.Set(decoded);

            lblAverageFitness.Text = $"Best {e.Solution.Fitness}";

            Application.DoEvents();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Clear();
            gs.Evolve();
        }

        private void Clear()
        {
            chart1.Series.Clear();
            chart1.Series.Add("BestFitness");
            chart1.Series.Add("AverageFitness");
            chart1.Series["BestFitness"].ChartType = SeriesChartType.Spline;
            chart1.Series["AverageFitness"].ChartType = SeriesChartType.Spline;
            chart1.Series["BestFitness"].Points.Clear();
            chart1.Series["AverageFitness"].Points.Clear();
        }

        private void AddPointsToChart(int iteration, double best_fitness, double average_fitness)
        {
            chart1.Series["BestFitness"].Points.Add(new DataPoint(iteration, best_fitness));
            chart1.Series["AverageFitness"].Points.Add(new DataPoint(iteration, average_fitness));
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (gs == null) return;
            if (gs.Population.Chromosomes.Count == 0) return;

            List<object> domain_values = new List<object>();

            var v = data.ValuesWillBePlaced;

            for (int i = 0; i < v.Count; i++)
            {
                domain_values.Add(v[i]);
            }

            var gene_bars = GraphicHelper.GeneratePoolGraph(gs.Population, domain_values, pictureBox1.Width, pictureBox1.Height, 1f, 1f);

            foreach (KeyValuePair<RectangleF, Brush> kvp in gene_bars)
            {
                e.Graphics.FillRectangle(kvp.Value, kvp.Key);
            }
        }
    }
}