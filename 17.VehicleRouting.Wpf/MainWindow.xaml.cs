using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GACore;

namespace _17.VehicleRouting.Wpf;

public partial class MainWindow : Window
{
    private static readonly Color[] VehicleColors =
    [
        Color.FromRgb(37, 99, 235),
        Color.FromRgb(22, 163, 74),
        Color.FromRgb(234, 88, 12),
        Color.FromRgb(147, 51, 234),
        Color.FromRgb(219, 39, 119),
        Color.FromRgb(13, 148, 136),
        Color.FromRgb(100, 116, 139)
    ];

    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private GeneticSolver<Customer>? _solver;
    private VehicleRoutingProblem? _problem;
    private CancellationTokenSource? _evolutionCancellation;
    private IReadOnlyList<Customer> _customers = [];
    private IReadOnlyList<Customer> _bestOrder = [];
    private IReadOnlyList<Chromosome<Customer>> _lastPoolSnapshot = [];
    private RouteEvaluation? _lastEvaluation;

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

    private void PauseButton_Click(object sender, RoutedEventArgs e) => _evolutionCancellation?.Cancel();

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _evolutionCancellation?.Cancel();
        _solver = null;
        _problem = null;
        _customers = [];
        _bestOrder = [];
        _lastPoolSnapshot = [];
        _lastEvaluation = null;
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        DistanceTextBlock.Text = "-";
        CapacityPenaltyTextBlock.Text = "-";
        VehiclesUsedTextBlock.Text = "-";
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

    private void MapCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawMap();

    private void ChromosomePoolHost_SizeChanged(object sender, SizeChangedEventArgs e) => DrawChromosomePool();

    private void FitnessCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawFitnessChart();

