using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GACore;

namespace _07.Knapsack.Wpf;

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
        RenderItems([]);
        RenderBackpack([]);
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

            StatusTextBlock.Text = "Generation limit reached";
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

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        WeightTextBlock.Text = "-";
        RemainingCapacityTextBlock.Text = "-";
        SelectedItemsTextBlock.Text = "-";
        ChromosomeTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        PopulationListBox.Items.Clear();
        RenderItems([]);
        RenderBackpack([]);
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
            FitnessGoal = FitnessGoal.Maximize,
            TargetFitness = null,
            TournamentSize = tournamentSize
        };

        _solver = new GeneticSolver<int>(
            new KnapsackProblem(),
            new TournamentSelection<int>(),
            new UniformCrossover<int>(),
            new RandomResetMutation<int>(random => random.Next(2)),
            options);

        return true;
    }

    private void RenderResult(GenerationResult<int> result)
    {
        var genes = result.BestChromosome.Genes;
        var totalWeight = KnapsackProblem.TotalWeight(genes);
        var totalValue = KnapsackProblem.TotalValue(genes);

        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = totalValue.ToString();
        WeightTextBlock.Text = $"{totalWeight} / {KnapsackProblem.Capacity}";
        RemainingCapacityTextBlock.Text = Math.Max(0, KnapsackProblem.Capacity - totalWeight).ToString();
        SelectedItemsTextBlock.Text = GetSelectedItemNames(genes);
        ChromosomeTextBlock.Text = string.Join("", genes);
        StatusTextBlock.Text = totalWeight <= KnapsackProblem.Capacity
            ? "Valid solution"
            : "Over capacity";

        RenderItems(genes);
        RenderBackpack(genes);

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            foreach (var chromosome in _solver.Population.Take(12))
            {
                PopulationListBox.Items.Add($"{chromosome.Fitness,4:F0}  {string.Join("", chromosome.Genes)}");
            }
        }

        DrawFitnessChart();
    }

    private void RenderItems(IReadOnlyList<int> genes)
    {
        ItemsWrapPanel.Children.Clear();

        for (int i = 0; i < KnapsackProblem.Items.Length; i++)
        {
            var item = KnapsackProblem.Items[i];
            var isSelected = genes.Count > i && genes[i] == 1;
            var borderBrush = isSelected ? Color.FromRgb(71, 154, 111) : Color.FromRgb(201, 208, 217);
            var background = isSelected ? Color.FromRgb(231, 245, 238) : Color.FromRgb(241, 243, 246);
            var valueDensity = item.Value / (double)item.Weight;

            var card = new Border
            {
                Width = 158,
                MinHeight = 88,
                Margin = new Thickness(0, 0, 10, 10),
                Padding = new Thickness(10),
                Background = new SolidColorBrush(background),
                BorderBrush = new SolidColorBrush(borderBrush),
                BorderThickness = isSelected ? new Thickness(2) : new Thickness(1),
                CornerRadius = new CornerRadius(6)
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = $"{i + 1}. {item.Name}",
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(new TextBlock
            {
                Text = $"Weight {item.Weight} kg  |  Value {item.Value}",
                Foreground = new SolidColorBrush(Color.FromRgb(89, 97, 109)),
                Margin = new Thickness(0, 6, 0, 0)
            });
            panel.Children.Add(new TextBlock
            {
                Text = $"Value/kg {valueDensity:F1}",
                Foreground = new SolidColorBrush(Color.FromRgb(89, 97, 109)),
                Margin = new Thickness(0, 2, 0, 0)
            });

            card.Child = panel;
            ItemsWrapPanel.Children.Add(card);
        }
    }

    private void RenderBackpack(IReadOnlyList<int> genes)
    {
        BackpackWrapPanel.Children.Clear();

        var totalWeight = KnapsackProblem.TotalWeight(genes);
        var capacityRatio = totalWeight / (double)KnapsackProblem.Capacity;

        CapacityProgressBar.Value = Math.Min(totalWeight, KnapsackProblem.Capacity);
        CapacityProgressBar.Foreground = new SolidColorBrush(
            totalWeight > KnapsackProblem.Capacity
                ? Color.FromRgb(217, 75, 65)
                : Color.FromRgb(31, 138, 91));
        CapacityBarTextBlock.Text = $"{totalWeight} / {KnapsackProblem.Capacity} kg";
        CapacityPercentTextBlock.Text = $"{capacityRatio:P0}";
        CapacityPercentTextBlock.Foreground = new SolidColorBrush(
            totalWeight > KnapsackProblem.Capacity
                ? Color.FromRgb(180, 45, 45)
                : Color.FromRgb(32, 36, 42));

        for (int i = 0; i < KnapsackProblem.Items.Length; i++)
        {
            if (genes.Count <= i || genes[i] != 1)
            {
                continue;
            }

            var item = KnapsackProblem.Items[i];
            var width = Math.Max(82, item.Weight * 16);

            var block = new Border
            {
                Width = width,
                Height = 58,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(8),
                Background = new SolidColorBrush(Color.FromRgb(44, 109, 185)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(27, 76, 136)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6)
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = item.Name,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            panel.Children.Add(new TextBlock
            {
                Text = $"{item.Weight} kg / {item.Value} value",
                Foreground = new SolidColorBrush(Color.FromRgb(224, 235, 248)),
                FontSize = 12
            });

            block.Child = panel;
            BackpackWrapPanel.Children.Add(block);
        }

        if (BackpackWrapPanel.Children.Count == 0)
        {
            BackpackWrapPanel.Children.Add(new TextBlock
            {
                Text = "No items selected yet.",
                Foreground = new SolidColorBrush(Color.FromRgb(89, 97, 109)),
                Margin = new Thickness(0, 8, 0, 0)
            });
        }
    }

    private static string GetSelectedItemNames(IReadOnlyList<int> genes)
    {
        var selected = KnapsackProblem.Items
            .Where((_, index) => genes.Count > index && genes[index] == 1)
            .Select(item => item.Name)
            .ToArray();

        return selected.Length == 0 ? "-" : string.Join(", ", selected);
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
