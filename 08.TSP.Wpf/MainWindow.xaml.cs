using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GACore;

namespace _08.TSP.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private GeneticSolver<City>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private IReadOnlyList<City> _cities = [];
    private IReadOnlyList<City> _bestRoute = [];
    private IReadOnlyList<Chromosome<City>> _lastPoolSnapshot = [];

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
        _cities = [];
        _bestRoute = [];
        _lastPoolSnapshot = [];
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        AverageFitnessTextBlock.Text = "-";
        CityCountTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        RouteListBox.Items.Clear();
        ChromosomePoolImage.Source = null;
        ChromosomePoolPlaceholderTextBlock.Visibility = Visibility.Visible;
        DrawMap();
        DrawChromosomePool();
        DrawFitnessChart();
    }

    private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        UpdateParameterLabels();
    }

    private void MapCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawMap();
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
        if (!int.TryParse(CityCountTextBox.Text, out var cityCount) || cityCount < 4)
        {
            MessageBox.Show("City count must be at least 4.", "Invalid city count", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        _cities = DatasetComboBox.SelectedIndex == 1
            ? TspProblem.CreateRandomCities(cityCount, new Random(42))
            : TspProblem.CreateCircularCities(cityCount);

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

        _solver = new GeneticSolver<City>(
            new TspProblem(_cities),
            new TournamentSelection<City>(),
            new OrderCrossover<City>(),
            new InversionMutation<City>(),
            options);

        return true;
    }

    private void RenderResult(GenerationResult<City> result)
    {
        _bestRoute = result.BestChromosome.Genes.ToArray();
        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F1");
        AverageFitnessTextBlock.Text = result.AverageFitness.ToString("F1");
        CityCountTextBlock.Text = _cities.Count.ToString();
        StatusTextBlock.Text = "Running";

        RouteListBox.Items.Clear();
        foreach (var city in _bestRoute.Take(32))
        {
            RouteListBox.Items.Add($"{city.Id,2}: ({city.X,6:F1}, {city.Y,6:F1})");
        }

        if (_solver is not null)
        {
            _lastPoolSnapshot = _solver.Population
                .Select(chromosome => chromosome.Clone())
                .ToArray();
        }

        DrawMap();
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

        const int maxVisibleRows = 160;

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
                var route = _lastPoolSnapshot[sourceRow].Genes;
                var y = row * yStep;

                for (int column = 0; column < chromosomeLength; column++)
                {
                    var brush = GetCityBrush(route[column]);
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

    private Brush GetCityBrush(City city)
    {
        var color = HslToRgb((city.Id - 1) / (double)Math.Max(1, _cities.Count), 0.72, 0.50);
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

    private void DrawMap()
    {
        MapCanvas.Children.Clear();

        if (_cities.Count == 0 || MapCanvas.ActualWidth <= 0 || MapCanvas.ActualHeight <= 0)
        {
            return;
        }

        var padding = 28.0;
        var minX = _cities.Min(city => city.X);
        var maxX = _cities.Max(city => city.X);
        var minY = _cities.Min(city => city.Y);
        var maxY = _cities.Max(city => city.Y);
        var scaleX = (MapCanvas.ActualWidth - padding * 2) / Math.Max(1, maxX - minX);
        var scaleY = (MapCanvas.ActualHeight - padding * 2) / Math.Max(1, maxY - minY);
        var scale = Math.Min(scaleX, scaleY);

        Point Project(City city)
        {
            var x = padding + (city.X - minX) * scale;
            var y = padding + (city.Y - minY) * scale;
            return new Point(x, y);
        }

        if (_bestRoute.Count > 1)
        {
            for (int i = 0; i < _bestRoute.Count; i++)
            {
                var current = Project(_bestRoute[i]);
                var next = Project(_bestRoute[(i + 1) % _bestRoute.Count]);
                var line = new Line
                {
                    X1 = current.X,
                    Y1 = current.Y,
                    X2 = next.X,
                    Y2 = next.Y,
                    Stroke = new SolidColorBrush(Color.FromRgb(23, 105, 224)),
                    StrokeThickness = 2,
                    Opacity = 0.75
                };
                MapCanvas.Children.Add(line);
            }
        }

        foreach (var city in _cities)
        {
            var point = Project(city);
            var marker = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.Firebrick,
                Stroke = Brushes.White,
                StrokeThickness = 1.5
            };

            Canvas.SetLeft(marker, point.X - 5);
            Canvas.SetTop(marker, point.Y - 5);
            MapCanvas.Children.Add(marker);
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
        DatasetComboBox.IsEnabled = !isRunning;
        CityCountTextBox.IsEnabled = !isRunning;
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
