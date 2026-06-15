using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GACore;

namespace _26.SnakeNeuroevolution.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private GeneticSolver<double>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private SnakeSimulation? _bestSimulation;
    private int _displayFrameIndex;

    public MainWindow()
    {
        InitializeComponent();
        UpdateParameterLabels();
        DrawBoard();
        DrawNetwork();
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCreateSolver()) return;

        SetRunningState(true);
        _evolutionCancellation = new CancellationTokenSource();
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();
        _displayFrameIndex = 0;

        try
        {
            foreach (var result in _solver!.Run())
            {
                RenderResult(result);

                if (_bestSimulation?.FoodEaten >= 8)
                {
                    StatusTextBlock.Text = "Strong snake policy found";
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
        _bestSimulation = null;
        _displayFrameIndex = 0;
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        FoodTextBlock.Text = "-";
        StepsTextBlock.Text = "-";
        FinalStateTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        BoardCaptionTextBlock.Text = "best policy replay";
        PopulationListBox.Items.Clear();
        DrawBoard();
        DrawNetwork();
        DrawFitnessChart();
    }

    private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        UpdateParameterLabels();
    }

    private void GameCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawBoard();

    private void NetworkCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawNetwork();

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
            FitnessGoal = FitnessGoal.Maximize,
            TargetFitness = null,
            TournamentSize = tournamentSize
        };

        _solver = new GeneticSolver<double>(
            new SnakeProblem(),
            new TournamentSelection<double>(),
            new UniformCrossover<double>(),
            new SnakeWeightMutation(),
            options,
            new Random(44));

        return true;
    }

    private void RenderResult(GenerationResult<double> result)
    {
        _bestSimulation = SnakeProblem.Simulate(result.BestChromosome.Genes);
        _displayFrameIndex = _bestSimulation.Frames.Count - 1;
        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F1");
        FoodTextBlock.Text = _bestSimulation.FoodEaten.ToString();
        StepsTextBlock.Text = _bestSimulation.Steps.ToString();
        FinalStateTextBlock.Text =
            $"head=({_bestSimulation.FinalFrame.Snake[0].X},{_bestSimulation.FinalFrame.Snake[0].Y})\n" +
            $"length={_bestSimulation.FinalFrame.Snake.Count}\n" +
            $"crashed={_bestSimulation.Crashed}";
        StatusTextBlock.Text = "Running";
        BoardCaptionTextBlock.Text = $"frame {_displayFrameIndex + 1} / {_bestSimulation.Frames.Count}";

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            foreach (var chromosome in _solver.Population.Take(12))
            {
                var sim = SnakeProblem.Simulate(chromosome.Genes);
                PopulationListBox.Items.Add($"{chromosome.Fitness,8:F1}  food={sim.FoodEaten,2}  steps={sim.Steps,3}  w0={chromosome.Genes[0],6:F2}");
            }
        }

        DrawBoard();
        DrawNetwork();
        DrawFitnessChart();
    }

    private void DrawBoard()
    {
        GameCanvas.Children.Clear();

        if (GameCanvas.ActualWidth <= 0 || GameCanvas.ActualHeight <= 0)
        {
            return;
        }

        var size = Math.Min(GameCanvas.ActualWidth, GameCanvas.ActualHeight) - 20;
        var cell = size / SnakeProblem.BoardSize;
        var left = (GameCanvas.ActualWidth - size) / 2;
        var top = (GameCanvas.ActualHeight - size) / 2;

        GameCanvas.Children.Add(new Rectangle
        {
            Width = size,
            Height = size,
            Fill = new SolidColorBrush(Color.FromRgb(236, 253, 245)),
            Stroke = new SolidColorBrush(Color.FromRgb(167, 243, 208)),
            StrokeThickness = 1
        });
        Canvas.SetLeft(GameCanvas.Children[^1], left);
        Canvas.SetTop(GameCanvas.Children[^1], top);

        for (int i = 1; i < SnakeProblem.BoardSize; i++)
        {
            var offset = i * cell;
            GameCanvas.Children.Add(new Line
            {
                X1 = left + offset,
                X2 = left + offset,
                Y1 = top,
                Y2 = top + size,
                Stroke = new SolidColorBrush(Color.FromRgb(209, 250, 229)),
                StrokeThickness = 1
            });
            GameCanvas.Children.Add(new Line
            {
                X1 = left,
                X2 = left + size,
                Y1 = top + offset,
                Y2 = top + offset,
                Stroke = new SolidColorBrush(Color.FromRgb(209, 250, 229)),
                StrokeThickness = 1
            });
        }

        if (_bestSimulation is null)
        {
            DrawCell(new GridPoint(SnakeProblem.BoardSize / 2, SnakeProblem.BoardSize / 2), cell, left, top, Color.FromRgb(22, 163, 74), 0.95);
            DrawCell(new GridPoint(SnakeProblem.BoardSize / 2 + 3, SnakeProblem.BoardSize / 2), cell, left, top, Color.FromRgb(239, 68, 68), 1.0);
            return;
        }

        var frame = _bestSimulation.Frames[Math.Clamp(_displayFrameIndex, 0, _bestSimulation.Frames.Count - 1)];

        DrawCell(frame.Food, cell, left, top, Color.FromRgb(239, 68, 68), 1.0);

        for (int i = frame.Snake.Count - 1; i >= 0; i--)
        {
            var color = i == 0 ? Color.FromRgb(21, 128, 61) : Color.FromRgb(34, 197, 94);
            DrawCell(frame.Snake[i], cell, left, top, color, i == 0 ? 1.0 : 0.88);
        }

        if (frame.Crashed)
        {
            var label = new TextBlock
            {
                Text = "CRASH",
                Foreground = new SolidColorBrush(Color.FromRgb(153, 27, 27)),
                FontWeight = FontWeights.Bold,
                FontSize = 24
            };
            Canvas.SetLeft(label, left + 12);
            Canvas.SetTop(label, top + 10);
            GameCanvas.Children.Add(label);
        }
    }

    private void DrawCell(GridPoint point, double cell, double left, double top, Color color, double opacity)
    {
        var rect = new Rectangle
        {
            Width = Math.Max(2, cell - 3),
            Height = Math.Max(2, cell - 3),
            RadiusX = 4,
            RadiusY = 4,
            Fill = new SolidColorBrush(color),
            Opacity = opacity
        };
        Canvas.SetLeft(rect, left + point.X * cell + 1.5);
        Canvas.SetTop(rect, top + point.Y * cell + 1.5);
        GameCanvas.Children.Add(rect);
    }

    private void DrawNetwork()
    {
        NetworkCanvas.Children.Clear();

        if (NetworkCanvas.ActualWidth <= 0 || NetworkCanvas.ActualHeight <= 0)
        {
            return;
        }

        var layers = new[] { SnakeProblem.InputCount, SnakeProblem.HiddenCount, SnakeProblem.OutputCount };
        var width = NetworkCanvas.ActualWidth;
        var height = NetworkCanvas.ActualHeight;
        var left = 40.0;
        var right = width - 40;
        var top = 32.0;
        var bottom = height - 34;
        var points = new List<List<Point>>();

        for (int layer = 0; layer < layers.Length; layer++)
        {
            var x = left + layer * ((right - left) / (layers.Length - 1));
            var count = layers[layer];
            var gap = count == 1 ? 0 : (bottom - top) / (count - 1);
            var layerPoints = new List<Point>();

            for (int node = 0; node < count; node++)
            {
                layerPoints.Add(new Point(x, count == 1 ? height / 2 : top + node * gap));
            }

            points.Add(layerPoints);
        }

        for (int layer = 0; layer < points.Count - 1; layer++)
        {
            foreach (var a in points[layer])
            {
                foreach (var b in points[layer + 1])
                {
                    NetworkCanvas.Children.Add(new Line
                    {
                        X1 = a.X,
                        Y1 = a.Y,
                        X2 = b.X,
                        Y2 = b.Y,
                        Stroke = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                        StrokeThickness = 0.55,
                        Opacity = 0.38
                    });
                }
            }
        }

        for (int layer = 0; layer < points.Count; layer++)
        {
            var fill = layer == 0
                ? Color.FromRgb(59, 130, 246)
                : layer == points.Count - 1
                    ? Color.FromRgb(249, 115, 22)
                    : Color.FromRgb(34, 197, 94);

            foreach (var point in points[layer])
            {
                var node = new Ellipse
                {
                    Width = 18,
                    Height = 18,
                    Fill = new SolidColorBrush(fill),
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(node, point.X - 9);
                Canvas.SetTop(node, point.Y - 9);
                NetworkCanvas.Children.Add(node);
            }
        }
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