    private bool TryCreateSolver()
    {
        if (!int.TryParse(CustomerCountTextBox.Text, out var customerCount) || customerCount < 6)
        {
            MessageBox.Show("Customer count must be at least 6.", "Invalid customer count", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(VehicleCountTextBox.Text, out var vehicleCount) || vehicleCount < 1)
        {
            MessageBox.Show("Vehicle count must be at least 1.", "Invalid vehicle count", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(VehicleCapacityTextBox.Text, out var capacity) || capacity < 4)
        {
            MessageBox.Show("Vehicle capacity must be at least 4.", "Invalid vehicle capacity", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(PopulationSizeTextBox.Text, out var populationSize) || populationSize < 4 ||
            !int.TryParse(MaxGenerationsTextBox.Text, out var maxGenerations) || maxGenerations < 1 ||
            !int.TryParse(TournamentSizeTextBox.Text, out var tournamentSize) || tournamentSize < 1)
        {
            MessageBox.Show("Population, generations and tournament values must be valid positive numbers.", "Invalid solver parameters", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        _customers = VehicleRoutingProblem.CreateCustomers(customerCount, new Random(42));
        _problem = new VehicleRoutingProblem(_customers, new Depot(0, 0), vehicleCount, capacity);

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

        _solver = new GeneticSolver<Customer>(
            _problem,
            new TournamentSelection<Customer>(),
            new OrderCrossover<Customer>(),
            new InversionMutation<Customer>(),
            options);

        return true;
    }

    private void RenderResult(GenerationResult<Customer> result)
    {
        if (_problem is null) return;

        _bestOrder = result.BestChromosome.Genes.ToArray();
        _lastEvaluation = _problem.Evaluate(_bestOrder);
        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F1");
        DistanceTextBlock.Text = _lastEvaluation.Distance.ToString("F1");
        CapacityPenaltyTextBlock.Text = (_lastEvaluation.CapacityPenalty + _lastEvaluation.UnservedPenalty).ToString();
        VehiclesUsedTextBlock.Text = _lastEvaluation.Routes.Count.ToString();
        StatusTextBlock.Text = "Running";

        RouteListBox.Items.Clear();
        foreach (var route in _lastEvaluation.Routes)
        {
            RouteListBox.Items.Add($"Vehicle {route.VehicleId}: load {route.Load,2} | {string.Join(" -> ", route.Customers.Select(c => c.Id))}");
        }

        if (_solver is not null)
        {
            _lastPoolSnapshot = _solver.Population.Take(160).Select(chromosome => chromosome.Clone()).ToArray();
        }

        DrawMap();
        DrawChromosomePool();
        DrawFitnessChart();
    }

    private void DrawMap()
    {
        MapCanvas.Children.Clear();

        if (_customers.Count == 0 || MapCanvas.ActualWidth <= 0 || MapCanvas.ActualHeight <= 0)
        {
            return;
        }

        var padding = 32.0;
        var minX = Math.Min(_customers.Min(c => c.X), 0);
        var maxX = Math.Max(_customers.Max(c => c.X), 0);
        var minY = Math.Min(_customers.Min(c => c.Y), 0);
        var maxY = Math.Max(_customers.Max(c => c.Y), 0);
        var scale = Math.Min(
            (MapCanvas.ActualWidth - padding * 2) / Math.Max(1, maxX - minX),
            (MapCanvas.ActualHeight - padding * 2) / Math.Max(1, maxY - minY));

        Point Project(double x, double y) => new(
            padding + (x - minX) * scale,
            padding + (y - minY) * scale);

        if (_lastEvaluation is not null)
        {
            foreach (var route in _lastEvaluation.Routes)
            {
                var color = VehicleColors[(route.VehicleId - 1) % VehicleColors.Length];
                var brush = new SolidColorBrush(color);
                var previous = Project(0, 0);

                foreach (var customer in route.Customers)
                {
                    var current = Project(customer.X, customer.Y);
                    MapCanvas.Children.Add(new Line { X1 = previous.X, Y1 = previous.Y, X2 = current.X, Y2 = current.Y, Stroke = brush, StrokeThickness = 2.2, Opacity = 0.78 });
                    previous = current;
                }

                var depot = Project(0, 0);
                MapCanvas.Children.Add(new Line { X1 = previous.X, Y1 = previous.Y, X2 = depot.X, Y2 = depot.Y, Stroke = brush, StrokeThickness = 2.2, Opacity = 0.78 });
            }
        }

        var depotPoint = Project(0, 0);
        MapCanvas.Children.Add(new Rectangle { Width = 18, Height = 18, Fill = Brushes.Black, Stroke = Brushes.White, StrokeThickness = 2 });
        Canvas.SetLeft(MapCanvas.Children[^1], depotPoint.X - 9);
        Canvas.SetTop(MapCanvas.Children[^1], depotPoint.Y - 9);

        foreach (var customer in _customers)
        {
            var point = Project(customer.X, customer.Y);
            var marker = new Ellipse { Width = 14, Height = 14, Fill = Brushes.White, Stroke = Brushes.Firebrick, StrokeThickness = 2 };
            Canvas.SetLeft(marker, point.X - 7);
            Canvas.SetTop(marker, point.Y - 7);
            MapCanvas.Children.Add(marker);

            var label = new TextBlock { Text = customer.Id.ToString(), FontSize = 10, FontWeight = FontWeights.SemiBold };
            Canvas.SetLeft(label, point.X + 7);
            Canvas.SetTop(label, point.Y - 8);
            MapCanvas.Children.Add(label);
        }
    }

    private void DrawChromosomePool()
    {
        if (_lastPoolSnapshot.Count == 0 || ChromosomePoolHost.ActualWidth <= 2 || ChromosomePoolHost.ActualHeight <= 2) return;

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
                    var brush = GetCustomerBrush(genes[column]);
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

    private Brush GetCustomerBrush(Customer customer)
    {
        var color = HslToRgb((customer.Id - 1) / (double)Math.Max(1, _customers.Count), 0.68, 0.54);
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
        return Color.FromRgb((byte)Math.Round((r1 + m) * 255), (byte)Math.Round((g1 + m) * 255), (byte)Math.Round((b1 + m) * 255));
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
        CustomerCountTextBox.IsEnabled = !isRunning;
        VehicleCountTextBox.IsEnabled = !isRunning;
        VehicleCapacityTextBox.IsEnabled = !isRunning;
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
