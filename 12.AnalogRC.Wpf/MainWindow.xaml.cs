using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GACore;

namespace _12.AnalogRC.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private GeneticSolver<int>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private RcProblem? _problem;
    private int[] _bestGenes = [];

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

                if (result.IsSolutionFound)
                {
                    StatusTextBlock.Text = "Target reached";
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

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        _evolutionCancellation?.Cancel();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _evolutionCancellation?.Cancel();
        _solver = null;
        _problem = null;
        _bestGenes = [];
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        CutoffTextBlock.Text = "-";
        ResistorTextBlock.Text = "-";
        CapacitorTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        PopulationListBox.Items.Clear();
        ResponseCanvas.Children.Clear();
        DrawFitnessChart();
    }

    private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        UpdateParameterLabels();
    }

    private void FitnessCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawFitnessChart();
    }

    private void ResponseCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawResponse();
    }

    private bool TryCreateSolver()
    {
        if (!double.TryParse(TargetFrequencyTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var targetFrequency) || targetFrequency <= 0)
        {
            MessageBox.Show("Target cutoff frequency must be a positive number.", "Invalid target", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        _problem = new RcProblem(targetFrequency);
        var options = new SolverOptions
        {
            PopulationSize = populationSize,
            MaxGenerations = maxGenerations,
            ElitismRate = ElitismRateSlider.Value,
            MutationRate = MutationRateSlider.Value,
            FitnessGoal = FitnessGoal.Minimize,
            TargetFitness = 0,
            FitnessTolerance = targetFrequency * 0.002,
            TournamentSize = tournamentSize
        };

        _solver = new GeneticSolver<int>(
            _problem,
            new TournamentSelection<int>(),
            new UniformCrossover<int>(),
            new RcMutation(),
            options);

        return true;
    }

    private void RenderResult(GenerationResult<int> result)
    {
        if (_problem is null) return;

        _bestGenes = result.BestChromosome.Genes.ToArray();
        var design = _problem.Decode(_bestGenes);

        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F2");
        CutoffTextBlock.Text = $"{design.CutoffFrequency:F2} Hz";
        ResistorTextBlock.Text = RcProblem.FormatResistance(design.Resistance);
        CapacitorTextBlock.Text = RcProblem.FormatCapacitance(design.Capacitance);
        StatusTextBlock.Text = result.IsSolutionFound ? "Target reached" : "Running";

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            foreach (var chromosome in _solver.Population.Take(12))
            {
                var item = _problem.Decode(chromosome.Genes);
                PopulationListBox.Items.Add($"{chromosome.Fitness,8:F2}  R={RcProblem.FormatResistance(item.Resistance),8}  C={RcProblem.FormatCapacitance(item.Capacitance),8}  fc={item.CutoffFrequency,9:F2}");
            }
        }

        DrawResponse();
        DrawFitnessChart();
    }

    private void DrawResponse()
    {
        ResponseCanvas.Children.Clear();
        if (_problem is null || _bestGenes.Length == 0 || ResponseCanvas.ActualWidth <= 0 || ResponseCanvas.ActualHeight <= 0) return;

        var design = _problem.Decode(_bestGenes);
        var width = ResponseCanvas.ActualWidth;
        var height = ResponseCanvas.ActualHeight;
        var padding = 28.0;
        var minFrequency = Math.Max(1, _problem.TargetFrequency / 100.0);
        var maxFrequency = _problem.TargetFrequency * 100.0;
        var minLog = Math.Log10(minFrequency);
        var maxLog = Math.Log10(maxFrequency);

        var axis = new Line { X1 = padding, X2 = padding, Y1 = padding, Y2 = height - padding, Stroke = Brushes.Gray, StrokeThickness = 1 };
        ResponseCanvas.Children.Add(axis);
        ResponseCanvas.Children.Add(new Line { X1 = padding, X2 = width - padding, Y1 = height - padding, Y2 = height - padding, Stroke = Brushes.Gray, StrokeThickness = 1 });

        var curve = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(23, 105, 224)),
            StrokeThickness = 2.4
        };

        for (int i = 0; i < 160; i++)
        {
            var t = i / 159.0;
            var frequency = Math.Pow(10, minLog + (maxLog - minLog) * t);
            var gain = 1.0 / Math.Sqrt(1.0 + Math.Pow(frequency / design.CutoffFrequency, 2));
            var db = 20 * Math.Log10(gain);
            var y = padding + Math.Clamp(-db / 40.0, 0, 1) * (height - padding * 2);
            var x = padding + t * (width - padding * 2);
            curve.Points.Add(new Point(x, y));
        }

        ResponseCanvas.Children.Add(curve);

        var cutoffT = (Math.Log10(design.CutoffFrequency) - minLog) / (maxLog - minLog);
        var cutoffX = padding + cutoffT * (width - padding * 2);
        ResponseCanvas.Children.Add(new Line { X1 = cutoffX, X2 = cutoffX, Y1 = padding, Y2 = height - padding, Stroke = Brushes.Firebrick, StrokeThickness = 1.6, StrokeDashArray = [4, 4] });
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
        TargetFrequencyTextBox.IsEnabled = !isRunning;
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
