using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GACore;

namespace _24.LunarLanderPolicy.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private GeneticSolver<double>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private LanderSimulation? _bestSimulation;
    private double[] _bestWeights = [];

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

                if (_bestSimulation?.Landed == true && result.BestFitness < 10)
                {
                    StatusTextBlock.Text = "Stable landing found";
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
        _bestWeights = [];
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        LandingScoreTextBlock.Text = "-";
        FinalStateTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        PopulationListBox.Items.Clear();
        DrawSimulation();
        DrawFitnessChart();
    }

    private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        UpdateParameterLabels();
    }

    private void SimulationCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawSimulation();

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
            new LunarLanderProblem(),
            new TournamentSelection<double>(),
            new UniformCrossover<double>(),
            new NeuralWeightMutation(),
            options,
            new Random(42));

        return true;
    }

    private void RenderResult(GenerationResult<double> result)
    {
        _bestWeights = result.BestChromosome.Genes.ToArray();
        _bestSimulation = LunarLanderProblem.Simulate(_bestWeights);
        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F2");
        LandingScoreTextBlock.Text = _bestSimulation.Landed ? "Landed" : "Not landed";
        FinalStateTextBlock.Text =
            $"x={_bestSimulation.FinalState.X:F2}, y={_bestSimulation.FinalState.Y:F2}\n" +
            $"vx={_bestSimulation.FinalState.Vx:F2}, vy={_bestSimulation.FinalState.Vy:F2}\n" +
            $"angle={_bestSimulation.FinalState.Angle:F2}, fuel={_bestSimulation.Fuel:F1}";
        StatusTextBlock.Text = "Running";

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            foreach (var chromosome in _solver.Population.Take(12))
            {
                PopulationListBox.Items.Add($"{chromosome.Fitness,8:F2}  w0={chromosome.Genes[0],6:F2} w1={chromosome.Genes[1],6:F2} w2={chromosome.Genes[2],6:F2}...");
            }
        }

        DrawSimulation();
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
        var pad = 34.0;

        SimulationCanvas.Children.Add(new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new LinearGradientBrush(Color.FromRgb(11, 16, 32), Color.FromRgb(30, 41, 59), 90)
        });

        var landingPad = new Rectangle
        {
            Width = 120,
            Height = 8,
            Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94))
        };
        Canvas.SetLeft(landingPad, width / 2 - 60);
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
            Opacity = 0.88
        };

        foreach (var state in _bestSimulation.Path)
        {
            trajectory.Points.Add(Project(state));
        }
        SimulationCanvas.Children.Add(trajectory);

        DrawLander(_bestSimulation.FinalState, Project(_bestSimulation.FinalState));
    }

    private void DrawLander(LanderState state, Point center)
    {
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
            RenderTransform = new TransformGroup
            {
                Children =
                {
                    new RotateTransform(state.Angle * 180 / Math.PI),
                    new TranslateTransform(center.X, center.Y)
                }
            }
        };
        SimulationCanvas.Children.Add(body);

        if (state.Y > 0.02)
        {
            var flame = new Polygon
            {
                Fill = new SolidColorBrush(Color.FromRgb(249, 115, 22)),
                Points = new PointCollection { new(-7, 12), new(7, 12), new(0, 34) },
                Opacity = 0.72,
                RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        new RotateTransform(state.Angle * 180 / Math.PI),
                        new TranslateTransform(center.X, center.Y)
                    }
                }
            };
            SimulationCanvas.Children.Add(flame);
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
