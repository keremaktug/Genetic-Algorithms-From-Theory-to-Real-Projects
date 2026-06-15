using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GACore;

namespace _19.ImageApproximation.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private GeneticSolver<ApproxCircle>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private ApproxCircle[] _bestCircles = [];

    public MainWindow()
    {
        InitializeComponent();
        UpdateParameterLabels();
        TargetImage.Source = CreateBitmap(ImageApproximationProblem.CreateTargetPixels());
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
        _bestCircles = [];
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        AverageFitnessTextBlock.Text = "-";
        CircleCountTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        PopulationListBox.Items.Clear();
        DrawApproximation();
        DrawFitnessChart();
    }

    private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        UpdateParameterLabels();
    }

    private void ApproximationCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawApproximation();

    private void FitnessCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawFitnessChart();

    private bool TryCreateSolver()
    {
        if (!int.TryParse(CircleCountTextBox.Text, out var circleCount) || circleCount < 4)
        {
            MessageBox.Show("Circle count must be at least 4.", "Invalid circle count", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(PopulationSizeTextBox.Text, out var populationSize) || populationSize < 4 ||
            !int.TryParse(MaxGenerationsTextBox.Text, out var maxGenerations) || maxGenerations < 1 ||
            !int.TryParse(TournamentSizeTextBox.Text, out var tournamentSize) || tournamentSize < 1)
        {
            MessageBox.Show("Population, generations and tournament values must be valid positive numbers.", "Invalid solver parameters", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

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

        _solver = new GeneticSolver<ApproxCircle>(
            new ImageApproximationProblem(circleCount),
            new TournamentSelection<ApproxCircle>(),
            new UniformCrossover<ApproxCircle>(),
            new ApproxCircleMutation(),
            options,
            new Random(42));

        return true;
    }

    private void RenderResult(GenerationResult<ApproxCircle> result)
    {
        _bestCircles = result.BestChromosome.Genes.ToArray();
        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F0");
        AverageFitnessTextBlock.Text = result.AverageFitness.ToString("F0");
        CircleCountTextBlock.Text = _bestCircles.Length.ToString();
        StatusTextBlock.Text = "Running";

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            foreach (var chromosome in _solver.Population.Take(10))
            {
                var c = chromosome.Genes[0];
                PopulationListBox.Items.Add($"{chromosome.Fitness,8:F0}  first circle: ({c.X,4:F1},{c.Y,4:F1}) r={c.Radius,4:F1} rgb=({c.R},{c.G},{c.B})");
            }
        }

        DrawApproximation();
        DrawFitnessChart();
    }

    private void DrawApproximation()
    {
        ApproximationCanvas.Children.Clear();

        if (ApproximationCanvas.ActualWidth <= 0 || ApproximationCanvas.ActualHeight <= 0)
        {
            return;
        }

        var scale = Math.Min(
            ApproximationCanvas.ActualWidth / ImageApproximationProblem.Width,
            ApproximationCanvas.ActualHeight / ImageApproximationProblem.Height);
        var offsetX = (ApproximationCanvas.ActualWidth - ImageApproximationProblem.Width * scale) / 2;
        var offsetY = (ApproximationCanvas.ActualHeight - ImageApproximationProblem.Height * scale) / 2;

        foreach (var circle in _bestCircles)
        {
            var ellipse = new Ellipse
            {
                Width = circle.Radius * 2 * scale,
                Height = circle.Radius * 2 * scale,
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)Math.Round(circle.Alpha * 255),
                    circle.R,
                    circle.G,
                    circle.B)),
                StrokeThickness = 0
            };

            Canvas.SetLeft(ellipse, offsetX + (circle.X - circle.Radius) * scale);
            Canvas.SetTop(ellipse, offsetY + (circle.Y - circle.Radius) * scale);
            ApproximationCanvas.Children.Add(ellipse);
        }
    }

    private static BitmapSource CreateBitmap(IReadOnlyList<Pixel> pixels)
    {
        var raw = new byte[pixels.Count * 4];

        for (int i = 0; i < pixels.Count; i++)
        {
            raw[i * 4] = pixels[i].B;
            raw[i * 4 + 1] = pixels[i].G;
            raw[i * 4 + 2] = pixels[i].R;
            raw[i * 4 + 3] = 255;
        }

        var bitmap = BitmapSource.Create(
            ImageApproximationProblem.Width,
            ImageApproximationProblem.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            raw,
            ImageApproximationProblem.Width * 4);
        bitmap.Freeze();
        return bitmap;
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
        CircleCountTextBox.IsEnabled = !isRunning;
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
