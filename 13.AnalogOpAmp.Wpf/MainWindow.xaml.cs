using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GACore;

namespace _13.AnalogOpAmp.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private GeneticSolver<int>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private OpAmpProblem? _problem;
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
        GainTextBlock.Text = "-";
        RgTextBlock.Text = "-";
        RfTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        PopulationListBox.Items.Clear();
        TransferCanvas.Children.Clear();
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

    private void TransferCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawTransfer();
    }

    private bool TryCreateSolver()
    {
        if (!double.TryParse(TargetGainTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var targetGain) || targetGain <= 1)
        {
            MessageBox.Show("Target gain must be greater than 1.", "Invalid target", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        _problem = new OpAmpProblem(targetGain);
        var options = new SolverOptions
        {
            PopulationSize = populationSize,
            MaxGenerations = maxGenerations,
            ElitismRate = ElitismRateSlider.Value,
            MutationRate = MutationRateSlider.Value,
            FitnessGoal = FitnessGoal.Minimize,
            TargetFitness = 0,
            FitnessTolerance = targetGain * 0.001,
            TournamentSize = tournamentSize
        };

        _solver = new GeneticSolver<int>(
            _problem,
            new TournamentSelection<int>(),
            new UniformCrossover<int>(),
            new OpAmpMutation(),
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
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F5");
        GainTextBlock.Text = $"{design.Gain:F5} V/V";
        RgTextBlock.Text = OpAmpProblem.FormatResistance(design.Rg);
        RfTextBlock.Text = OpAmpProblem.FormatResistance(design.Rf);
        StatusTextBlock.Text = result.IsSolutionFound ? "Target reached" : "Running";

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            foreach (var chromosome in _solver.Population.Take(12))
            {
                var item = _problem.Decode(chromosome.Genes);
                PopulationListBox.Items.Add($"{chromosome.Fitness,8:F5}  Rg={OpAmpProblem.FormatResistance(item.Rg),8}  Rf={OpAmpProblem.FormatResistance(item.Rf),8}  gain={item.Gain,8:F4}");
            }
        }

        DrawTransfer();
        DrawFitnessChart();
    }

    private void DrawTransfer()
    {
        TransferCanvas.Children.Clear();
        if (_problem is null || _bestGenes.Length == 0 || TransferCanvas.ActualWidth <= 0 || TransferCanvas.ActualHeight <= 0) return;

        var design = _problem.Decode(_bestGenes);
        var width = TransferCanvas.ActualWidth;
        var height = TransferCanvas.ActualHeight;
        var padding = 32.0;
        var maxInput = 1.0;
        var maxOutput = Math.Max(_problem.TargetGain, design.Gain) * maxInput;

        TransferCanvas.Children.Add(new Line { X1 = padding, X2 = padding, Y1 = padding, Y2 = height - padding, Stroke = Brushes.Gray, StrokeThickness = 1 });
        TransferCanvas.Children.Add(new Line { X1 = padding, X2 = width - padding, Y1 = height - padding, Y2 = height - padding, Stroke = Brushes.Gray, StrokeThickness = 1 });

        DrawGainLine(_problem.TargetGain, maxInput, maxOutput, width, height, padding, Color.FromRgb(217, 75, 65));
        DrawGainLine(design.Gain, maxInput, maxOutput, width, height, padding, Color.FromRgb(23, 105, 224));
    }

    private void DrawGainLine(double gain, double maxInput, double maxOutput, double width, double height, double padding, Color color)
    {
        var line = new Polyline { Stroke = new SolidColorBrush(color), StrokeThickness = 2.4 };

        for (int i = 0; i <= 60; i++)
        {
            var input = maxInput * i / 60.0;
            var output = gain * input;
            var x = padding + input / maxInput * (width - padding * 2);
            var y = height - padding - output / maxOutput * (height - padding * 2);
            line.Points.Add(new Point(x, y));
        }

        TransferCanvas.Children.Add(line);
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
        TargetGainTextBox.IsEnabled = !isRunning;
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
