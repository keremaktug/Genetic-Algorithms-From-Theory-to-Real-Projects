using GACore;

namespace _06.EightQueens.Wpf;

public sealed class EightQueensProblem : IGeneticProblem<int>
{
    public Chromosome<int> CreateChromosome(Random random)
    {
        var genes = new int[8];

        for (int column = 0; column < genes.Length; column++)
        {
            genes[column] = random.Next(8);
        }

        return new Chromosome<int>(genes);
    }

    public double CalculateFitness(Chromosome<int> chromosome)
    {
        var conflicts = 0;

        for (int leftColumn = 0; leftColumn < chromosome.Genes.Length; leftColumn++)
        {
            for (int rightColumn = leftColumn + 1; rightColumn < chromosome.Genes.Length; rightColumn++)
            {
                var leftRow = chromosome.Genes[leftColumn];
                var rightRow = chromosome.Genes[rightColumn];
                var sameRow = leftRow == rightRow;
                var sameDiagonal = Math.Abs(leftColumn - rightColumn) == Math.Abs(leftRow - rightRow);

                if (sameRow || sameDiagonal)
                {
                    conflicts++;
                }
            }
        }

        return conflicts;
    }

    public static HashSet<int> GetConflictingColumns(IReadOnlyList<int> genes)
    {
        var result = new HashSet<int>();

        for (int leftColumn = 0; leftColumn < genes.Count; leftColumn++)
        {
            for (int rightColumn = leftColumn + 1; rightColumn < genes.Count; rightColumn++)
            {
                var leftRow = genes[leftColumn];
                var rightRow = genes[rightColumn];
                var sameRow = leftRow == rightRow;
                var sameDiagonal = Math.Abs(leftColumn - rightColumn) == Math.Abs(leftRow - rightRow);

                if (sameRow || sameDiagonal)
                {
                    result.Add(leftColumn);
                    result.Add(rightColumn);
                }
            }
        }

        return result;
    }
}
