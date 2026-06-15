using GACore;

namespace _09.Sudoku.Wpf;

public sealed class SudokuProblem : IGeneticProblem<int>
{
    private const string Puzzle =
        "530070000" +
        "600195000" +
        "098000060" +
        "800060003" +
        "400803001" +
        "700020006" +
        "060000280" +
        "000419005" +
        "000080079";

    private readonly int[,] _fixedGrid = new int[9, 9];
    private readonly List<(int Row, int Col)>[] _emptyCellsByRow = Enumerable.Range(0, 9).Select(_ => new List<(int Row, int Col)>()).ToArray();
    private readonly int[][] _valuesToPlaceByRow = new int[9][];
    private readonly List<(int Start, int Length)> _rowRanges = [];

    public SudokuProblem()
    {
        var counts = Enumerable.Range(1, 9).ToDictionary(value => value, _ => 9);

        for (int i = 0; i < Puzzle.Length; i++)
        {
            var row = i / 9;
            var col = i % 9;
            var value = Puzzle[i] - '0';
            _fixedGrid[row, col] = value;

            if (value == 0)
            {
                _emptyCellsByRow[row].Add((row, col));
            }
            else
            {
                counts[value]--;
            }
        }

        var start = 0;

        for (int row = 0; row < 9; row++)
        {
            var existingValues = Enumerable.Range(0, 9)
                .Select(col => _fixedGrid[row, col])
                .Where(value => value != 0)
                .ToHashSet();

            _valuesToPlaceByRow[row] = Enumerable.Range(1, 9)
                .Where(value => !existingValues.Contains(value))
                .ToArray();

            _rowRanges.Add((start, _valuesToPlaceByRow[row].Length));
            start += _valuesToPlaceByRow[row].Length;
        }
    }

    public int EmptyCellCount => _rowRanges.Sum(range => range.Length);

    public IReadOnlyList<(int Start, int Length)> RowRanges => _rowRanges;

    public Chromosome<int> CreateChromosome(Random random)
    {
        var genes = new List<int>();

        for (int row = 0; row < 9; row++)
        {
            var rowValues = _valuesToPlaceByRow[row].ToArray();

            for (int i = rowValues.Length - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                (rowValues[i], rowValues[swapIndex]) = (rowValues[swapIndex], rowValues[i]);
            }

            genes.AddRange(rowValues);
        }

        return new Chromosome<int>(genes);
    }

    public double CalculateFitness(Chromosome<int> chromosome)
    {
        var grid = Decode(chromosome.Genes);
        return CountDuplicateScore(grid);
    }

    public bool IsFixed(int row, int col)
    {
        return _fixedGrid[row, col] != 0;
    }

    public int[,] Decode(IReadOnlyList<int> genes)
    {
        var grid = (int[,])_fixedGrid.Clone();

        for (int row = 0; row < 9; row++)
        {
            var range = _rowRanges[row];

            for (int i = 0; i < range.Length && range.Start + i < genes.Count; i++)
            {
                var cell = _emptyCellsByRow[row][i];
                grid[cell.Row, cell.Col] = genes[range.Start + i];
            }
        }

        return grid;
    }

    public HashSet<(int Row, int Col)> GetConflictCells(int[,] grid)
    {
        var conflicts = new HashSet<(int Row, int Col)>();

        for (int i = 0; i < 9; i++)
        {
            AddGroupConflicts(Enumerable.Range(0, 9).Select(col => (Row: i, Col: col)), grid, conflicts);
            AddGroupConflicts(Enumerable.Range(0, 9).Select(row => (Row: row, Col: i)), grid, conflicts);
        }

        for (int boxRow = 0; boxRow < 3; boxRow++)
        {
            for (int boxCol = 0; boxCol < 3; boxCol++)
            {
                var cells =
                    from rowOffset in Enumerable.Range(0, 3)
                    from colOffset in Enumerable.Range(0, 3)
                    select (Row: boxRow * 3 + rowOffset, Col: boxCol * 3 + colOffset);

                AddGroupConflicts(cells, grid, conflicts);
            }
        }

        return conflicts;
    }

    private static int CountDuplicateScore(int[,] grid)
    {
        var score = 0;

        for (int i = 0; i < 9; i++)
        {
            score += CountDuplicates(Enumerable.Range(0, 9).Select(row => grid[row, i]));
        }

        for (int boxRow = 0; boxRow < 3; boxRow++)
        {
            for (int boxCol = 0; boxCol < 3; boxCol++)
            {
                var values =
                    from rowOffset in Enumerable.Range(0, 3)
                    from colOffset in Enumerable.Range(0, 3)
                    select grid[boxRow * 3 + rowOffset, boxCol * 3 + colOffset];

                score += CountDuplicates(values);
            }
        }

        return score;
    }

    private static int CountDuplicates(IEnumerable<int> values)
    {
        var presentValues = values.Where(value => value != 0).ToArray();
        return presentValues.Length - presentValues.Distinct().Count();
    }

    private static void AddGroupConflicts(
        IEnumerable<(int Row, int Col)> cells,
        int[,] grid,
        HashSet<(int Row, int Col)> conflicts)
    {
        foreach (var group in cells.GroupBy(cell => grid[cell.Row, cell.Col]).Where(group => group.Key != 0 && group.Count() > 1))
        {
            foreach (var cell in group)
            {
                conflicts.Add(cell);
            }
        }
    }
}
