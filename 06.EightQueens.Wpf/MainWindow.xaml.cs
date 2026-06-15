using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GACore;

namespace _06.EightQueens.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];
    private readonly BitmapImage _queenImage = new(new Uri("pack://application:,,,/queen.png"));

    private GeneticSolver<int>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
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
        _bestGenes = [];
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        ChromosomeTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        PopulationListBox.Items.Clear();
        DrawBoard();
        DrawFitnessChart();
    }

    private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        UpdateParameterLabels();
    }

    private void BoardCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawBoard();
    }

    private void FitnessCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawFitnessChart();
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
            TargetFitness = 0,
            TournamentSize = tournamentSize
        };

        _solver = new GeneticSolver<int>(
            new EightQueensProblem(),
            new TournamentSelection<int>(),
            new UniformCrossover<int>(),
            new RandomResetMutation<int>(random => random.Next(8)),
            options);

        return true;
    }

    private void RenderResult(GenerationResult<int> result)
    {
        _bestGenes = result.BestChromosome.Genes.ToArray();
        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F0");
        ChromosomeTextBlock.Text = $"[{string.Join(", ", _bestGenes)}]";
        StatusTextBlock.Text = result.IsSolutionFound ? "Solution found" : "Running";

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            foreach (var chromosome in _solver.Population.Take(12))
            {
                PopulationListBox.Items.Add($"{chromosome.Fitness,3:F0}  [{string.Join(", ", chromosome.Genes)}]");
            }
        }

        DrawBoard();
        DrawFitnessChart();
    }

    private void DrawBoard()
    {
        BoardCanvas.Children.Clear();

        var size = Math.Min(BoardCanvas.ActualWidth, BoardCanvas.ActualHeight);
        if (size <= 0) return;

        var cell = size / 8.0;
        var leftOffset = (BoardCanvas.ActualWidth - size) / 2.0;
        var topOffset = (BoardCanvas.ActualHeight - size) / 2.0;
        var conflicts = EightQueensProblem.GetConflictingColumns(_bestGenes);

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var square = new Rectangle
                {
                    Width = cell,
                    Height = cell,
                    Fill = (row + col) % 2 == 0
                        ? new SolidColorBrush(Color.FromRgb(238, 232, 218))
                        : new SolidColorBrush(Color.FromRgb(105, 122, 94))
                };

                Canvas.SetLeft(square, leftOffset + col * cell);
                Canvas.SetTop(square, topOffset + row * cell);
                BoardCanvas.Children.Add(square);
            }
        }

        for (int col = 0; col < _bestGenes.Length; col++)
        {
            var row = _bestGenes[col];
            var imageSize = cell * 0.72;
            var x = leftOffset + col * cell + (cell - imageSize) / 2.0;
            var y = topOffset + row * cell + (cell - imageSize) / 2.0;

            if (conflicts.Contains(col))
            {
                var conflictMarker = new Ellipse
                {
                    Width = cell * 0.82,
                    Height = cell * 0.82,
                    Stroke = Brushes.Firebrick,
                    StrokeThickness = Math.Max(2, cell * 0.045),
                    Fill = Brushes.Transparent
                };

                Canvas.SetLeft(conflictMarker, leftOffset + col * cell + cell * 0.09);
                Canvas.SetTop(conflictMarker, topOffset + row * cell + cell * 0.09);
                BoardCanvas.Children.Add(conflictMarker);
            }

            var queen = new Image
            {
                Source = _queenImage,
                Width = imageSize,
                Height = imageSize,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true
            };

            Canvas.SetLeft(queen, x);
            Canvas.SetTop(queen, y);
            BoardCanvas.Children.Add(queen);
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
