using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GACore;

namespace _22.GearTrain.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private GeneticSolver<int>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private int[] _bestGears = [20, 20, 20, 20];
    private double _targetRatio = 0.144279;

    public MainWindow()
    {
        InitializeComponent();
        UpdateParameterLabels();
        DrawGears();
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

                if (result.BestFitness <= 0.0001)
                {
                    StatusTextBlock.Text = "Target ratio matched";
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
        _bestGears = [20, 20, 20, 20];
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        ActualRatioTextBlock.Text = "-";
        TargetRatioDisplayTextBlock.Text = "-";
        GearsTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        PopulationListBox.Items.Clear();
        DrawGears();
        DrawFitnessChart();
    }

    private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        UpdateParameterLabels();
    }

    private void GearCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawGears();

    private void FitnessCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawFitnessChart();

    private bool TryCreateSolver()
    {
        if (!double.TryParse(TargetRatioTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _targetRatio) || _targetRatio <= 0)
        {
            MessageBox.Show("Target ratio must be a positive number. Use dot as decimal separator.", "Invalid target ratio", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(MinTeethTextBox.Text, out var minTeeth) ||
            !int.TryParse(MaxTeethTextBox.Text, out var maxTeeth) ||
            minTeeth < 2 ||
            maxTeeth <= minTeeth)
        {
            MessageBox.Show("Gear tooth range is invalid.", "Invalid gear range", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

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

        _solver = new GeneticSolver<int>(
            new GearTrainProblem(_targetRatio, minTeeth, maxTeeth),
            new TournamentSelection<int>(),
            new UniformCrossover<int>(),
            new GearMutation(minTeeth, maxTeeth),
            options,
            new Random(42));

        return true;
    }

    private void RenderResult(GenerationResult<int> result)
    {
        _bestGears = result.BestChromosome.Genes.ToArray();
        var ratio = GearTrainProblem.CalculateRatio(_bestGears);
        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = $"{result.BestFitness:F6}%";
        ActualRatioTextBlock.Text = ratio.ToString("F9", CultureInfo.InvariantCulture);
        TargetRatioDisplayTextBlock.Text = _targetRatio.ToString("F9", CultureInfo.InvariantCulture);
        GearsTextBlock.Text = $"{_bestGears[0]}  {_bestGears[1]}  {_bestGears[2]}  {_bestGears[3]}";
        StatusTextBlock.Text = "Running";

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            foreach (var chromosome in _solver.Population.Take(12))
            {
                var genes = chromosome.Genes;
                PopulationListBox.Items.Add($"{chromosome.Fitness,10:F6}%  A={genes[0],2} B={genes[1],2} C={genes[2],2} D={genes[3],2}");
            }
        }

        DrawGears();
        DrawFitnessChart();
    }

    private void DrawGears()
    {
        GearCanvas.Children.Clear();

        if (GearCanvas.ActualWidth <= 0 || GearCanvas.ActualHeight <= 0)
        {
            return;
        }

        var width = GearCanvas.ActualWidth;
        var height = GearCanvas.ActualHeight;
        var centerYTop = height * 0.34;
        var centerYBottom = height * 0.66;
        var centerXLeft = width * 0.32;
        var centerXRight = width * 0.68;

        DrawShaft(centerXRight, centerYTop, centerXLeft, centerYBottom);
        DrawGear(centerXLeft, centerYTop, _bestGears[0], "A", Color.FromRgb(37, 99, 235));
        DrawGear(centerXRight, centerYTop, _bestGears[1], "B", Color.FromRgb(34, 197, 94));
        DrawGear(centerXLeft, centerYBottom, _bestGears[2], "C", Color.FromRgb(245, 158, 11));
        DrawGear(centerXRight, centerYBottom, _bestGears[3], "D", Color.FromRgb(168, 85, 247));

        AddLabel("A meshes B", width * 0.45, centerYTop - 76, 14, FontWeights.SemiBold);
        AddLabel("B and C share shaft", width * 0.42, height * 0.50 - 10, 14, FontWeights.SemiBold);
        AddLabel("C meshes D", width * 0.45, centerYBottom + 60, 14, FontWeights.SemiBold);
    }

    private void DrawShaft(double x1, double y1, double x2, double y2)
    {
        GearCanvas.Children.Add(new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            StrokeThickness = 5,
            Opacity = 0.55
        });
    }

    private void DrawGear(double cx, double cy, int teeth, string name, Color color)
    {
        var radius = 28 + teeth * 0.85;
        var brush = new SolidColorBrush(color);

        for (int i = 0; i < Math.Min(teeth, 64); i++)
        {
            var angle = Math.PI * 2 * i / Math.Min(teeth, 64);
            var tooth = new Rectangle
            {
                Width = 5,
                Height = 12,
                Fill = brush,
                RenderTransform = new RotateTransform(angle * 180 / Math.PI + 90)
            };
            Canvas.SetLeft(tooth, cx + Math.Cos(angle) * (radius + 4) - 2.5);
            Canvas.SetTop(tooth, cy + Math.Sin(angle) * (radius + 4) - 6);
            GearCanvas.Children.Add(tooth);
        }

        var circle = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = brush,
            Stroke = Brushes.White,
            StrokeThickness = 4,
            Opacity = 0.90
        };
        Canvas.SetLeft(circle, cx - radius);
        Canvas.SetTop(circle, cy - radius);
        GearCanvas.Children.Add(circle);

        AddLabel($"{name}={teeth}", cx - 34, cy - 12, 20, FontWeights.SemiBold, Brushes.White);
    }

    private void AddLabel(string text, double left, double top, double fontSize, FontWeight weight, Brush? foreground = null)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = weight,
            Foreground = foreground ?? new SolidColorBrush(Color.FromRgb(32, 36, 42))
        };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        GearCanvas.Children.Add(label);
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
        TargetRatioTextBox.IsEnabled = !isRunning;
        MinTeethTextBox.IsEnabled = !isRunning;
        MaxTeethTextBox.IsEnabled = !isRunning;
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
