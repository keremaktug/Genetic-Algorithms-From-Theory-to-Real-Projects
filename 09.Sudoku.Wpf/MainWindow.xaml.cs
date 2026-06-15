using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GACore;

namespace _09.Sudoku.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];
    private readonly SudokuProblem _problem = new();

    private GeneticSolver<int>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private int[,] _bestGrid = new int[9, 9];
    private IReadOnlyList<Chromosome<int>> _lastPoolSnapshot = [];
    private int _maxGenerations;

    public MainWindow()
    {
        InitializeComponent();
        UpdateParameterLabels();
        EmptyCellsTextBlock.Text = _problem.EmptyCellCount.ToString();
        _bestGrid = _problem.Decode([]);
        DrawSudokuGrid();
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
            var solutionFound = false;
            using var results = _solver!.Run().GetEnumerator();

            while (await MoveNextOnBackgroundThread(results, _evolutionCancellation.Token))
            {
                var result = results.Current;

                RenderResult(result);

                if (result.IsSolutionFound)
                {
                    solutionFound = true;
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

            if (!solutionFound)
            {
                StatusTextBlock.Text = "Generation limit reached";
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

    private static Task<bool> MoveNextOnBackgroundThread(
        IEnumerator<GenerationResult<int>> results,
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
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();
        _bestGrid = _problem.Decode([]);
        _lastPoolSnapshot = [];

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        AverageFitnessTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        PopulationListBox.Items.Clear();
        ChromosomePoolImage.Source = null;
        ChromosomePoolPlaceholderTextBlock.Visibility = Visibility.Visible;
        DrawSudokuGrid();
        DrawChromosomePool();
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

        _maxGenerations = maxGenerations;

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
            _problem,
            new TournamentSelection<int>(),
            new SudokuRowCrossover(_problem.RowRanges),
            new SudokuRowMutation(_problem.RowRanges),
            options);

        return true;
    }

    private void RenderResult(GenerationResult<int> result)
    {
        _bestGrid = _problem.Decode(result.BestChromosome.Genes);
        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F0");
        AverageFitnessTextBlock.Text = result.AverageFitness.ToString("F2");
        StatusTextBlock.Text = result.IsSolutionFound ? "Solution found" : "Running";

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            _lastPoolSnapshot = _solver.Population
                .Take(180)
                .Select(chromosome => chromosome.Clone())
                .ToArray();

            foreach (var chromosome in _solver.Population.Take(10))
            {
                PopulationListBox.Items.Add($"{chromosome.Fitness,3:F0}  {string.Join("", chromosome.Genes.Take(36))}...");
            }
        }

        DrawSudokuGrid();
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

        var chromosomeLength = _lastPoolSnapshot[0].Genes.Length;
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
                var genes = _lastPoolSnapshot[row].Genes;
                var y = row * yStep;

                for (int column = 0; column < chromosomeLength; column++)
                {
                    var brush = GetDigitBrush(genes[column]);
                    var x = column * xStep;
                    context.DrawRectangle(brush, null, new Rect(x, y, Math.Ceiling(xStep) + 1, Math.Ceiling(yStep) + 1));
                }
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();

        ChromosomePoolImage.Source = bitmap;
        ChromosomePoolPlaceholderTextBlock.Visibility = Visibility.Collapsed;
    }

    private static Brush GetDigitBrush(int digit)
    {
        var color = digit switch
        {
            1 => Color.FromRgb(37, 99, 235),
            2 => Color.FromRgb(16, 185, 129),
            3 => Color.FromRgb(245, 158, 11),
            4 => Color.FromRgb(239, 68, 68),
            5 => Color.FromRgb(139, 92, 246),
            6 => Color.FromRgb(20, 184, 166),
            7 => Color.FromRgb(236, 72, 153),
            8 => Color.FromRgb(132, 204, 22),
            9 => Color.FromRgb(100, 116, 139),
            _ => Color.FromRgb(226, 232, 240)
        };

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void DrawSudokuGrid()
    {
        SudokuGrid.Children.Clear();
        SudokuGrid.RowDefinitions.Clear();
        SudokuGrid.ColumnDefinitions.Clear();

        const int cellSize = 48;
        SudokuGrid.Width = cellSize * 9;
        SudokuGrid.Height = cellSize * 9;

        for (int i = 0; i < 9; i++)
        {
            SudokuGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cellSize) });
            SudokuGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cellSize) });
        }

        var conflicts = _problem.GetConflictCells(_bestGrid);

        for (int row = 0; row < 9; row++)
        {
            for (int col = 0; col < 9; col++)
            {
                var isFixed = _problem.IsFixed(row, col);
                var border = new Border
                {
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(
                        col % 3 == 0 ? 2 : 0.5,
                        row % 3 == 0 ? 2 : 0.5,
                        col == 8 ? 2 : 0.5,
                        row == 8 ? 2 : 0.5),
                    Background = conflicts.Contains((row, col))
                        ? new SolidColorBrush(Color.FromRgb(255, 229, 225))
                        : isFixed
                            ? new SolidColorBrush(Color.FromRgb(235, 239, 245))
                            : Brushes.White,
                    Child = new TextBlock
                    {
                        Text = _bestGrid[row, col] == 0 ? "" : _bestGrid[row, col].ToString(),
                        FontSize = 22,
                        FontWeight = isFixed ? FontWeights.Bold : FontWeights.Normal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = isFixed ? Brushes.Black : new SolidColorBrush(Color.FromRgb(23, 105, 224))
                    }
                };

                Grid.SetRow(border, row);
                Grid.SetColumn(border, col);
                SudokuGrid.Children.Add(border);
            }
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
