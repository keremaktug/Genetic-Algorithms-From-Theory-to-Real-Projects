using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GACore;

namespace _11.Rectangles.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];
    private readonly RectanglesProblem _problem = new();

    private GeneticSolver<RectanglePlacement>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private RectanglePlacement[] _bestPlacements = [];
    private IReadOnlyList<Chromosome<RectanglePlacement>> _lastPoolSnapshot = [];

    public MainWindow()
    {
        InitializeComponent();
        UpdateParameterLabels();
        DrawPacking();
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
            using var results = _solver!.Run().GetEnumerator();

            while (await MoveNextOnBackgroundThread(results, _evolutionCancellation.Token))
            {
                var result = results.Current;
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

            StatusTextBlock.Text = "Generation limit reached";
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

    private static Task<bool> MoveNextOnBackgroundThread(
        IEnumerator<GenerationResult<RectanglePlacement>> results,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return results.MoveNext();
        }, cancellationToken);
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        _evolutionCancellation?.Cancel();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _evolutionCancellation?.Cancel();
        _solver = null;
        _bestPlacements = [];
        _lastPoolSnapshot = [];
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        BoxAreaTextBlock.Text = "-";
        OverlapTextBlock.Text = "-";
        OutsideTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        PopulationListBox.Items.Clear();
        ChromosomePoolImage.Source = null;
        ChromosomePoolPlaceholderTextBlock.Visibility = Visibility.Visible;
        DrawPacking();
        DrawChromosomePool();
        DrawFitnessChart();
    }

    private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        UpdateParameterLabels();
    }

    private void PackingCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawPacking();
    }

    private void FitnessCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawFitnessChart();
    }

    private void ChromosomePoolHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawChromosomePool();
    }

    private bool TryCreateSolver()
    {
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

        _solver = new GeneticSolver<RectanglePlacement>(
            _problem,
            new TournamentSelection<RectanglePlacement>(),
            new UniformCrossover<RectanglePlacement>(),
            new RectanglesMutation(),
            options);

        return true;
    }

    private void RenderResult(GenerationResult<RectanglePlacement> result)
    {
        _bestPlacements = result.BestChromosome.Genes.ToArray();
        var evaluation = _problem.Evaluate(_bestPlacements);

        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F0");
        BoxAreaTextBlock.Text = evaluation.BoundingBoxArea.ToString();
        OverlapTextBlock.Text = evaluation.OverlapArea.ToString();
        OutsideTextBlock.Text = evaluation.OutsidePenalty.ToString();
        StatusTextBlock.Text = "Running";

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            _lastPoolSnapshot = _solver.Population
                .Take(180)
                .Select(chromosome => chromosome.Clone())
                .ToArray();

            foreach (var chromosome in _solver.Population.Take(10))
            {
                PopulationListBox.Items.Add($"{chromosome.Fitness,6:F0}  {string.Join(" ", chromosome.Genes.Take(4).Select(g => $"({g.X},{g.Y},{(g.Rotated ? 1 : 0)})"))}...");
            }
        }

        DrawPacking();
        DrawChromosomePool();
        DrawFitnessChart();
    }

    private void DrawChromosomePool()
    {
        if (_lastPoolSnapshot.Count == 0 ||
            ChromosomePoolHost.ActualWidth <= 2 ||
            ChromosomePoolHost.ActualHeight <= 2)
        {
            return;
        }

        var rectangleCount = RectanglesProblem.Shapes.Length;
        var chromosomeLength = rectangleCount * 3;
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
                var placements = _lastPoolSnapshot[row].Genes;
                var y = row * yStep;

                for (int rectangleIndex = 0; rectangleIndex < rectangleCount && rectangleIndex < placements.Length; rectangleIndex++)
                {
                    var placement = placements[rectangleIndex];
                    DrawPoolCell(context, rectangleIndex * 3, y, xStep, yStep, GetXBrush(placement.X));
                    DrawPoolCell(context, rectangleIndex * 3 + 1, y, xStep, yStep, GetYBrush(placement.Y));
                    DrawPoolCell(context, rectangleIndex * 3 + 2, y, xStep, yStep, placement.Rotated
                        ? FrozenBrush(Color.FromRgb(167, 139, 250))
                        : FrozenBrush(Color.FromRgb(229, 231, 235)));
                }
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();

        ChromosomePoolImage.Source = bitmap;
        ChromosomePoolPlaceholderTextBlock.Visibility = Visibility.Collapsed;
    }

    private static void DrawPoolCell(DrawingContext context, int column, double y, double xStep, double yStep, Brush brush)
    {
        var x = column * xStep;
        context.DrawRectangle(brush, null, new Rect(x, y, Math.Ceiling(xStep) + 1, Math.Ceiling(yStep) + 1));
    }

    private static Brush GetXBrush(int x)
    {
        var ratio = Math.Clamp(x / (double)Math.Max(1, RectanglesProblem.BoardWidth - 1), 0, 1);
        return FrozenBrush(Interpolate(
            Color.FromRgb(219, 234, 254),
            Color.FromRgb(37, 99, 235),
            ratio));
    }

    private static Brush GetYBrush(int y)
    {
        var ratio = Math.Clamp(y / (double)Math.Max(1, RectanglesProblem.BoardHeight - 1), 0, 1);
        return FrozenBrush(Interpolate(
            Color.FromRgb(220, 252, 231),
            Color.FromRgb(22, 163, 74),
            ratio));
    }

    private static Brush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color Interpolate(Color start, Color end, double ratio)
    {
        return Color.FromRgb(
            (byte)Math.Round(start.R + (end.R - start.R) * ratio),
            (byte)Math.Round(start.G + (end.G - start.G) * ratio),
            (byte)Math.Round(start.B + (end.B - start.B) * ratio));
    }


    private void DrawPacking()
    {
        PackingCanvas.Children.Clear();

        if (PackingCanvas.ActualWidth <= 0 || PackingCanvas.ActualHeight <= 0)
        {
            return;
        }

        var scale = Math.Min(PackingCanvas.ActualWidth / RectanglesProblem.BoardWidth, PackingCanvas.ActualHeight / RectanglesProblem.BoardHeight);
        var offsetX = (PackingCanvas.ActualWidth - RectanglesProblem.BoardWidth * scale) / 2.0;
        var offsetY = (PackingCanvas.ActualHeight - RectanglesProblem.BoardHeight * scale) / 2.0;
        var evaluation = _bestPlacements.Length == 0 ? null : _problem.Evaluate(_bestPlacements);

        DrawBoard(scale, offsetX, offsetY);

        if (_bestPlacements.Length == 0) return;

        var rects = _problem.ToPlacedRectangles(_bestPlacements);
        var overlapIds = _problem.GetOverlappingRectangleIds(rects);

        foreach (var placed in rects)
        {
            var color = RectanglesProblem.Shapes[placed.Index].Color;
            var rectangle = new Rectangle
            {
                Width = placed.Width * scale,
                Height = placed.Height * scale,
                Fill = new SolidColorBrush(color),
                Stroke = Brushes.Transparent,
                StrokeThickness = 0,
                Opacity = overlapIds.Contains(placed.Index) ? 0.68 : 0.86
            };

            Canvas.SetLeft(rectangle, offsetX + placed.X * scale);
            Canvas.SetTop(rectangle, offsetY + placed.Y * scale);
            PackingCanvas.Children.Add(rectangle);

            var label = new TextBlock
            {
                Text = RectanglesProblem.Shapes[placed.Index].Id.ToString(),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black
            };
            Canvas.SetLeft(label, offsetX + placed.X * scale + 4);
            Canvas.SetTop(label, offsetY + placed.Y * scale + 3);
            PackingCanvas.Children.Add(label);
        }

        if (evaluation is not null)
        {
            var bbox = evaluation.BoundingBox;
            var box = new Rectangle
            {
                Width = Math.Max(0, bbox.Right - bbox.Left) * scale,
                Height = Math.Max(0, bbox.Bottom - bbox.Top) * scale,
                Stroke = new SolidColorBrush(Color.FromRgb(23, 105, 224)),
                StrokeThickness = 3,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(box, offsetX + bbox.Left * scale);
            Canvas.SetTop(box, offsetY + bbox.Top * scale);
            PackingCanvas.Children.Add(box);
        }
    }

    private void DrawBoard(double scale, double offsetX, double offsetY)
    {
        var board = new Rectangle
        {
            Width = RectanglesProblem.BoardWidth * scale,
            Height = RectanglesProblem.BoardHeight * scale,
            Fill = Brushes.White,
            Stroke = new SolidColorBrush(Color.FromRgb(180, 187, 196)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(board, offsetX);
        Canvas.SetTop(board, offsetY);
        PackingCanvas.Children.Add(board);

        for (int x = 0; x <= RectanglesProblem.BoardWidth; x++)
        {
            var line = new Line
            {
                X1 = offsetX + x * scale,
                X2 = offsetX + x * scale,
                Y1 = offsetY,
                Y2 = offsetY + RectanglesProblem.BoardHeight * scale,
                Stroke = new SolidColorBrush(Color.FromRgb(238, 241, 245)),
                StrokeThickness = 1
            };
            PackingCanvas.Children.Add(line);
        }

        for (int y = 0; y <= RectanglesProblem.BoardHeight; y++)
        {
            var line = new Line
            {
                X1 = offsetX,
                X2 = offsetX + RectanglesProblem.BoardWidth * scale,
                Y1 = offsetY + y * scale,
                Y2 = offsetY + y * scale,
                Stroke = new SolidColorBrush(Color.FromRgb(238, 241, 245)),
                StrokeThickness = 1
            };
            PackingCanvas.Children.Add(line);
        }
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
