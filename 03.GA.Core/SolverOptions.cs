namespace GACore;

public sealed class SolverOptions
{
    public int PopulationSize { get; set; } = 100;

    public int MaxGenerations { get; set; } = 1000;

    public double ElitismRate { get; set; } = 0.10;

    public double MutationRate { get; set; } = 0.01;

    public FitnessGoal FitnessGoal { get; set; } = FitnessGoal.Minimize;

    public double? TargetFitness { get; set; } = 0;

    public double FitnessTolerance { get; set; } = 0.000001;

    public int TournamentSize { get; set; } = 3;
}
