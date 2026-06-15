using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using GACore;

namespace _23.RubiksCube.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private GeneticSolver<int>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private CubeState _scrambledCube = new();
    private CubeState _displayCube = new();
    private IReadOnlyList<CubeMove> _scrambleMoves = [];
    private int[] _bestGenes = [];
    private double _yaw = -32;
    private double _pitch = -24;
    private double _distance = 11.8;
    private Point _lastMouse;
    private bool _isDragging;

    public MainWindow()
    {
        InitializeComponent();
        UpdateParameterLabels();
        CreateScramble(5);
        DrawCube();
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

                if (result.BestFitness <= 0)
                {
                    StatusTextBlock.Text = "Solved by GA";
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

    private void ScrambleButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(ScrambleDepthTextBox.Text, out var depth) || depth < 1)
        {
            depth = 5;
        }

        CreateScramble(depth);
        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        BestMovesTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();
        DrawCube();
        DrawFitnessChart();
    }

    private void InverseButton_Click(object sender, RoutedEventArgs e)
    {
        var inverse = _scrambleMoves.Reverse().Select(move => move.Inverse()).ToArray();
        _displayCube = _scrambledCube.Clone();
        _displayCube.Apply(inverse);
        BestMovesTextBlock.Text = ToMoveText(inverse);
        BestFitnessTextBlock.Text = _displayCube.CountMismatches().ToString();
        StatusTextBlock.Text = "Inverse solution applied";
        DrawCube();
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e) => _evolutionCancellation?.Cancel();

    private void ResetViewButton_Click(object sender, RoutedEventArgs e)
    {
        _yaw = -32;
        _pitch = -24;
        _distance = 11.8;
        DrawCube();
    }

    private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        UpdateParameterLabels();
    }

    private void FitnessCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawFitnessChart();

    private void CreateScramble(int depth)
    {
        var random = new Random(42 + depth + Environment.TickCount % 997);
        var moves = new List<CubeMove>();
        CubeMove? previous = null;

        for (int i = 0; i < depth; i++)
        {
            CubeMove move;
            do
            {
                move = CubeMove.All[random.Next(CubeMove.All.Length)];
            }
            while (previous is not null && move.Axis == previous.Axis && move.Layer == previous.Layer);

            moves.Add(move);
            previous = move;
        }

        _scrambleMoves = moves;
        _scrambledCube = CubeState.Scramble(_scrambleMoves);
        _displayCube = _scrambledCube.Clone();
        ScrambleTextBlock.Text = ToMoveText(_scrambleMoves);
    }

    private bool TryCreateSolver()
    {
        if (!int.TryParse(MoveCountTextBox.Text, out var moveCount) || moveCount < 1 ||
            !int.TryParse(PopulationSizeTextBox.Text, out var populationSize) || populationSize < 4 ||
            !int.TryParse(MaxGenerationsTextBox.Text, out var maxGenerations) || maxGenerations < 1 ||
            !int.TryParse(TournamentSizeTextBox.Text, out var tournamentSize) || tournamentSize < 1)
        {
            MessageBox.Show("Move count, population, generations and tournament values must be valid positive numbers.", "Invalid solver parameters", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            new RubiksProblem(_scrambledCube, moveCount),
            new TournamentSelection<int>(),
            new UniformCrossover<int>(),
            new RubiksMutation(),
            options,
            new Random(42));

        return true;
    }

    private void RenderResult(GenerationResult<int> result)
    {
        _bestGenes = result.BestChromosome.Genes.ToArray();
        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        _displayCube = _scrambledCube.Clone();
        _displayCube.Apply(_bestGenes.Select(CubeMove.FromGene));

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F0");
        BestMovesTextBlock.Text = ToMoveText(_bestGenes.Select(CubeMove.FromGene));
        StatusTextBlock.Text = "Running";

        DrawCube();
        DrawFitnessChart();
    }

    private void DrawCube()
    {
        CubeViewport.Children.Clear();

        var camera = new PerspectiveCamera
        {
            Position = CameraPosition(),
            LookDirection = new Vector3D(-CameraPosition().X, -CameraPosition().Y, -CameraPosition().Z),
            UpDirection = new Vector3D(0, 1, 0),
            FieldOfView = 45
        };
        CubeViewport.Camera = camera;

        var group = new Model3DGroup();
        group.Children.Add(new AmbientLight(Color.FromRgb(120, 120, 120)));
        group.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-2, -3, -4)));

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    group.Children.Add(CreateCubieModel(x, y, z));
                }
            }
        }

        foreach (var sticker in _displayCube.Stickers)
        {
            group.Children.Add(CreateStickerModel(sticker));
        }

        CubeViewport.Children.Add(new ModelVisual3D { Content = group });
    }

    private Point3D CameraPosition()
    {
        var yaw = _yaw * Math.PI / 180;
        var pitch = _pitch * Math.PI / 180;
        return new Point3D(
            _distance * Math.Cos(pitch) * Math.Sin(yaw),
            _distance * Math.Sin(-pitch),
            _distance * Math.Cos(pitch) * Math.Cos(yaw));
    }

    private static GeometryModel3D CreateStickerModel(Sticker sticker)
    {
        var mesh = new MeshGeometry3D();
        var n = new Vector3D(sticker.Normal.X, sticker.Normal.Y, sticker.Normal.Z);
        var center = new Point3D(sticker.Position.X * 1.0 + n.X * 0.512, sticker.Position.Y * 1.0 + n.Y * 0.512, sticker.Position.Z * 1.0 + n.Z * 0.512);
        var size = 0.80;

        Vector3D u;
        Vector3D v;
        if (Math.Abs(n.X) > 0)
        {
            u = new Vector3D(0, size, 0);
            v = new Vector3D(0, 0, size);
        }
        else if (Math.Abs(n.Y) > 0)
        {
            u = new Vector3D(size, 0, 0);
            v = new Vector3D(0, 0, size);
        }
        else
        {
            u = new Vector3D(size, 0, 0);
            v = new Vector3D(0, size, 0);
        }

        mesh.Positions.Add(center - u / 2 - v / 2);
        mesh.Positions.Add(center + u / 2 - v / 2);
        mesh.Positions.Add(center + u / 2 + v / 2);
        mesh.Positions.Add(center - u / 2 + v / 2);
        mesh.TriangleIndices.Add(0);
        mesh.TriangleIndices.Add(1);
        mesh.TriangleIndices.Add(2);
        mesh.TriangleIndices.Add(0);
        mesh.TriangleIndices.Add(2);
        mesh.TriangleIndices.Add(3);

        var material = new DiffuseMaterial(new SolidColorBrush(ToColor(sticker.Color)));
        var model = new GeometryModel3D(mesh, material) { BackMaterial = material };
        return model;
    }

    private static GeometryModel3D CreateCubieModel(int x, int y, int z)
    {
        var mesh = new MeshGeometry3D();
        const double half = 0.50;
        var cx = x * 1.0;
        var cy = y * 1.0;
        var cz = z * 1.0;

        AddFace(mesh,
            new Point3D(cx + half, cy - half, cz - half),
            new Point3D(cx + half, cy + half, cz - half),
            new Point3D(cx + half, cy + half, cz + half),
            new Point3D(cx + half, cy - half, cz + half));
        AddFace(mesh,
            new Point3D(cx - half, cy - half, cz + half),
            new Point3D(cx - half, cy + half, cz + half),
            new Point3D(cx - half, cy + half, cz - half),
            new Point3D(cx - half, cy - half, cz - half));
        AddFace(mesh,
            new Point3D(cx - half, cy + half, cz - half),
            new Point3D(cx - half, cy + half, cz + half),
            new Point3D(cx + half, cy + half, cz + half),
            new Point3D(cx + half, cy + half, cz - half));
        AddFace(mesh,
            new Point3D(cx - half, cy - half, cz + half),
            new Point3D(cx - half, cy - half, cz - half),
            new Point3D(cx + half, cy - half, cz - half),
            new Point3D(cx + half, cy - half, cz + half));
        AddFace(mesh,
            new Point3D(cx - half, cy - half, cz + half),
            new Point3D(cx + half, cy - half, cz + half),
            new Point3D(cx + half, cy + half, cz + half),
            new Point3D(cx - half, cy + half, cz + half));
        AddFace(mesh,
            new Point3D(cx + half, cy - half, cz - half),
            new Point3D(cx - half, cy - half, cz - half),
            new Point3D(cx - half, cy + half, cz - half),
            new Point3D(cx + half, cy + half, cz - half));

        var plastic = new MaterialGroup();
        plastic.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(246, 247, 249))));
        plastic.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(255, 255, 255)), 35));

        return new GeometryModel3D(mesh, plastic) { BackMaterial = plastic };
    }

    private static void AddFace(MeshGeometry3D mesh, Point3D p0, Point3D p1, Point3D p2, Point3D p3)
    {
        var index = mesh.Positions.Count;
        mesh.Positions.Add(p0);
        mesh.Positions.Add(p1);
        mesh.Positions.Add(p2);
        mesh.Positions.Add(p3);
        mesh.TriangleIndices.Add(index);
        mesh.TriangleIndices.Add(index + 1);
        mesh.TriangleIndices.Add(index + 2);
        mesh.TriangleIndices.Add(index);
        mesh.TriangleIndices.Add(index + 2);
        mesh.TriangleIndices.Add(index + 3);
    }

    private static Color ToColor(CubeColor color)
    {
        return color switch
        {
            CubeColor.White => Colors.White,
            CubeColor.Yellow => Color.FromRgb(250, 204, 21),
            CubeColor.Red => Color.FromRgb(220, 38, 38),
            CubeColor.Orange => Color.FromRgb(249, 115, 22),
            CubeColor.Green => Color.FromRgb(22, 163, 74),
            CubeColor.Blue => Color.FromRgb(37, 99, 235),
            _ => Colors.Gray
        };
    }

    private void CubeViewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _lastMouse = e.GetPosition(this);
        CubeViewport.CaptureMouse();
    }

    private void CubeViewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var current = e.GetPosition(this);
        var delta = current - _lastMouse;
        _yaw += delta.X * 0.45;
        _pitch = Math.Clamp(_pitch + delta.Y * 0.35, -80, 80);
        _lastMouse = current;
        DrawCube();
    }

    private void CubeViewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        CubeViewport.ReleaseMouseCapture();
    }

    private void CubeViewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _distance = Math.Clamp(_distance - e.Delta * 0.003, 4.0, 14.0);
        DrawCube();
    }

    private static string ToMoveText(IEnumerable<CubeMove> moves)
    {
        return string.Join(" ", moves.Where(move => !move.IsNoOp).Select(move => move.Name));
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
        ScrambleButton.IsEnabled = !isRunning;
        InverseButton.IsEnabled = !isRunning;
        PauseButton.IsEnabled = isRunning;
        ScrambleDepthTextBox.IsEnabled = !isRunning;
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
