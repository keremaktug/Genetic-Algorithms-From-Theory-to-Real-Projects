using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GACore;

namespace _16.GraphColoring.Wpf;

public partial class MainWindow : Window
{
    private static readonly Color[] NodeColors =
    [
        Color.FromRgb(59, 130, 246),
        Color.FromRgb(34, 197, 94),
        Color.FromRgb(249, 115, 22),
        Color.FromRgb(168, 85, 247),
        Color.FromRgb(236, 72, 153),
        Color.FromRgb(20, 184, 166)
    ];

    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private GraphColoringProblem? _problem;
    private GeneticSolver<int>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private IReadOnlyList<Chromosome<int>> _lastPoolSnapshot = [];
    private int[] _bestColors = [];

    public MainWindow()
    {
        InitializeComponent();
        UpdateParameterLabels();
        _problem = GraphColoringProblem.CreateDenseGraph(3);
        _bestColors = Enumerable.Repeat(0, _problem.Nodes.Count).ToArray();
        DrawGraph();
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCreateSolver()) return;

        SetRunningState(true);
        _evolutionCancellation = new CancellationTokenSource();
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        try
        {
            foreach (var result in _solver!.Run())
            {
                RenderResult(result);

                var delay = (int)DelaySlider.Value;
                if (delay > 0)
                {
                    await Task.Delay(delay, _evolutionCancellation.Token);
                }
                else
                {
                    await Task.Yield();
                    _evolutionCancellation.Token.ThrowIfCancellationRequested();
                }
            }
        }
        catch (TaskCanceledException)
        {
            StatusTextBlock.Text = "Paused";
        }
        finally
        {
            SetRunningState(false);
        }
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        _evolutionCancellation?.Cancel();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _evolutionCancellation?.Cancel();
        _solver = null;
        _lastPoolSnapshot = [];
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        if (_problem is not null)
        {
            _bestColors = Enumerable.Repeat(0, _problem.Nodes.Count).ToArray();
        }

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        AverageFitnessTextBlock.Text = "-";
        ConflictEdgesTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        PopulationListBox.Items.Clear();
        ChromosomePoolImage.Source = null;
        ChromosomePoolPlaceholderTextBlock.Visibility = Visibility.Visible;
        DrawGraph();
        DrawChromosomePool();
        DrawFitnessChart();
    }

    private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        UpdateParameterLabels();
    }

    private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawGraph();
    }

    private void ChromosomePoolHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawChromosomePool();
    }

    private void FitnessCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawFitnessChart();
    }

    private bool TryCreateSolver()
    {
        if (!int.TryParse(ColorCountTextBox.Text, out var colorCount) || colorCount < 2 || colorCount > NodeColors.Length)
        {
            MessageBox.Show($"Color count must be between 2 and {NodeColors.Length}.", "Invalid color count", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(PopulationSizeTextBox.Text, out var populationSize) || populationSize < 4)
        {
            MessageBox.Show("Population size must be at least 4.", "Invalid population size", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(MaxGenerationsTextBox.Text, out var maxGenerations) || maxGenerations < 1)
        {
            MessageBox.Show("Maximum generations must be at least 1.", "Invalid generation limit", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(TournamentSizeTextBox.Text, out var tournamentSize) || tournamentSize < 1)
        {
            MessageBox.Show("Tournament size must be at least 1.", "Invalid tournament size", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        _problem = DatasetComboBox.SelectedIndex == 1
            ? GraphColoringProblem.CreateDenseGraph(colorCount)
            : GraphColoringProblem.CreateMapGraph(colorCount);
        _bestColors = Enumerable.Repeat(0, _problem.Nodes.Count).ToArray();

        var options = new SolverOptions
        {
            PopulationSize = populationSize,
            MaxGenerations = maxGenerations,
            ElitismRate = ElitismRateSlider.Value,
            MutationRate = MutationRateSlider.Value,
            FitnessGoal = FitnessGoal.Minimize,
            TargetFitness = null,
            TournamentSize = tournamentSize
        };

        _solver = new GeneticSolver<int>(
            _problem,
            new TournamentSelection<int>(),
            new UniformCrossover<int>(),
            new RandomResetMutation<int>(random => random.Next(colorCount)),
            options);

        return true;
    }

    private void RenderResult(GenerationResult<int> result)
    {
        if (_problem is null) return;

        _bestColors = result.BestChromosome.Genes.ToArray();
        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        var conflicts = _problem.GetConflictingEdges(_bestColors);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F0");
        AverageFitnessTextBlock.Text = result.AverageFitness.ToString("F2");
        ConflictEdgesTextBlock.Text = conflicts.Count.ToString();
        StatusTextBlock.Text = result.BestFitness <= 0
            ? "Valid coloring found, continuing"
            : "Running";

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            _lastPoolSnapshot = _solver.Population
                .Take(180)
                .Select(chromosome => chromosome.Clone())
                .ToArray();

            foreach (var chromosome in _solver.Population.Take(12))
            {
                PopulationListBox.Items.Add($"{chromosome.Fitness,3:F0}  {string.Join("", chromosome.Genes)}");
            }
        }

        DrawGraph();
        DrawChromosomePool();
        DrawFitnessChart();
    }

    private void DrawGraph()
    {
        GraphCanvas.Children.Clear();

        if (_problem is null || GraphCanvas.ActualWidth <= 0 || GraphCanvas.ActualHeight <= 0)
        {
            return;
        }

        var width = GraphCanvas.ActualWidth;
        var height = GraphCanvas.ActualHeight;
        var padding = 48.0;
        var conflictEdges = _problem.GetConflictingEdges(_bestColors).ToHashSet();

        Point Project(GraphNode node)
        {
            return new Point(
                padding + node.X * Math.Max(1, width - padding * 2),
                padding + node.Y * Math.Max(1, height - padding * 2));
        }

        foreach (var edge in _problem.Edges)
        {
            var from = Project(_problem.Nodes[edge.From]);
            var to = Project(_problem.Nodes[edge.To]);
            var isConflict = conflictEdges.Contains(edge);

            GraphCanvas.Children.Add(new Line
            {
                X1 = from.X,
                Y1 = from.Y,
                X2 = to.X,
                Y2 = to.Y,
                Stroke = isConflict ? Brushes.Firebrick : new SolidColorBrush(Color.FromRgb(178, 186, 198)),
                StrokeThickness = isConflict ? 3.0 : 1.4,
                Opacity = isConflict ? 0.95 : 0.70
            });
        }

        for (int i = 0; i < _problem.Nodes.Count; i++)
        {
            var node = _problem.Nodes[i];
            var point = Project(node);
            var colorIndex = _bestColors.Length > i ? Math.Clamp(_bestColors[i], 0, NodeColors.Length - 1) : 0;

            var circle = new Ellipse
            {
                Width = 46,
                Height = 46,
                Fill = new SolidColorBrush(NodeColors[colorIndex]),
                Stroke = Brushes.White,
                StrokeThickness = 3
            };

            Canvas.SetLeft(circle, point.X - 23);
            Canvas.SetTop(circle, point.Y - 23);
            GraphCanvas.Children.Add(circle);

            var label = new TextBlock
            {
                Text = node.Name,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Width = 46,
                TextAlignment = TextAlignment.Center
            };

            Canvas.SetLeft(label, point.X - 23);
            Canvas.SetTop(label, point.Y - 9);
            GraphCanvas.Children.Add(label);
        }
    }

    private void DrawChromosomePool()
    {
        if (_lastPoolSnapshot.Count == 0 ||
            ChromosomePoolHost.ActualWidth <= 2 ||
            ChromosomePoolHost.ActualHeight <= 2)
        {
            return;
        }

        var chromosomeLength = _lastPoolSnapshot[0].Genes.Length;
        var visibleRows = _lastPoolSnapshot.Count;
        var width = Math.Max(1, (int)ChromosomePoolHost.ActualWidth - 2);
        var height = Math.Max(1, (int)ChromosomePoolHost.ActualHeight - 2);
        var xStep = width / (double)chromosomeLength;
        var yStep = height / (double)visibleRows;
        var visual = new DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

            for (int row = 0; row < visibleRows; row++)
            {
                var genes = _lastPoolSnapshot[row].Genes;
                var y = row * yStep;

                for (int column = 0; column < chromosomeLength; column++)
                {
                    var colorIndex = Math.Clamp(genes[column], 0, NodeColors.Length - 1);
                    var brush = new SolidColorBrush(NodeColors[colorIndex]);
                    brush.Freeze();
                    var x = column * xStep;
                    context.DrawRectangle(brush, null, new Rect(x, y, Math.Ceiling(xStep) + 1, Math.Ceiling(yStep) + 1));
                }
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();

        ChromosomePoolImage.Source = bitmap;
        ChromosomePoolPlaceholderTextBlock.Visibility = Visibility.Collapsed;
    }

    private void DrawFitnessChart()
    {
        FitnessCanvas.Children.Clear();

        if (_bestFitnessHistory.Count < 2 || FitnessCanvas.ActualWidth <= 0 || FitnessCanvas.ActualHeight <= 0)
        {
            return;
        }

        var width = FitnessCanvas.ActualWidth;
        var height = FitnessCanvas.ActualHeight;
        var maxFitness = Math.Max(_averageFitnessHistory.Max(), _bestFitnessHistory.Max());

        DrawSeries(_averageFitnessHistory, maxFitness, width, height, Color.FromRgb(217, 75, 65));
        DrawSeries(_bestFitnessHistory, maxFitness, width, height, Color.FromRgb(23, 105, 224));
    }

    private void DrawSeries(List<double> values, double maxFitness, double width, double height, Color color)
    {
        var line = new Polyline
        {
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2,
            SnapsToDevicePixels = true
        };

        for (int i = 0; i < values.Count; i++)
        {
            var x = values.Count == 1 ? 0 : i * width / (values.Count - 1);
            var y = maxFitness == 0 ? height : height - (values[i] / maxFitness * height);
            line.Points.Add(new Point(x, y));
        }

        FitnessCanvas.Children.Add(line);
    }

    private void SetRunningState(bool isRunning)
    {
        StartButton.IsEnabled = !isRunning;
        PauseButton.IsEnabled = isRunning;
        ResetButton.IsEnabled = !isRunning;
        DatasetComboBox.IsEnabled = !isRunning;
        ColorCountTextBox.IsEnabled = !isRunning;
        PopulationSizeTextBox.IsEnabled = !isRunning;
        MaxGenerationsTextBox.IsEnabled = !isRunning;
        TournamentSizeTextBox.IsEnabled = !isRunning;

        if (isRunning)
        {
            StatusTextBlock.Text = "Running";
        }
    }

    private void UpdateParameterLabels()
    {
        ElitismRateTextBlock.Text = $"{ElitismRateSlider.Value:P0}";
        MutationRateTextBlock.Text = $"{MutationRateSlider.Value:P0}";
        DelayTextBlock.Text = $"{(int)DelaySlider.Value} ms";
    }
}
