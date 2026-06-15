using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GACore;

namespace _20.MazeSolver.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private MazeProblem? _problem;
    private GeneticSolver<int>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private MazeEvaluation? _lastEvaluation;
    private int[] _bestMoves = [];

    public MainWindow()
    {
        InitializeComponent();
        UpdateParameterLabels();
        _problem = new MazeProblem(80);
        DrawMaze();
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
                    StatusTextBlock.Text = "Exit reached";
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
        _lastEvaluation = null;
        _bestMoves = [];
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        DistanceTextBlock.Text = "-";
        CollisionsTextBlock.Text = "-";
        UsedMovesTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        PopulationListBox.Items.Clear();
        DrawMaze();
        DrawFitnessChart();
    }

    private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        UpdateParameterLabels();
    }

    private void MazeCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawMaze();

    private void FitnessCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawFitnessChart();

    private bool TryCreateSolver()
    {
        if (!int.TryParse(MoveCountTextBox.Text, out var moveCount) || moveCount < 8)
        {
            MessageBox.Show("Move count must be at least 8.", "Invalid move count", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(PopulationSizeTextBox.Text, out var populationSize) || populationSize < 4 ||
            !int.TryParse(MaxGenerationsTextBox.Text, out var maxGenerations) || maxGenerations < 1 ||
            !int.TryParse(TournamentSizeTextBox.Text, out var tournamentSize) || tournamentSize < 1)
        {
            MessageBox.Show("Population, generations and tournament values must be valid positive numbers.", "Invalid solver parameters", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        _problem = new MazeProblem(moveCount);
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
            _problem,
            new TournamentSelection<int>(),
            new UniformCrossover<int>(),
            new RandomResetMutation<int>(random => random.Next(4)),
            options,
            new Random(42));

        return true;
    }

    private void RenderResult(GenerationResult<int> result)
    {
        if (_problem is null) return;

        _bestMoves = result.BestChromosome.Genes.ToArray();
        _lastEvaluation = _problem.Evaluate(_bestMoves);
        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F1");
        DistanceTextBlock.Text = _lastEvaluation.DistanceToExit.ToString();
        CollisionsTextBlock.Text = _lastEvaluation.Collisions.ToString();
        UsedMovesTextBlock.Text = _lastEvaluation.UsedMoves.ToString();
        StatusTextBlock.Text = _lastEvaluation.ReachedExit ? "Exit reached" : "Running";

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            foreach (var chromosome in _solver.Population.Take(12))
            {
                PopulationListBox.Items.Add($"{chromosome.Fitness,6:F1}  {ToMoveText(chromosome.Genes.Take(32))}...");
            }
        }

        DrawMaze();
        DrawFitnessChart();
    }

    private void DrawMaze()
    {
        MazeCanvas.Children.Clear();

        if (_problem is null || MazeCanvas.ActualWidth <= 0 || MazeCanvas.ActualHeight <= 0)
        {
            return;
        }

        var cell = Math.Min(MazeCanvas.ActualWidth / _problem.Width, MazeCanvas.ActualHeight / _problem.Height);
        var offsetX = (MazeCanvas.ActualWidth - _problem.Width * cell) / 2;
        var offsetY = (MazeCanvas.ActualHeight - _problem.Height * cell) / 2;

        for (int y = 0; y < _problem.Height; y++)
        {
            for (int x = 0; x < _problem.Width; x++)
            {
                var rect = new Rectangle
                {
                    Width = cell + 0.5,
                    Height = cell + 0.5,
                    Fill = _problem.IsWall(x, y)
                        ? new SolidColorBrush(Color.FromRgb(31, 41, 55))
                        : Brushes.White
                };
                Canvas.SetLeft(rect, offsetX + x * cell);
                Canvas.SetTop(rect, offsetY + y * cell);
                MazeCanvas.Children.Add(rect);
            }
        }

        if (_lastEvaluation is not null && _lastEvaluation.Path.Count > 1)
        {
            var line = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(23, 105, 224)),
                StrokeThickness = Math.Max(3, cell * 0.18),
                Opacity = 0.82
            };

            foreach (var point in _lastEvaluation.Path)
            {
                line.Points.Add(new Point(offsetX + point.X * cell + cell / 2, offsetY + point.Y * cell + cell / 2));
            }

            MazeCanvas.Children.Add(line);
        }

        DrawMarker(_problem.Start, "S", Color.FromRgb(34, 197, 94), cell, offsetX, offsetY);
        DrawMarker(_problem.Exit, "E", Color.FromRgb(239, 68, 68), cell, offsetX, offsetY);
    }

    private void DrawMarker(MazePoint point, string text, Color color, double cell, double offsetX, double offsetY)
    {
        var ellipse = new Ellipse
        {
            Width = cell * 0.72,
            Height = cell * 0.72,
            Fill = new SolidColorBrush(color),
            Stroke = Brushes.White,
            StrokeThickness = 2
        };
        Canvas.SetLeft(ellipse, offsetX + point.X * cell + cell * 0.14);
        Canvas.SetTop(ellipse, offsetY + point.Y * cell + cell * 0.14);
        MazeCanvas.Children.Add(ellipse);

        var label = new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            FontSize = Math.Max(11, cell * 0.36),
            Width = cell,
            TextAlignment = TextAlignment.Center
        };
        Canvas.SetLeft(label, offsetX + point.X * cell);
        Canvas.SetTop(label, offsetY + point.Y * cell + cell * 0.29);
        MazeCanvas.Children.Add(label);
    }

    private static string ToMoveText(IEnumerable<int> moves)
    {
        return string.Concat(moves.Select(move => move switch
        {
            0 => "U",
            1 => "R",
            2 => "D",
            3 => "L",
            _ => "?"
        }));
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
        MoveCountTextBox.IsEnabled = !isRunning;
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
