using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using GA.Api.Events;
using GA.Api.Types;
using GA.Api.Types.Enums;

namespace GA.Api._8Queens
{
    public partial class MainForm : Form
    {
        private static readonly Random rnd = new Random();
        private static GeneticSolver g = null;
        public static List<RectangleStruct> RectangleStructs;
        public static Chromosome BestSolution;
        public int factor = 20;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            CreateData();
            Init();
        }

        public void Init()
        {
            g = new GeneticSolver(64 * 1024, 500, 0.35, 0.1, CrossoverType.UniformCrossover);
            g.GeneratorFunction = VariablesGenerator;
            g.FitnessFunction = CalculateFitness;
            g.IterationCompleted += G_IterationCompleted;
            g.SolutionFound += G_SolutionFound;            
        }

        void CreateData()
        {
            RectangleStructs = new List<RectangleStruct>();
            RectangleStructs.Add(new RectangleStruct(1, 8, 7, Brushes.Blue));
            RectangleStructs.Add(new RectangleStruct(2, 5, 3, Brushes.Red));
            RectangleStructs.Add(new RectangleStruct(3, 2, 6, Brushes.Green));
            RectangleStructs.Add(new RectangleStruct(4, 6, 4, Brushes.Brown));
            RectangleStructs.Add(new RectangleStruct(5, 3, 3, Brushes.Chartreuse));
            RectangleStructs.Add(new RectangleStruct(6, 6, 5, Brushes.DarkBlue));
            RectangleStructs.Add(new RectangleStruct(7, 1, 2, Brushes.DarkCyan));
            RectangleStructs.Add(new RectangleStruct(8, 2, 1, Brushes.DarkOrange));
            RectangleStructs.Add(new RectangleStruct(9, 1, 3, Brushes.DarkOrchid));
            RectangleStructs.Add(new RectangleStruct(10, 1, 1, Brushes.BurlyWood));
            RectangleStructs.Add(new RectangleStruct(11, 2, 1, Brushes.Cyan));

            BestSolution = new Chromosome();
            BestSolution.Data.Add(2); BestSolution.Data.Add(1); BestSolution.Data.Add(0);
            BestSolution.Data.Add(4); BestSolution.Data.Add(3); BestSolution.Data.Add(0);
            BestSolution.Data.Add(3); BestSolution.Data.Add(7); BestSolution.Data.Add(0);
            BestSolution.Data.Add(4); BestSolution.Data.Add(9); BestSolution.Data.Add(0);
            BestSolution.Data.Add(9); BestSolution.Data.Add(11); BestSolution.Data.Add(0);
            BestSolution.Data.Add(12); BestSolution.Data.Add(3); BestSolution.Data.Add(0);
            BestSolution.Data.Add(11); BestSolution.Data.Add(5); BestSolution.Data.Add(0);
            BestSolution.Data.Add(12); BestSolution.Data.Add(9); BestSolution.Data.Add(0);
            BestSolution.Data.Add(17); BestSolution.Data.Add(9); BestSolution.Data.Add(0);
            BestSolution.Data.Add(1); BestSolution.Data.Add(1); BestSolution.Data.Add(0);
            BestSolution.Data.Add(10); BestSolution.Data.Add(10); BestSolution.Data.Add(0);
        }

        public Chromosome VariablesGenerator()
        {
            int size = RectangleStructs.Count;

            var variables = new List<object>();

            for (var i = 0; i < size; i++)
            {
                var x = rnd.Next(0, 19);
                var y = rnd.Next(0, 19);
                var o = rnd.Next(0, 2);
                variables.Add(x);
                variables.Add(y);
                variables.Add(o);
            }

            return new Chromosome(variables);
        }

        public static double CalculateFitness(Chromosome chromosome)
        {
            BestSolution = chromosome;
            var overlapping_area = CalculateOverlappingArea();
            var bbox = CalculateBoundingBox();
            int area = (bbox.Item3 - bbox.Item1) * (bbox.Item4 - bbox.Item2);
            var illegal_rects = CalculateIllegalRectangles();
            return (overlapping_area * 5) + (area * 2) + (illegal_rects * 10);
        }

        private void G_SolutionFound(object sender, SolutionFoundEventArgs e)
        {
            MessageBox.Show("Solution Found");            
        }

        private void G_IterationCompleted(object sender, IterationCompletedEventArgs e)
        {
            BestSolution = e.BestChromosome;
            AddPointsToChart(e.IterationCount, e.BestChromosome.Fitness, e.AverageFitness);

            var bbox = CalculateBoundingBox();
            int area = (bbox.Item3 - bbox.Item1) * (bbox.Item4 - bbox.Item2);
            label1.Text = $"BBox Area : {area}";
            label2.Text = $"Overlapping Area : {CalculateOverlappingArea().ToString()}";
            label3.Text = $"Illegal Rectangle Count : {CalculateIllegalRectangles().ToString()}";

            map.Refresh();
            chart1.Update();
            Application.DoEvents();
        }

        private void DrawGrid(Graphics g)
        {
            for (int i = 1; i < 19; i++)
            {
                for (int j = 1; j < 19; j++)
                {
                    g.FillEllipse(Brushes.Black, new RectangleF((i * 20) - 1.25f, (j * 20) - 1.25f, 2.5f, 2.5f));
                }
            }
        }

