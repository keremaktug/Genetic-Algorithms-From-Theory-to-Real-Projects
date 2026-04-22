using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using GA.Api.Events;
using GA.Api.Types;
using GA.Api.Types.Enums;

namespace GA.Api._8Queens
{
    public partial class MainForm : Form
    {
        private static readonly Random Random = new Random(32767);
        private GeneticSolver gs = null;
        private Bitmap chessboard_image = new Bitmap("images\\chessboard.png");
        private Bitmap queen_image = new Bitmap("images\\queen.png");

        private static int[,] board = new int[8, 8];
        private static List<Point> queen_positions = new List<Point>();
        private static List<Point> forbidden_positions = new List<Point>();

        public MainForm()
        {
            InitializeComponent();
        }

        private void btnEvolve_Click(object sender, EventArgs e)
        {
            Clear();

            int pop_size_factor = 8;
            int elitism_rate = 2;
            var crossover_type = CrossoverType.OnePointCrossover;

            queen_positions = new List<Point>();

            gs = new GeneticSolver(1024 * pop_size_factor, 1000, elitism_rate / 10, 0.01, crossover_type)
            {
                GeneratorFunction = SolutionGenerator,
                FitnessFunction = CalculateFitness
            };

            gs.IterationCompleted += Gs_IterationCompleted;
            gs.SolutionFound += Gs_SolutionFound;

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

        public Chromosome SolutionGenerator()
        {
            var queens = new List<object>();

            var board = new Point[8, 8];

            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 8; j++)
                    board[i, j] = new Point(i, j);

            var indices = new List<int>();

            for (int i = 0; i < 8 * 8; i++) indices.Add(i);

            var selected = indices.OrderBy(x => Random.Next()).Take(8).ToList();

            int a = 0;

            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    if (selected.Contains(a)) queens.Add((object)board[i, j]);
                    a++;
                }
            }

            return new Chromosome(queens);
        }

        public double CalculateFitness(Chromosome chromosome)
        {
            var f = 0;

            queen_positions = new List<Point>();

            foreach (var queen_data in chromosome.Data)
            {
                queen_positions.Add((Point)queen_data);
            }

            //Horizontal/Vertical check

            for (int i = 0; i < queen_positions.Count; i++)
            {
                var queen = queen_positions[i];

                for (int y = 0; y < 8; y++)
                {
                    var p = new Point(queen.X, y);
                    if (p != queen && queen_positions.Any(x => x == p)) f++;
                }

                for (int x = 0; x < 8; x++)
                {
                    var p = new Point(x, queen.Y);
                    if (p != queen && queen_positions.Any(a => a == p)) f++;
                }
            }

            //Diagonal check

            for (int i = 0; i < queen_positions.Count; i++)
            {
                for (int j = 0; j < queen_positions.Count; j++)
                {
                    if (i != j)
                    {
                        var dx = System.Math.Abs(queen_positions[i].X - queen_positions[j].X);
                        var dy = System.Math.Abs(queen_positions[i].Y - queen_positions[j].Y);
                        if (dx == dy) f++;
                    }
                }
            }

            return f;
        }

        private void Gs_SolutionFound(object sender, SolutionFoundEventArgs e)
        {
            var solution = e.Solution;

            queen_positions = new List<Point>();

            foreach (var pt in solution.Data)
            {
                queen_positions.Add((Point)pt);
            }

            Chessboard.Refresh();

            MessageBox.Show("Solution found");
        }

        private void Gs_IterationCompleted(object sender, IterationCompletedEventArgs e)
        {
            var best_chromosome = e.BestChromosome;

            queen_positions = new List<Point>();

            foreach (var pt in best_chromosome.Data)
            {
                queen_positions.Add((Point)pt);
            }

            AddPointsToChart(e.IterationCount, best_chromosome.Fitness, e.AverageFitness);

            Application.DoEvents();
            Chessboard.Refresh();
        }

        private void Chessboard_Paint(object sender, PaintEventArgs e)
        {
            DrawBoard(e.Graphics);

            foreach (var pt in queen_positions)
            {
                DrawQueen(e.Graphics, pt.X, pt.Y);
            }
        }

        private void DrawBoard(Graphics g)
        {
            g.DrawImage(chessboard_image, 0, 0, 333, 333);
        }

        private void DrawQueen(Graphics g, int x, int y)
        {
            var x_delta = (x * 42f) + 5;
            var y_delta = (y * 42f) + 5;
            g.DrawImage(queen_image, x_delta, y_delta, 27.5f, 27.5f);
        }
    }
}
