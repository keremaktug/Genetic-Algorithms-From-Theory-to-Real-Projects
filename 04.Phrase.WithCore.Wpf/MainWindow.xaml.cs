using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Shapes;
using GACore;

namespace _04.Phrase.WithCore.Wpf;

public partial class MainWindow : Window
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ 1234567890,.";

    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private GeneticSolver<char>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private IReadOnlyList<Chromosome<char>> _lastPoolSnapshot = [];

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
                    StatusTextBlock.Text = "Solution found";
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
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        BestPhraseTextBlock.Text = "Press Start to run GA.Core.";
        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        AverageFitnessTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        PopulationListBox.Items.Clear();
        _lastPoolSnapshot = [];
        ChromosomePoolImage.Source = null;
        ChromosomePoolPlaceholderTextBlock.Visibility = Visibility.Visible;
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

    private void ChromosomePoolHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawChromosomePool();
    }

    private bool TryCreateSolver()
    {
        var targetPhrase = TargetPhraseTextBox.Text.Trim();

        if (targetPhrase.Length < 2)
        {
            MessageBox.Show("Target phrase must contain at least two characters.", "Invalid target", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        var problem = new PhraseProblem(targetPhrase, Alphabet);
        var options = new SolverOptions
        {
            PopulationSize = populationSize,
            MaxGenerations = maxGenerations,
            ElitismRate = ElitismRateSlider.Value,
            MutationRate = MutationRateSlider.Value,
            FitnessGoal = FitnessGoal.Minimize,
            TargetFitness = 0,
            TournamentSize = tournamentSize
        };

        ICrossoverOperator<char> crossover = CrossoverComboBox.SelectedIndex == 1
            ? new UniformCrossover<char>()
            : new OnePointCrossover<char>();

        _solver = new GeneticSolver<char>(
            problem,
            new TournamentSelection<char>(),
            crossover,
            new RandomResetMutation<char>(random => Alphabet[random.Next(Alphabet.Length)]),
            options);

        return true;
    }

    private void RenderResult(GenerationResult<char> result)
    {
        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        BestPhraseTextBlock.Text = new string(result.BestChromosome.Genes);
        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F0");
        AverageFitnessTextBlock.Text = result.AverageFitness.ToString("F2");
        StatusTextBlock.Text = result.IsSolutionFound ? "Solution found" : "Running";

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            _lastPoolSnapshot = _solver.Population
                .Select(chromosome => chromosome.Clone())
                .ToArray();

            foreach (var chromosome in _solver.Population.Take(24))
            {
                PopulationListBox.Items.Add($"{chromosome.Fitness,4:F0}  {new string(chromosome.Genes)}");
            }
        }

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

        const int maxVisibleRows = 180;

        var chromosomeLength = _lastPoolSnapshot[0].Genes.Length;
        var visibleRows = Math.Min(maxVisibleRows, _lastPoolSnapshot.Count);
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
                var sourceRow = visibleRows == _lastPoolSnapshot.Count
                    ? row
                    : (int)Math.Round(row * (_lastPoolSnapshot.Count - 1) / (double)(visibleRows - 1));
                var genes = _lastPoolSnapshot[sourceRow].Genes;
                var y = row * yStep;

                for (int column = 0; column < chromosomeLength; column++)
                {
                    var brush = GetGeneBrush(genes[column]);
                    var x = column * xStep;
                    context.DrawRectangle(brush, null, new Rect(x, y, Math.Ceiling(xStep) + 1, Math.Ceiling(yStep) + 1));
                }
            }

            var guidePen = new Pen(new SolidColorBrush(Color.FromArgb(42, 32, 36, 42)), 1);
            guidePen.Freeze();

            for (int column = 1; column < chromosomeLength; column++)
            {
                var x = column * xStep;
                context.DrawLine(guidePen, new Point(x, 0), new Point(x, height));
            }

            var rowGuideStep = Math.Max(1, visibleRows / 12);
            for (int row = rowGuideStep; row < visibleRows; row += rowGuideStep)
            {
                var y = row * yStep;
                context.DrawLine(guidePen, new Point(0, y), new Point(width, y));
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();

        ChromosomePoolImage.Source = bitmap;
        ChromosomePoolPlaceholderTextBlock.Visibility = Visibility.Collapsed;
    }

    private static Brush GetGeneBrush(char gene)
    {
        var index = Alphabet.IndexOf(gene);

        if (index < 0)
        {
            return Brushes.LightGray;
        }

        var hue = index / (double)Math.Max(1, Alphabet.Length);
        var color = HslToRgb(hue, 0.75, 0.50);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color HslToRgb(double hue, double saturation, double lightness)
    {
        var chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var hPrime = hue * 6;
        var x = chroma * (1 - Math.Abs(hPrime % 2 - 1));

        (double r1, double g1, double b1) = hPrime switch
        {
            < 1 => (chroma, x, 0d),
            < 2 => (x, chroma, 0d),
            < 3 => (0d, chroma, x),
            < 4 => (0d, x, chroma),
            < 5 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };

        var m = lightness - chroma / 2;

        return Color.FromRgb(
            (byte)Math.Round((r1 + m) * 255),
            (byte)Math.Round((g1 + m) * 255),
            (byte)Math.Round((b1 + m) * 255));
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
        TargetPhraseTextBox.IsEnabled = !isRunning;
        PopulationSizeTextBox.IsEnabled = !isRunning;
        MaxGenerationsTextBox.IsEnabled = !isRunning;
        TournamentSizeTextBox.IsEnabled = !isRunning;
        CrossoverComboBox.IsEnabled = !isRunning;

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