        private void DrawRectangles(Graphics g)
        {
            var j = 0;
            for (var i = 0; i < BestSolution.Data.Count; i += 3)
            {
                var rect = RectangleStructs[j];
                var x = (int)BestSolution.Data[i];
                var y = (int)BestSolution.Data[i + 1];
                var o = (int)BestSolution.Data[i + 2];
                var w = rect.Width;
                var h = rect.Height;
                if (o == 0)
                    g.FillRectangle(rect.Brush, x * factor, y * factor, rect.Width * factor, rect.Height * factor);
                else
                    g.FillRectangle(rect.Brush, x * factor, y * factor, rect.Height * factor, rect.Width * factor);
                j++;
            }
        }

        private void DrawBoundingBox(Graphics g)
        {
            var bbox = CalculateBoundingBox();
            g.DrawRectangle(Pens.Red, bbox.Item1 * factor, bbox.Item2 * factor, (bbox.Item3 - bbox.Item1) * factor, (bbox.Item4 - bbox.Item2) * factor);
        }

        private void btnEvolve_Click(object sender, EventArgs e)
        {
            Clear();
            g.Evolve();                        
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

        private void map_Paint(object sender, PaintEventArgs e)
        {
            DrawGrid(e.Graphics);
            DrawRectangles(e.Graphics);
            DrawBoundingBox(e.Graphics);
        }

        private static int CalculateIllegalRectangles()
        {
            int r = 0;
            var j = 0;
            for (var i = 0; i < BestSolution.Data.Count; i += 3)
            {
                var rect = RectangleStructs[j];
                var id = rect.Id;
                var x = (int)BestSolution.Data[i];
                var y = (int)BestSolution.Data[i + 1];
                var o = (int)BestSolution.Data[i + 2];
                var w = rect.Width;
                var h = rect.Height;

                if ((x + w) > 19) r++;
                if ((y + h) > 19) r++;
                
                j++;
            }

            return r;
        }

        private static Tuple<int, int, int, int> CalculateBoundingBox()
        {
            int min_x = int.MaxValue;
            int min_y = int.MaxValue;
            int max_x = int.MinValue;
            int max_y = int.MinValue;

            var j = 0;
            for (var i = 0; i < BestSolution.Data.Count; i += 3)
            {
                var rect = RectangleStructs[j];
                var x = (int)BestSolution.Data[i];
                var y = (int)BestSolution.Data[i + 1];
                var o = (int)BestSolution.Data[i + 2];
                var w = rect.Width;
                var h = rect.Height;

                var rx1 = x;
                var ry1 = y;
                var rx2 = o == 0 ? x + w : x + h;
                var ry2 = o == 0 ? y + h : y + w;

                if (rx1 < min_x)
                    min_x = rx1;

                if (ry1 < min_y)
                    min_y = ry1;

                if (rx2 > max_x)
                    max_x = rx2;

                if (ry2 > max_y)
                    max_y = ry2;

                j++;
            }

            return new Tuple<int, int, int, int>(min_x, min_y, max_x, max_y);
        }

        private static int CalculateOverlappingArea()
        {
            var r = 0;

            var all = new List<Tuple<int, int, int, int, int>>();

            var j = 0;
            for (var i = 0; i < BestSolution.Data.Count; i += 3)
            {
                var rect = RectangleStructs[j];
                var id = rect.Id;
                var x = (int)BestSolution.Data[i];
                var y = (int)BestSolution.Data[i + 1];
                var o = (int)BestSolution.Data[i + 2];
                var w = rect.Width;
                var h = rect.Height;

                if (o == 0)
                    all.Add(new Tuple<int, int, int, int, int>(id, x, y, w, h));
                else
                    all.Add(new Tuple<int, int, int, int, int>(id, x, y, h, w));
                j++;
            }

            for (var a = 0; a < all.Count; a++)
            {
                for (var b = 0; b < all.Count; b++)
                {
                    var id1 = all[a].Item1;
                    var x1 = all[a].Item2;
                    var y1 = all[a].Item3;
                    var w1 = all[a].Item4;
                    var h1 = all[a].Item5;
                    var id2 = all[b].Item1;
                    var x2 = all[b].Item2;
                    var y2 = all[b].Item3;
                    var w2 = all[b].Item4;
                    var h2 = all[b].Item5;
                    if (id1 != id2)
                        r += OverlapArea(x1, y1, w1, h1, x2, y2, w2, h2);
                }
            }

            return (int)(r * 0.5);
        }

        private static int OverlapArea(int x1, int y1, int w1, int h1, int x2, int y2, int w2, int h2)
        {
            int left = System.Math.Max(x1, x2);
            int right = System.Math.Min(x1 + w1, x2 + w2);
            int top = System.Math.Max(y1, y2);
            int bottom = System.Math.Min(y1 + h1, y2 + h2);
            int overlapWidth = System.Math.Max(0, right - left);
            int overlapHeight = System.Math.Max(0, bottom - top);
            return overlapWidth * overlapHeight;
        }
    }
}
