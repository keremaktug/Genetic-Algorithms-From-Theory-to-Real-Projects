using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GACore;

namespace _25.NeuralArchitectureSearch.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private GeneticSolver<double>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private LanderSimulation? _bestSimulation;
    private NetworkArchitecture? _bestArchitecture;
    private double[] _bestGenes = [];

    public MainWindow()
    {
        InitializeComponent();
        UpdateParameterLabels();
        DrawSimulation();
        DrawNetwork();
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

                if (_bestSimulation?.Landed == true && result.BestFitness < 14)
                {
                    StatusTextBlock.Text = "Compact stable policy found";
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
        _bestArchitecture = null;
        _bestGenes = [];
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        LandingScoreTextBlock.Text = "-";
        ArchitectureTextBlock.Text = "-";
        FinalStateTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        NetworkCaptionTextBlock.Text = "architecture genes";
        PopulationListBox.Items.Clear();
        DrawSimulation();
        DrawNetwork();
        DrawFitnessChart();
    }

    private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        UpdateParameterLabels();
    }

    private void SimulationCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawSimulation();

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
            FitnessGoal = FitnessGoal.Minimize,
            TargetFitness = null,
            TournamentSize = tournamentSize
        };

        _solver = new GeneticSolver<double>(
            new NasLanderProblem(),
            new TournamentSelection<double>(),
            new UniformCrossover<double>(),
            new NasMutation(),
            options,
            new Random(43));

        return true;
    }

    private void RenderResult(GenerationResult<double> result)
    {
        _bestGenes = result.BestChromosome.Genes.ToArray();
        _bestArchitecture = NasLanderProblem.DecodeArchitecture(_bestGenes);
        _bestSimulation = NasLanderProblem.Simulate(_bestGenes);
        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F2");
        LandingScoreTextBlock.Text = _bestSimulation.Landed ? "Landed" : "Not landed";
        ArchitectureTextBlock.Text =
            $"{_bestArchitecture}\n" +
            $"layers={_bestArchitecture.LayerCount}, weights={_bestArchitecture.UsedWeightCount}\n" +
            $"arch genes=({_bestGenes[0]:F2}, {_bestGenes[1]:F2}, {_bestGenes[2]:F2})";
        FinalStateTextBlock.Text =
            $"x={_bestSimulation.FinalState.X:F2}, y={_bestSimulation.FinalState.Y:F2}\n" +
            $"vx={_bestSimulation.FinalState.Vx:F2}, vy={_bestSimulation.FinalState.Vy:F2}\n" +
            $"angle={_bestSimulation.FinalState.Angle:F2}, fuel={_bestSimulation.Fuel:F1}";
        StatusTextBlock.Text = "Running";
        NetworkCaptionTextBlock.Text = _bestArchitecture.ToString();

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            foreach (var chromosome in _solver.Population.Take(12))
            {
                var architecture = NasLanderProblem.DecodeArchitecture(chromosome.Genes);
                PopulationListBox.Items.Add($"{chromosome.Fitness,8:F2}  {architecture,-16} weights={architecture.UsedWeightCount,3}");
            }
        }

        DrawSimulation();
        DrawNetwork();
        DrawFitnessChart();
    }

    private void DrawSimulation()
    {
        SimulationCanvas.Children.Clear();

        if (SimulationCanvas.ActualWidth <= 0 || SimulationCanvas.ActualHeight <= 0)
        {
            return;
        }

        var width = SimulationCanvas.ActualWidth;
        var height = SimulationCanvas.ActualHeight;
        var groundY = height * 0.84;
        var pad = 28.0;

        SimulationCanvas.Children.Add(new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new LinearGradientBrush(Color.FromRgb(11, 16, 32), Color.FromRgb(30, 41, 59), 90)
        });

        AddStars(width, height);

        var landingPad = new Rectangle
        {
            Width = 112,
            Height = 8,
            Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94))
        };
        Canvas.SetLeft(landingPad, width / 2 - 56);
        Canvas.SetTop(landingPad, groundY);
        SimulationCanvas.Children.Add(landingPad);

        SimulationCanvas.Children.Add(new Line
        {
            X1 = pad,
            X2 = width - pad,
            Y1 = groundY + 8,
            Y2 = groundY + 8,
            Stroke = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            StrokeThickness = 2
        });

        if (_bestSimulation is null)
        {
            return;
        }

        Point Project(LanderState state) => new(
            width / 2 + state.X * (width * 0.38),
            groundY - state.Y * (height * 0.68));

        var trajectory = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            StrokeThickness = 2.2,
            Opacity = 0.9
        };

        foreach (var state in _bestSimulation.Path)
        {
            trajectory.Points.Add(Project(state));
        }

        SimulationCanvas.Children.Add(trajectory);
        DrawLander(_bestSimulation.FinalState, Project(_bestSimulation.FinalState));
    }

    private void AddStars(double width, double height)
    {
        var random = new Random(7);
        for (int i = 0; i < 42; i++)
        {
            var star = new Ellipse
            {
                Width = 1.5,
                Height = 1.5,
                Fill = Brushes.White,
                Opacity = 0.35 + random.NextDouble() * 0.45
            };
            Canvas.SetLeft(star, random.NextDouble() * width);
            Canvas.SetTop(star, random.NextDouble() * height * 0.72);
            SimulationCanvas.Children.Add(star);
        }
    }

    private void DrawLander(LanderState state, Point center)
    {
        var transform = new TransformGroup
        {
            Children =
            {
                new RotateTransform(state.Angle * 180 / Math.PI),
                new TranslateTransform(center.X, center.Y)
            }
        };

        var body = new Polygon
        {
            Fill = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            Stroke = Brushes.White,
            StrokeThickness = 2,
            Points = new PointCollection
            {
                new(-16, -12),
                new(16, -12),
                new(12, 12),
                new(-12, 12)
            },
            RenderTransform = transform
        };
        SimulationCanvas.Children.Add(body);

        if (state.Y > 0.02)
        {
            var flame = new Polygon
            {
                Fill = new SolidColorBrush(Color.FromRgb(249, 115, 22)),
                Points = new PointCollection { new(-7, 12), new(7, 12), new(0, 34) },
                Opacity = 0.72,
                RenderTransform = transform
            };
            SimulationCanvas.Children.Add(flame);
        }
    }

    private void DrawNetwork()
    {
        NetworkCanvas.Children.Clear();

        if (NetworkCanvas.ActualWidth <= 0 || NetworkCanvas.ActualHeight <= 0)
        {
            return;
        }

        var architecture = _bestArchitecture ?? new NetworkArchitecture(2, 8, 6, 143);
        var layers = architecture.LayerSizes.ToArray();
        var width = NetworkCanvas.ActualWidth;
        var height = NetworkCanvas.ActualHeight;
        var left = 38.0;
        var right = width - 38;
        var top = 34.0;
        var bottom = height - 34;
        var layerGap = layers.Length == 1 ? 0 : (right - left) / (layers.Length - 1);
        var points = new List<List<Point>>();

        for (int layer = 0; layer < layers.Length; layer++)
        {
            var count = layers[layer];
            var x = left + layer * layerGap;
            var visibleCount = Math.Min(count, 12);
            var nodeGap = visibleCount == 1 ? 0 : (bottom - top) / (visibleCount - 1);
            var layerPoints = new List<Point>();

            for (int node = 0; node < visibleCount; node++)
            {
                var y = visibleCount == 1 ? height / 2 : top + node * nodeGap;
                layerPoints.Add(new Point(x, y));
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
                        StrokeThickness = 0.6,
                        Opacity = 0.42
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
                    Width = 22,
                    Height = 22,
                    Fill = new SolidColorBrush(fill),
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(node, point.X - 11);
                Canvas.SetTop(node, point.Y - 11);
                NetworkCanvas.Children.Add(node);
            }

            var label = new TextBlock
            {
                Text = layer == 0 ? "inputs" : layer == points.Count - 1 ? "outputs" : $"{layers[layer]} neurons",
                Foreground = new SolidColorBrush(Color.FromRgb(89, 97, 109)),
                FontSize = 12,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(label, points[layer][0].X - 36);
            Canvas.SetTop(label, height - 24);
            NetworkCanvas.Children.Add(label);
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
