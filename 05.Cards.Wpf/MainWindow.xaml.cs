using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GACore;

namespace _05.Cards.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private GeneticSolver<int>? _solver;
    private CancellationTokenSource? _evolutionCancellation;

    public MainWindow()
    {
        InitializeComponent();
        UpdateParameterLabels();
        RenderCards([]);
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

        RenderCards([]);
        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        SumTextBlock.Text = "-";
        ProductTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        PopulationListBox.Items.Clear();
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
            new CardsProblem(),
            new TournamentSelection<int>(),
            new OrderCrossover<int>(),
            new SwapMutation<int>(),
            options);

        return true;
    }

    private void RenderResult(GenerationResult<int> result)
    {
        var cards = result.BestChromosome.Genes;
        var firstGroupSum = CardsProblem.FirstGroupSum(cards);
        var secondGroupProduct = CardsProblem.SecondGroupProduct(cards);

        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        RenderCards(cards);
        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F0");
        SumTextBlock.Text = $"{firstGroupSum} / 36";
        ProductTextBlock.Text = $"{secondGroupProduct} / 360";
        StatusTextBlock.Text = result.IsSolutionFound ? "Solution found" : "Running";

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            foreach (var chromosome in _solver.Population.Take(24))
            {
                PopulationListBox.Items.Add($"{chromosome.Fitness,4:F0}  {string.Join(" ", chromosome.Genes)}");
            }
        }

        DrawFitnessChart();
    }

    private void RenderCards(IReadOnlyList<int> cards)
    {
        CardsItemsControl.Items.Clear();

        if (cards.Count == 0)
        {
            CardsItemsControl.Items.Add(CreateCardBlock("-", "#F1F3F5"));
            return;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            var color = i < 5 ? "#EAF3FF" : "#FFF1E8";
            CardsItemsControl.Items.Add(CreateCardBlock(cards[i].ToString(), color));
        }
    }

    private Border CreateCardBlock(string text, string background)
    {
        return new Border
        {
            Width = 54,
            Height = 70,
            Margin = new Thickness(0, 0, 10, 10),
            Background = (Brush)new BrushConverter().ConvertFromString(background)!,
            BorderBrush = Brushes.DarkGray,
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
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
