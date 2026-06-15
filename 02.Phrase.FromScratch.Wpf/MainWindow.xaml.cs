using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace _02.Phrase.FromScratch.Wpf;

public partial class MainWindow : Window
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ 1234567890,.";

    private readonly Random _random = new();
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private List<(char[] Genes, int Fitness)> _population = [];
    private CancellationTokenSource? _evolutionCancellation;
    private string _targetPhrase = "";
    private int _populationSize;
    private int _maxGenerations;
    private int _generation;

    public MainWindow()
    {
        InitializeComponent();
        UpdateParameterLabels();
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_population.Count == 0 || _generation >= _maxGenerations || _population[0].Fitness == 0)
        {
            if (!TryReadParameters()) return;
            InitializePopulation();
        }

        SetRunningState(true);
        _evolutionCancellation = new CancellationTokenSource();

        try
        {
            while (_generation < _maxGenerations && _population[0].Fitness > 0)
            {
                RunOneGeneration();
                RenderState();

                var delay = (int)DelaySlider.Value;
                if (delay > 0)
                {
                    await Task.Delay(delay, _evolutionCancellation.Token);
                }
                else
                {
                    await Task.Yield();
                }
            }

            StatusTextBlock.Text = _population[0].Fitness == 0 ? "Solution found" : "Generation limit reached";
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

    private void StepButton_Click(object sender, RoutedEventArgs e)
    {
        if (_population.Count == 0)
        {
            if (!TryReadParameters()) return;
            InitializePopulation();
            RenderState();
            return;
        }

        if (_generation >= _maxGenerations || _population[0].Fitness == 0)
        {
            StatusTextBlock.Text = _population[0].Fitness == 0 ? "Solution found" : "Generation limit reached";
            return;
        }

        RunOneGeneration();
        RenderState();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _evolutionCancellation?.Cancel();

        if (!TryReadParameters()) return;

        InitializePopulation();
        RenderState();
        StatusTextBlock.Text = "Ready";
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

    private bool TryReadParameters()
    {
        _targetPhrase = TargetPhraseTextBox.Text.Trim();

        if (_targetPhrase.Length < 2)
        {
            MessageBox.Show("Target phrase must contain at least two characters.", "Invalid target", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(PopulationSizeTextBox.Text, out _populationSize) || _populationSize < 4)
        {
            MessageBox.Show("Population size must be at least 4.", "Invalid population size", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(MaxGenerationsTextBox.Text, out _maxGenerations) || _maxGenerations < 1)
        {
            MessageBox.Show("Maximum generations must be at least 1.", "Invalid generation limit", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private void InitializePopulation()
    {
        _generation = 0;
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();
        _population = CreateInitialPopulation();
        EvaluateAndSort(_population);
        RecordHistory();
        RenderState();
    }

    private List<(char[] Genes, int Fitness)> CreateInitialPopulation()
    {
        var result = new List<(char[] Genes, int Fitness)>();

        for (int i = 0; i < _populationSize; i++)
        {
            result.Add((CreateRandomChromosome(), 0));
        }

        return result;
    }

    private char[] CreateRandomChromosome()
    {
        var genes = new char[_targetPhrase.Length];

        for (int i = 0; i < genes.Length; i++)
        {
            genes[i] = CreateRandomGene();
        }

        return genes;
    }

    private char CreateRandomGene()
    {
        var index = _random.Next(Alphabet.Length);
        return Alphabet[index];
    }

    private void RunOneGeneration()
    {
        _population = CreateNextGeneration(_population);
        EvaluateAndSort(_population);
        _generation++;
        RecordHistory();
    }

    private void EvaluateAndSort(List<(char[] Genes, int Fitness)> currentPopulation)
    {
        for (int i = 0; i < currentPopulation.Count; i++)
        {
            var genes = currentPopulation[i].Genes;
            currentPopulation[i] = (genes, CalculateFitness(genes));
        }

        currentPopulation.Sort((left, right) => left.Fitness.CompareTo(right.Fitness));
    }

    private int CalculateFitness(char[] genes)
    {
        var fitness = 0;

        for (int i = 0; i < _targetPhrase.Length; i++)
        {
            fitness += Math.Abs(_targetPhrase[i] - genes[i]);
        }

        return fitness;
    }

    private List<(char[] Genes, int Fitness)> CreateNextGeneration(List<(char[] Genes, int Fitness)> currentPopulation)
    {
        var nextGeneration = new List<(char[] Genes, int Fitness)>();
        var eliteCount = Math.Max(1, (int)(_populationSize * ElitismRateSlider.Value));

        for (int i = 0; i < eliteCount; i++)
        {
            nextGeneration.Add((Clone(currentPopulation[i].Genes), 0));
        }

        while (nextGeneration.Count < _populationSize)
        {
            var parentA = SelectParent(currentPopulation);
            var parentB = SelectParent(currentPopulation);
            var child = Crossover(parentA.Genes, parentB.Genes);

            Mutate(child);
            nextGeneration.Add((child, 0));
        }

        return nextGeneration;
    }

    private (char[] Genes, int Fitness) SelectParent(List<(char[] Genes, int Fitness)> currentPopulation)
    {
        var selectionPoolSize = Math.Max(2, currentPopulation.Count / 2);
        var index = _random.Next(selectionPoolSize);
        return currentPopulation[index];
    }

    private char[] Crossover(char[] parentA, char[] parentB)
    {
        var child = new char[_targetPhrase.Length];
        var crossoverPoint = _random.Next(1, _targetPhrase.Length);

        for (int i = 0; i < _targetPhrase.Length; i++)
        {
            child[i] = i < crossoverPoint ? parentA[i] : parentB[i];
        }

        return child;
    }

    private void Mutate(char[] genes)
    {
        for (int i = 0; i < genes.Length; i++)
        {
            if (_random.NextDouble() < MutationRateSlider.Value)
            {
                genes[i] = CreateRandomGene();
            }
        }
    }

    private static char[] Clone(char[] genes)
    {
        var clone = new char[genes.Length];
        Array.Copy(genes, clone, genes.Length);
        return clone;
    }

    private void RecordHistory()
    {
        _bestFitnessHistory.Add(_population[0].Fitness);
        _averageFitnessHistory.Add(_population.Average(chromosome => chromosome.Fitness));
    }

    private void RenderState()
    {
        var best = _population[0];
        var average = _averageFitnessHistory.Count == 0 ? 0 : _averageFitnessHistory[^1];

        BestPhraseTextBlock.Text = new string(best.Genes);
        GenerationTextBlock.Text = _generation.ToString();
        BestFitnessTextBlock.Text = best.Fitness.ToString();
        AverageFitnessTextBlock.Text = average.ToString("F2");

        PopulationListBox.Items.Clear();
        foreach (var chromosome in _population.Take(24))
        {
            PopulationListBox.Items.Add($"{chromosome.Fitness,4}  {new string(chromosome.Genes)}");
        }

        DrawFitnessChart();
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
        StepButton.IsEnabled = !isRunning;
        ResetButton.IsEnabled = !isRunning;
        TargetPhraseTextBox.IsEnabled = !isRunning;
        PopulationSizeTextBox.IsEnabled = !isRunning;
        MaxGenerationsTextBox.IsEnabled = !isRunning;

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
