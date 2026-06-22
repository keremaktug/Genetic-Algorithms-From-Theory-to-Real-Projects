using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using GACore;

namespace _21.Rastrigin.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private GeneticSolver<double>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private IReadOnlyList<Chromosome<double>> _population = [];
    private double[] _bestGenes = [];

    private const double SurfaceHeightScale = 1.35;

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

                if (result.BestFitness <= 0.001)
                {
                    StatusTextBlock.Text = "Near global minimum";
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
        _population = [];
        _bestGenes = [];
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        XTextBlock.Text = "-";
        YTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        PopulationListBox.Items.Clear();
        DrawSurface();
        DrawFitnessChart();
    }

    private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        UpdateParameterLabels();
    }

    private void SurfaceViewport_SizeChanged(object sender, SizeChangedEventArgs e) => DrawSurface();

    private void FitnessCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawFitnessChart();

    private bool TryCreateSolver()
    {
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

        _solver = new GeneticSolver<double>(
            new RastriginProblem(),
            new TournamentSelection<double>(),
            new UniformCrossover<double>(),
            new RealValueMutation(),
            options,
            new Random(42));

        return true;
    }

    private void RenderResult(GenerationResult<double> result)
    {
        _bestGenes = result.BestChromosome.Genes.ToArray();
        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F5");
        XTextBlock.Text = _bestGenes[0].ToString("F4");
        YTextBlock.Text = _bestGenes[1].ToString("F4");
        StatusTextBlock.Text = "Running";

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            _population = _solver.Population.Select(chromosome => chromosome.Clone()).ToArray();

            foreach (var chromosome in _solver.Population.Take(12))
            {
                PopulationListBox.Items.Add($"{chromosome.Fitness,9:F4}  x={chromosome.Genes[0],7:F3}  y={chromosome.Genes[1],7:F3}");
            }
        }

        DrawSurface();
        DrawFitnessChart();
    }

    private void DrawSurface()
    {
        SurfaceViewport.Children.Clear();

        if (SurfaceViewport.ActualWidth <= 0 || SurfaceViewport.ActualHeight <= 0)
        {
            return;
        }

        SurfaceViewport.Camera = new PerspectiveCamera
        {
            Position = new Point3D(7.8, 9.6, 10.8),
            LookDirection = new Vector3D(-7.8, -8.6, -10.8),
            UpDirection = new Vector3D(0, 1, 0),
            FieldOfView = 48
        };

        var scene = new Model3DGroup();
        scene.Children.Add(new AmbientLight(Color.FromRgb(175, 184, 196)));
        scene.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-0.45, -0.9, -0.55)));
        scene.Children.Add(CreateSurfaceModel());
        scene.Children.Add(CreateBaseGridModel());

        foreach (var chromosome in _population.Take(250))
        {
            scene.Children.Add(CreateMarker(chromosome.Genes[0], chromosome.Genes[1], 0.052, Color.FromArgb(210, 30, 41, 59)));
        }

        if (_bestGenes.Length == 2)
        {
            scene.Children.Add(CreateMarker(0, 0, 0.105, Colors.Gold));
            scene.Children.Add(CreateMarker(_bestGenes[0], _bestGenes[1], 0.14, Color.FromRgb(220, 38, 38)));
        }

        SurfaceViewport.Children.Add(new ModelVisual3D { Content = scene });
    }

    private static Model3DGroup CreateSurfaceModel()
    {
        const int cells = 62;
        var group = new Model3DGroup();

        for (int row = 0; row < cells; row++)
        {
            for (int col = 0; col < cells; col++)
            {
                var x0 = RastriginProblem.Min + col / (double)cells * (RastriginProblem.Max - RastriginProblem.Min);
                var x1 = RastriginProblem.Min + (col + 1) / (double)cells * (RastriginProblem.Max - RastriginProblem.Min);
                var y0 = RastriginProblem.Min + row / (double)cells * (RastriginProblem.Max - RastriginProblem.Min);
                var y1 = RastriginProblem.Min + (row + 1) / (double)cells * (RastriginProblem.Max - RastriginProblem.Min);
                var centerValue = Math.Min(80, RastriginProblem.Evaluate((x0 + x1) / 2, (y0 + y1) / 2));

                group.Children.Add(CreateSurfaceCell(x0, y0, x1, y1, HeatColor(centerValue / 80.0)));
            }
        }

        return group;
    }

    private static GeometryModel3D CreateSurfaceCell(double x0, double y0, double x1, double y1, Color color)
    {
        var mesh = new MeshGeometry3D();
        mesh.Positions.Add(ToSurfacePoint(x0, y0));
        mesh.Positions.Add(ToSurfacePoint(x1, y0));
        mesh.Positions.Add(ToSurfacePoint(x1, y1));
        mesh.Positions.Add(ToSurfacePoint(x0, y1));
        mesh.TriangleIndices.Add(0);
        mesh.TriangleIndices.Add(1);
        mesh.TriangleIndices.Add(2);
        mesh.TriangleIndices.Add(0);
        mesh.TriangleIndices.Add(2);
        mesh.TriangleIndices.Add(3);

        var material = new DiffuseMaterial(new SolidColorBrush(color));
        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private static Model3DGroup CreateBaseGridModel()
    {
        var group = new Model3DGroup();
        var axisMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(135, 71, 85, 105)));

        for (var i = -5; i <= 5; i++)
        {
            group.Children.Add(CreateLineTube(new Point3D(-5.12, -0.05, i), new Point3D(5.12, -0.05, i), 0.01, axisMaterial));
            group.Children.Add(CreateLineTube(new Point3D(i, -0.05, -5.12), new Point3D(i, -0.05, 5.12), 0.01, axisMaterial));
        }

        return group;
    }

    private static GeometryModel3D CreateLineTube(Point3D a, Point3D b, double radius, Material material)
    {
        var vector = b - a;
        var length = vector.Length;
        var center = a + vector * 0.5;
        var mesh = new MeshGeometry3D();
        const int sides = 8;
        var up = Math.Abs(Vector3D.DotProduct(vector, new Vector3D(0, 1, 0))) > 0.9 * length
            ? new Vector3D(1, 0, 0)
            : new Vector3D(0, 1, 0);
        var right = Vector3D.CrossProduct(vector, up);
        right.Normalize();
        up = Vector3D.CrossProduct(right, vector);
        up.Normalize();
        vector.Normalize();

        for (var i = 0; i < sides; i++)
        {
            var angle = i * 2 * Math.PI / sides;
            var offset = right * (Math.Cos(angle) * radius) + up * (Math.Sin(angle) * radius);
            mesh.Positions.Add(center - vector * (length / 2) + offset);
            mesh.Positions.Add(center + vector * (length / 2) + offset);
        }

        for (var i = 0; i < sides; i++)
        {
            var next = (i + 1) % sides;
            mesh.TriangleIndices.Add(i * 2);
            mesh.TriangleIndices.Add(next * 2);
            mesh.TriangleIndices.Add(next * 2 + 1);
            mesh.TriangleIndices.Add(i * 2);
            mesh.TriangleIndices.Add(next * 2 + 1);
            mesh.TriangleIndices.Add(i * 2 + 1);
        }

        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private static GeometryModel3D CreateMarker(double x, double y, double radius, Color color)
    {
        var center = ToSurfacePoint(x, y);
        center.Y += 0.08;
        var mesh = CreateSphereMesh(center, radius, 12, 8);
        var material = new DiffuseMaterial(new SolidColorBrush(color));
        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private static MeshGeometry3D CreateSphereMesh(Point3D center, double radius, int longitudeSegments, int latitudeSegments)
    {
        var mesh = new MeshGeometry3D();

        for (var lat = 0; lat <= latitudeSegments; lat++)
        {
            var theta = lat * Math.PI / latitudeSegments;
            var sinTheta = Math.Sin(theta);
            var cosTheta = Math.Cos(theta);

            for (var lon = 0; lon <= longitudeSegments; lon++)
            {
                var phi = lon * 2 * Math.PI / longitudeSegments;
                mesh.Positions.Add(new Point3D(
                    center.X + radius * sinTheta * Math.Cos(phi),
                    center.Y + radius * cosTheta,
                    center.Z + radius * sinTheta * Math.Sin(phi)));
            }
        }

        for (var lat = 0; lat < latitudeSegments; lat++)
        {
            for (var lon = 0; lon < longitudeSegments; lon++)
            {
                var first = lat * (longitudeSegments + 1) + lon;
                var second = first + longitudeSegments + 1;
                mesh.TriangleIndices.Add(first);
                mesh.TriangleIndices.Add(second);
                mesh.TriangleIndices.Add(first + 1);
                mesh.TriangleIndices.Add(second);
                mesh.TriangleIndices.Add(second + 1);
                mesh.TriangleIndices.Add(first + 1);
            }
        }

        return mesh;
    }

    private static Point3D ToSurfacePoint(double x, double y)
    {
        var value = Math.Min(80, RastriginProblem.Evaluate(x, y));
        return new Point3D(x, value / 80.0 * SurfaceHeightScale, y);
    }

    private static Color HeatColor(double t)
    {
        t = Math.Clamp(t, 0, 1);
        var start = Color.FromRgb(37, 99, 235);
        var midLow = Color.FromRgb(56, 189, 248);
        var midHigh = Color.FromRgb(250, 204, 21);
        var end = Color.FromRgb(249, 115, 22);

        if (t < 0.34)
        {
            return Interpolate(start, midLow, t / 0.34);
        }

        if (t < 0.68)
        {
            return Interpolate(midLow, midHigh, (t - 0.34) / 0.34);
        }

        return Interpolate(midHigh, end, (t - 0.68) / 0.32);
    }

    private static Color Interpolate(Color a, Color b, double t)
    {
        return Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
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
