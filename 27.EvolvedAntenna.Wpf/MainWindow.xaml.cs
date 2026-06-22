using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GACore;

namespace _27.EvolvedAntenna.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];

    private GeneticSolver<double>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private AntennaEvaluation? _bestEvaluation;
    private IReadOnlyList<Chromosome<double>> _population = [];

    public MainWindow()
    {
        InitializeComponent();
        UpdateParameterLabels();
        DrawAll();
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCreateSolver())
        {
            return;
        }

        SetRunningState(true);
        _evolutionCancellation = new CancellationTokenSource();
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        try
        {
            foreach (var result in _solver!.Run())
            {
                RenderResult(result);

                if (result.BestFitness <= 1.0)
                {
                    StatusTextBlock.Text = "Mission-like design found";
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
        _bestEvaluation = null;
        _population = [];
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        GenerationTextBlock.Text = "0";
        FitnessTextBlock.Text = "-";
        BoresightTextBlock.Text = "-";
        Gain20TextBlock.Text = "-";
        VswrTextBlock.Text = "-";
        SideLobeTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        PopulationListBox.Items.Clear();
        DrawAll();
    }

    private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        UpdateParameterLabels();
    }

    private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawAll();

    private void FitnessCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawFitnessChart();

    private bool TryCreateSolver()
    {
        if (!int.TryParse(PopulationSizeTextBox.Text, out var populationSize) || populationSize < 8 ||
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
            new EvolvedAntennaProblem(),
            new TournamentSelection<double>(),
            new UniformCrossover<double>(),
            new AntennaMutation(),
            options,
            new Random(42));

        return true;
    }

    private void RenderResult(GenerationResult<double> result)
    {
        _bestEvaluation = EvolvedAntennaProblem.Evaluate(result.BestChromosome);
        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        FitnessTextBlock.Text = result.BestFitness.ToString("F2");
        BoresightTextBlock.Text = $"{_bestEvaluation.BoresightGain:F2} dB";
        Gain20TextBlock.Text = $"{_bestEvaluation.Gain20:F2} dB";
        VswrTextBlock.Text = _bestEvaluation.MaxVswr.ToString("F2");
        SideLobeTextBlock.Text = $"{_bestEvaluation.SideLobeMax:F2} dB";
        StatusTextBlock.Text = result.BestFitness <= 1.0 ? "Mission-like design found" : "Running";

        PopulationListBox.Items.Clear();
        if (_solver is not null)
        {
            _population = _solver.Population.Select(chromosome => chromosome.Clone()).ToArray();

            foreach (var chromosome in _solver.Population.Take(10))
            {
                var eval = EvolvedAntennaProblem.Evaluate(chromosome);
                PopulationListBox.Items.Add($"{chromosome.Fitness,8:F2}  gain0={eval.BoresightGain,5:F1}  gain20={eval.Gain20,5:F1}  vswr={eval.MaxVswr,4:F2}");
            }
        }

        DrawAll();
        DrawFitnessChart();
    }

    private void DrawAll()
    {
        DrawGeometry();
        DrawPattern();
    }

    private void DrawGeometry()
    {
        GeometryCanvas.Children.Clear();
        if (GeometryCanvas.ActualWidth <= 0 || GeometryCanvas.ActualHeight <= 0)
        {
            return;
        }

        var evaluation = _bestEvaluation ?? EvolvedAntennaProblem.Evaluate(new EvolvedAntennaProblem().CreateChromosome(new Random(7)));
        var design = evaluation.Design;
        var width = GeometryCanvas.ActualWidth;
        var height = GeometryCanvas.ActualHeight;
        var centerX = width / 2;
        var baseY = height * 0.78;
        var scale = Math.Min(width, height) * 0.14;

        AddText(GeometryCanvas, "crossed-element antenna", 18, 14, Brushes.Black, 13, FontWeights.SemiBold);
        AddText(GeometryCanvas, $"height = {design.HeightLambda:F2} lambda", 18, 34, Brushes.DimGray, 12, FontWeights.Normal);

        GeometryCanvas.Children.Add(new Line
        {
            X1 = centerX,
            Y1 = baseY,
            X2 = centerX,
            Y2 = baseY - design.HeightLambda * scale,
            Stroke = Brushes.SlateGray,
            StrokeThickness = 3
        });

        GeometryCanvas.Children.Add(new Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = new SolidColorBrush(Color.FromRgb(31, 111, 235)),
            Stroke = Brushes.White,
            StrokeThickness = 2
        });
        Canvas.SetLeft(GeometryCanvas.Children[^1], centerX - 8);
        Canvas.SetTop(GeometryCanvas.Children[^1], baseY - 8);

        var y = baseY - 36;
        var colors = new[]
        {
            Color.FromRgb(31, 111, 235),
            Color.FromRgb(34, 197, 94),
            Color.FromRgb(245, 158, 11),
            Color.FromRgb(168, 85, 247)
        };

        for (int i = 0; i < design.Elements.Length; i++)
        {
            y -= design.Elements[i].SpacingLambda * scale;
            var half = design.Elements[i].SizeLambda * scale * 0.62;
            var brush = new SolidColorBrush(colors[i % colors.Length]);

            GeometryCanvas.Children.Add(new Line { X1 = centerX - half, Y1 = y, X2 = centerX + half, Y2 = y, Stroke = brush, StrokeThickness = 6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            GeometryCanvas.Children.Add(new Line { X1 = centerX, Y1 = y - half, X2 = centerX, Y2 = y + half, Stroke = brush, StrokeThickness = 6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            AddText(GeometryCanvas, $"E{i + 1}", centerX + half + 10, y - 9, Brushes.Black, 12, FontWeights.SemiBold);
            AddText(GeometryCanvas, $"s={design.Elements[i].SpacingLambda:F2}, size={design.Elements[i].SizeLambda:F2}", centerX + half + 34, y - 8, Brushes.DimGray, 11, FontWeights.Normal);
        }

        foreach (var chromosome in _population.Take(80))
        {
            var designPreview = EvolvedAntennaProblem.Decode(chromosome.Genes);
            var previewX = 28 + chromosome.Fitness % Math.Max(1, width - 60);
            var previewHeight = designPreview.HeightLambda * 7;
            GeometryCanvas.Children.Add(new Line
            {
                X1 = previewX,
                Y1 = height - 24,
                X2 = previewX,
                Y2 = height - 24 - previewHeight,
                Stroke = new SolidColorBrush(Color.FromArgb(45, 31, 111, 235)),
                StrokeThickness = 1
            });
        }
    }

    private void DrawPattern()
    {
        PatternCanvas.Children.Clear();
        if (PatternCanvas.ActualWidth <= 0 || PatternCanvas.ActualHeight <= 0)
        {
            return;
        }

        var evaluation = _bestEvaluation ?? EvolvedAntennaProblem.Evaluate(new EvolvedAntennaProblem().CreateChromosome(new Random(7)));
        var width = PatternCanvas.ActualWidth;
        var height = PatternCanvas.ActualHeight;
        var centerX = width / 2;
        var centerY = height * 0.72;
        var maxRadius = Math.Min(width * 0.42, height * 0.62);

        for (int ring = 1; ring <= 4; ring++)
        {
            var radius = maxRadius * ring / 4.0;
            var ellipse = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = new SolidColorBrush(Color.FromRgb(224, 229, 236)),
                StrokeThickness = 1
            };
            Canvas.SetLeft(ellipse, centerX - radius);
            Canvas.SetTop(ellipse, centerY - radius);
            PatternCanvas.Children.Add(ellipse);
        }

        DrawPatternAxis(centerX, centerY, maxRadius, 0);
        DrawPatternAxis(centerX, centerY, maxRadius, -20);
        DrawPatternAxis(centerX, centerY, maxRadius, 20);
        DrawTargetArc(centerX, centerY, maxRadius);

        var polyline = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(31, 111, 235)),
            StrokeThickness = 3
        };

        for (int angle = -90; angle <= 90; angle++)
        {
            var gain = EvolvedAntennaProblem.GainAtAngle(evaluation.Design, Math.Abs(angle));
            var radius = Math.Clamp((gain + 2.0) / 20.0, 0.05, 1.0) * maxRadius;
            var radians = (angle - 90) * Math.PI / 180.0;
            polyline.Points.Add(new Point(centerX + Math.Cos(radians) * radius, centerY + Math.Sin(radians) * radius));
        }

        PatternCanvas.Children.Add(polyline);
        AddText(PatternCanvas, "0 deg", centerX - 16, centerY - maxRadius - 22, Brushes.Black, 12, FontWeights.SemiBold);
        AddText(PatternCanvas, "20 deg target", centerX + maxRadius * 0.34, centerY - maxRadius * 0.9, Brushes.DimGray, 12, FontWeights.Normal);
        AddText(PatternCanvas, "green = desired high-gain field of view", 18, 18, Brushes.DimGray, 12, FontWeights.Normal);
    }

    private void DrawTargetArc(double centerX, double centerY, double maxRadius)
    {
        for (int angle = -20; angle <= 20; angle++)
        {
            var radians = (angle - 90) * Math.PI / 180.0;
            PatternCanvas.Children.Add(new Line
            {
                X1 = centerX,
                Y1 = centerY,
                X2 = centerX + Math.Cos(radians) * maxRadius,
                Y2 = centerY + Math.Sin(radians) * maxRadius,
                Stroke = new SolidColorBrush(Color.FromArgb(16, 34, 197, 94)),
                StrokeThickness = 2
            });
        }
    }

    private void DrawPatternAxis(double centerX, double centerY, double radius, double angle)
    {
        var radians = (angle - 90) * Math.PI / 180.0;
        PatternCanvas.Children.Add(new Line
        {
            X1 = centerX,
            Y1 = centerY,
            X2 = centerX + Math.Cos(radians) * radius,
            Y2 = centerY + Math.Sin(radians) * radius,
            Stroke = new SolidColorBrush(Color.FromRgb(190, 198, 210)),
            StrokeThickness = 1
        });
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

    private static void AddText(Canvas canvas, string text, double left, double top, Brush foreground, double fontSize, FontWeight weight)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = foreground,
            FontSize = fontSize,
            FontWeight = weight
        };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        canvas.Children.Add(label);
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

