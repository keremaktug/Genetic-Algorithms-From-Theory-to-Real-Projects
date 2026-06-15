namespace GACore;

public sealed class TournamentSelection<TGene> : ISelectionStrategy<TGene>
{
    public Chromosome<TGene> Select(IReadOnlyList<Chromosome<TGene>> population, SolverOptions options, Random random)
    {
        var tournamentSize = Math.Clamp(options.TournamentSize, 1, population.Count);
        Chromosome<TGene>? winner = null;

        for (int i = 0; i < tournamentSize; i++)
        {
            var candidate = population[random.Next(population.Count)];

            if (winner is null || IsBetter(candidate, winner, options.FitnessGoal))
            {
                winner = candidate;
            }
        }

        return winner!;
    }

    private static bool IsBetter(Chromosome<TGene> candidate, Chromosome<TGene> current, FitnessGoal fitnessGoal)
    {
        return fitnessGoal == FitnessGoal.Minimize
            ? candidate.Fitness < current.Fitness
            : candidate.Fitness > current.Fitness;
    }
}
