using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GACore;

namespace _21.Rastrigin.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private GeneticSolver<double>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private IReadOnlyList<Chromosome<double>> _population = [];
    private double[] _bestGenes = [];

    public MainWindow()
    {
        InitializeComponent();
        UpdateParameterLabels();
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

                if (result.BestFitness <= 0.001)
                {
                    StatusTextBlock.Text = "Near global minimum";
                    break;
                }

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

    private void PauseButton_Click(object sender, RoutedEventArgs e) => _evolutionCancellation?.Cancel();

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _evolutionCancellation?.Cancel();
        _solver = null;
        _population = [];
        _bestGenes = [];
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        XTextBlock.Text = "-";
        YTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        PopulationListBox.Items.Clear();
        DrawSurface();
        DrawFitnessChart();
    }

    private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        UpdateParameterLabels();
    }

    private void SurfaceCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawSurface();

    private void FitnessCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawFitnessChart();

    private bool TryCreateSolver()
    {
        if (!int.TryParse(PopulationSizeTextBox.Text, out var populationSize) || populationSize < 4 ||
            !int.TryParse(MaxGenerationsTextBox.Text, out var maxGenerations) || maxGenerations < 1 ||
            !int.TryParse(TournamentSizeTextBox.Text, out var tournamentSize) || tournamentSize < 1)
        {
            MessageBox.Show("Population, generations and tournament values must be valid positive numbers.", "Invalid solver parameters", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

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

        _solver = new GeneticSolver<double>(
            new RastriginProblem(),
            new TournamentSelection<double>(),
            new UniformCrossover<double>(),
            new RealValueMutation(),
            options,
            new Random(42));

        return true;
    }

    private void RenderResult(GenerationResult<double> result)
    {
        _bestGenes = result.BestChromosome.Genes.ToArray();
        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F5");
        XTextBlock.Text = _bestGenes[0].ToString("F4");
        YTextBlock.Text = _bestGenes[1].ToString("F4");
        StatusTextBlock.Text = "Running";

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            _population = _solver.Population.Select(chromosome => chromosome.Clone()).ToArray();

            foreach (var chromosome in _solver.Population.Take(12))
            {
                PopulationListBox.Items.Add($"{chromosome.Fitness,9:F4}  x={chromosome.Genes[0],7:F3}  y={chromosome.Genes[1],7:F3}");
            }
        }

        DrawSurface();
        DrawFitnessChart();
    }

    private void DrawSurface()
    {
        SurfaceCanvas.Children.Clear();

        if (SurfaceCanvas.ActualWidth <= 0 || SurfaceCanvas.ActualHeight <= 0)
        {
            return;
        }

        var size = Math.Min(SurfaceCanvas.ActualWidth - 96, SurfaceCanvas.ActualHeight - 58);
        size = Math.Max(120, size);
        var left = (SurfaceCanvas.ActualWidth - size) / 2;
        var top = (SurfaceCanvas.ActualHeight - size) / 2 - 4;
        const int cells = 80;
        var cell = size / cells;

        for (int row = 0; row < cells; row++)
        {
            for (int col = 0; col < cells; col++)
            {
                var x = RastriginProblem.Min + col / (double)(cells - 1) * (RastriginProblem.Max - RastriginProblem.Min);
                var y = RastriginProblem.Max - row / (double)(cells - 1) * (RastriginProblem.Max - RastriginProblem.Min);
                var value = Math.Min(80, RastriginProblem.Evaluate(x, y));
                var color = HeatColor(value / 80.0);

                var rect = new Rectangle
                {
                    Width = cell + 0.5,
                    Height = cell + 0.5,
                    Fill = new SolidColorBrush(color),
                    StrokeThickness = 0
                };
                Canvas.SetLeft(rect, left + col * cell);
                Canvas.SetTop(rect, top + row * cell);
                SurfaceCanvas.Children.Add(rect);
            }
        }

        DrawAxisLine(left + size / 2, top, left + size / 2, top + size);
        DrawAxisLine(left, top + size / 2, left + size, top + size / 2);
        DrawFrame(left, top, size);
        DrawAxisLabels(left, top, size);
        DrawLegend(left + size + 18, top + 8);

        foreach (var chromosome in _population.Take(250))
        {
            DrawPoint(chromosome.Genes[0], chromosome.Genes[1], Brushes.White, new SolidColorBrush(Color.FromRgb(15, 23, 42)), 5, left, top, size, 0.72);
        }

        if (_bestGenes.Length == 2)
        {
            DrawPoint(0, 0, Brushes.Gold, Brushes.Black, 12, left, top, size, 1.0);
            AddCanvasText("global min", ProjectX(0, left, size) + 9, ProjectY(0, top, size) - 24, Brushes.Black, 12, FontWeights.SemiBold);

            DrawPoint(_bestGenes[0], _bestGenes[1], Brushes.Black, Brushes.White, 14, left, top, size, 1.0);
            AddCanvasText("best", ProjectX(_bestGenes[0], left, size) + 10, ProjectY(_bestGenes[1], top, size) + 4, Brushes.Black, 12, FontWeights.SemiBold);
        }
    }

    private void DrawFrame(double left, double top, double size)
    {
        var frame = new Rectangle
        {
            Width = size,
            Height = size,
            Fill = Brushes.Transparent,
            Stroke = new SolidColorBrush(Color.FromRgb(32, 36, 42)),
            StrokeThickness = 1.4
        };
        Canvas.SetLeft(frame, left);
        Canvas.SetTop(frame, top);
        SurfaceCanvas.Children.Add(frame);
    }

    private void DrawAxisLabels(double left, double top, double size)
    {
        AddCanvasText("x = -5.12", left - 26, top + size + 8, Brushes.Black, 12, FontWeights.Normal);
        AddCanvasText("x = 0", left + size / 2 - 12, top + size + 8, Brushes.Black, 12, FontWeights.Normal);
        AddCanvasText("x = 5.12", left + size - 34, top + size + 8, Brushes.Black, 12, FontWeights.Normal);
        AddCanvasText("y = 5.12", left - 58, top - 4, Brushes.Black, 12, FontWeights.Normal);
        AddCanvasText("y = 0", left - 38, top + size / 2 - 8, Brushes.Black, 12, FontWeights.Normal);
        AddCanvasText("y = -5.12", left - 62, top + size - 12, Brushes.Black, 12, FontWeights.Normal);
    }

    private void DrawLegend(double left, double top)
    {
        AddCanvasText("Value", left, top - 22, Brushes.Black, 13, FontWeights.SemiBold);

        for (int i = 0; i < 80; i++)
        {
            var rect = new Rectangle
            {
                Width = 16,
                Height = 2.5,
                Fill = new SolidColorBrush(HeatColor(i / 79.0)),
                StrokeThickness = 0
            };
            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, top + i * 2.5);
            SurfaceCanvas.Children.Add(rect);
        }

        AddCanvasText("low", left + 22, top - 2, Brushes.Black, 12, FontWeights.Normal);
        AddCanvasText("high", left + 22, top + 188, Brushes.Black, 12, FontWeights.Normal);
    }

    private void DrawAxisLine(double x1, double y1, double x2, double y2)
    {
        SurfaceCanvas.Children.Add(new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            StrokeThickness = 1
        });
    }

    private void DrawPoint(double x, double y, Brush fill, Brush stroke, double size, double left, double top, double surfaceSize, double opacity)
    {
        var px = ProjectX(x, left, surfaceSize);
        var py = ProjectY(y, top, surfaceSize);
        var ellipse = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = 1.6,
            Opacity = opacity
        };
        Canvas.SetLeft(ellipse, px - size / 2);
        Canvas.SetTop(ellipse, py - size / 2);
        SurfaceCanvas.Children.Add(ellipse);
    }

    private static double ProjectX(double x, double left, double surfaceSize)
    {
        return left + (x - RastriginProblem.Min) / (RastriginProblem.Max - RastriginProblem.Min) * surfaceSize;
    }

    private static double ProjectY(double y, double top, double surfaceSize)
    {
        return top + (RastriginProblem.Max - y) / (RastriginProblem.Max - RastriginProblem.Min) * surfaceSize;
    }

    private void AddCanvasText(string text, double left, double top, Brush foreground, double fontSize, FontWeight weight)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = foreground,
            FontSize = fontSize,
            FontWeight = weight
        };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        SurfaceCanvas.Children.Add(label);
    }

    private static Color HeatColor(double t)
    {
        t = Math.Clamp(t, 0, 1);
        var start = Color.FromRgb(29, 78, 216);
        var midLow = Color.FromRgb(34, 197, 94);
        var midHigh = Color.FromRgb(245, 158, 11);
        var end = Color.FromRgb(185, 28, 28);

        if (t < 0.34)
        {
            return Interpolate(start, midLow, t / 0.34);
        }

        if (t < 0.68)
        {
            return Interpolate(midLow, midHigh, (t - 0.34) / 0.34);
        }

        return Interpolate(midHigh, end, (t - 0.68) / 0.32);
    }

    private static Color Interpolate(Color a, Color b, double t)
    {
        return Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    private void DrawFitnessChart()
    {
        FitnessCanvas.Children.Clear();
        if (_bestFitnessHistory.Count < 2 || FitnessCanvas.ActualWidth <= 0 || FitnessCanvas.ActualHeight <= 0) return;

        var width = FitnessCanvas.ActualWidth;
        var height = FitnessCanvas.ActualHeight;
        var maxFitness = Math.Max(_averageFitnessHistory.Max(), _bestFitnessHistory.Max());
        DrawSeries(_averageFitnessHistory, maxFitness, width, height, Color.FromRgb(217, 75, 65));
        DrawSeries(_bestFitnessHistory, maxFitness, width, height, Color.FromRgb(23, 105, 224));
    }

    private void DrawSeries(List<double> values, double maxFitness, double width, double height, Color color)
    {
        var line = new Polyline { Stroke = new SolidColorBrush(color), StrokeThickness = 2, SnapsToDevicePixels = true };
        for (int i = 0; i < values.Count; i++)
        {
            var x = values.Count == 1 ? 0 : i * width / (values.Count - 1);
            var y = maxFitness == 0 ? height : height - values[i] / maxFitness * height;
            line.Points.Add(new Point(x, y));
        }
        FitnessCanvas.Children.Add(line);
    }

    private void SetRunningState(bool isRunning)
    {
        StartButton.IsEnabled = !isRunning;
        PauseButton.IsEnabled = isRunning;
        ResetButton.IsEnabled = !isRunning;
        PopulationSizeTextBox.IsEnabled = !isRunning;
        MaxGenerationsTextBox.IsEnabled = !isRunning;
        TournamentSizeTextBox.IsEnabled = !isRunning;
        if (isRunning) StatusTextBlock.Text = "Running";
    }

    private void UpdateParameterLabels()
    {
        ElitismRateTextBlock.Text = $"{ElitismRateSlider.Value:P0}";
        MutationRateTextBlock.Text = $"{MutationRateSlider.Value:P0}";
        DelayTextBlock.Text = $"{(int)DelaySlider.Value} ms";
    }
}
