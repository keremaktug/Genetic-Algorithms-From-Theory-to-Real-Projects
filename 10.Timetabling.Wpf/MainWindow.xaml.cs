using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GACore;

namespace _10.Timetabling.Wpf;

public partial class MainWindow : Window
{
    private readonly List<double> _bestFitnessHistory = [];
    private readonly List<double> _averageFitnessHistory = [];
    private readonly TimetablingProblem _problem = new();

    private GeneticSolver<CourseAssignment>? _solver;
    private CancellationTokenSource? _evolutionCancellation;
    private CourseAssignment[] _bestAssignments = [];

    public MainWindow()
    {
        InitializeComponent();
        UpdateParameterLabels();
        DrawTimetable();
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
                    StatusTextBlock.Text = "Feasible timetable found";
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
        _bestAssignments = [];
        _bestFitnessHistory.Clear();
        _averageFitnessHistory.Clear();

        GenerationTextBlock.Text = "0";
        BestFitnessTextBlock.Text = "-";
        HardPenaltyTextBlock.Text = "-";
        SoftPenaltyTextBlock.Text = "-";
        StatusTextBlock.Text = "Ready";
        ViolationsListBox.Items.Clear();
        DrawTimetable();
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

        _solver = new GeneticSolver<CourseAssignment>(
            _problem,
            new TournamentSelection<CourseAssignment>(),
            new UniformCrossover<CourseAssignment>(),
            new TimetablingMutation(),
            options);

        return true;
    }

    private void RenderResult(GenerationResult<CourseAssignment> result)
    {
        _bestAssignments = result.BestChromosome.Genes.ToArray();
        var evaluation = _problem.Evaluate(_bestAssignments);

        _bestFitnessHistory.Add(result.BestFitness);
        _averageFitnessHistory.Add(result.AverageFitness);

        GenerationTextBlock.Text = result.Generation.ToString();
        BestFitnessTextBlock.Text = result.BestFitness.ToString("F0");
        HardPenaltyTextBlock.Text = evaluation.HardPenalty.ToString();
        SoftPenaltyTextBlock.Text = evaluation.SoftPenalty.ToString();
        StatusTextBlock.Text = result.IsSolutionFound ? "Feasible timetable found" : "Running";

        ViolationsListBox.Items.Clear();
        foreach (var violation in evaluation.Violations.Take(30))
        {
            ViolationsListBox.Items.Add(violation);
        }

        DrawTimetable();
        DrawFitnessChart();
    }

    private void DrawTimetable()
    {
        TimetableGrid.Children.Clear();
        TimetableGrid.RowDefinitions.Clear();
        TimetableGrid.ColumnDefinitions.Clear();

        TimetableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        for (int day = 0; day < TimetablingProblem.Days.Length; day++)
        {
            TimetableGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        TimetableGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
        for (int slot = 0; slot < TimetablingProblem.SlotsPerDay; slot++)
        {
            TimetableGrid.RowDefinitions.Add(new RowDefinition());
        }

        AddCell("", 0, 0, true, Brushes.White);
        for (int day = 0; day < TimetablingProblem.Days.Length; day++)
        {
            AddCell(TimetablingProblem.Days[day], 0, day + 1, true, new SolidColorBrush(Color.FromRgb(235, 239, 245)));
        }

        for (int slot = 0; slot < TimetablingProblem.SlotsPerDay; slot++)
        {
            AddCell($"S{slot + 1}", slot + 1, 0, true, new SolidColorBrush(Color.FromRgb(235, 239, 245)));
        }

        var byTime = _bestAssignments
            .Select((assignment, index) => new { Assignment = assignment, Course = TimetablingProblem.Courses[index] })
            .GroupBy(item => item.Assignment.TimeSlot);

        foreach (var group in byTime)
        {
            var row = group.Key % TimetablingProblem.SlotsPerDay + 1;
            var col = group.Key / TimetablingProblem.SlotsPerDay + 1;
            var text = string.Join("\n", group.Select(item => $"{item.Course.Code} {TimetablingProblem.Rooms[item.Assignment.RoomIndex].Name}"));
            var hasConflict = group.GroupBy(item => item.Assignment.RoomIndex).Any(roomGroup => roomGroup.Count() > 1)
                || group.GroupBy(item => item.Course.Teacher).Any(teacherGroup => teacherGroup.Count() > 1);
            AddCell(text, row, col, false, hasConflict ? new SolidColorBrush(Color.FromRgb(255, 229, 225)) : Brushes.White);
        }
    }

    private void AddCell(string text, int row, int column, bool isHeader, Brush background)
    {
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(215, 220, 226)),
            BorderThickness = new Thickness(1),
            Background = background,
            Padding = new Thickness(6),
            Child = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = isHeader ? 13 : 12,
                FontWeight = isHeader ? FontWeights.SemiBold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
        };

        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        TimetableGrid.Children.Add(border);
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
