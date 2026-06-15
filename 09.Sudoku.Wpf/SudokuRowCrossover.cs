using GACore;

namespace _09.Sudoku.Wpf;

public sealed class SudokuRowCrossover : ICrossoverOperator<int>
{
    private readonly IReadOnlyList<(int Start, int Length)> _rowRanges;

    public SudokuRowCrossover(IReadOnlyList<(int Start, int Length)> rowRanges)
    {
        _rowRanges = rowRanges;
    }

    public Chromosome<int> Crossover(Chromosome<int> parentA, Chromosome<int> parentB, Random random)
    {
        var child = new int[parentA.Genes.Length];

        foreach (var range in _rowRanges)
        {
            var source = random.Next(2) == 0 ? parentA.Genes : parentB.Genes;

            for (int i = 0; i < range.Length; i++)
            {
                child[range.Start + i] = source[range.Start + i];
            }
        }

        return new Chromosome<int>(child);
    }
}
