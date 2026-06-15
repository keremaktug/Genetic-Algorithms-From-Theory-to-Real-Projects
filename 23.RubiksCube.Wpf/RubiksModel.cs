using GACore;

namespace _23.RubiksCube.Wpf;

public sealed class RubiksProblem : IGeneticProblem<int>
{
    private readonly CubeState _scrambled;
    private readonly int _moveCount;

    public RubiksProblem(CubeState scrambled, int moveCount)
    {
        _scrambled = scrambled;
        _moveCount = moveCount;
    }

    public Chromosome<int> CreateChromosome(Random random)
    {
        var genes = new int[_moveCount];

        for (int i = 0; i < genes.Length; i++)
        {
            genes[i] = random.Next(CubeMove.All.Length + 1);
        }

        return new Chromosome<int>(genes);
    }

    public double CalculateFitness(Chromosome<int> chromosome)
    {
        var state = _scrambled.Clone();
        state.Apply(chromosome.Genes.Select(CubeMove.FromGene));
        return state.CountMismatches();
    }
}

public sealed class RubiksMutation : IMutationOperator<int>
{
    public void Mutate(Chromosome<int> chromosome, double mutationRate, Random random)
    {
        for (int i = 0; i < chromosome.Genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                chromosome.Genes[i] = random.Next(CubeMove.All.Length + 1);
            }
        }
    }
}

public sealed class CubeState
{
    private readonly List<Sticker> _stickers;

    public CubeState()
    {
        _stickers = CreateSolvedStickers();
    }

    private CubeState(IEnumerable<Sticker> stickers)
    {
        _stickers = stickers.Select(sticker => sticker.Clone()).ToList();
    }

    public IReadOnlyList<Sticker> Stickers => _stickers;

    public CubeState Clone() => new(_stickers);

    public void Apply(IEnumerable<CubeMove> moves)
    {
        foreach (var move in moves)
        {
            Apply(move);
        }
    }

    public void Apply(CubeMove move)
    {
        if (move.IsNoOp) return;

        foreach (var sticker in _stickers.Where(sticker => move.IsInLayer(sticker.Position)))
        {
            sticker.Position = Rotate(sticker.Position, move.Axis, move.Direction);
            sticker.Normal = Rotate(sticker.Normal, move.Axis, move.Direction);
        }
    }

    public int CountMismatches()
    {
        return _stickers.Count(sticker => sticker.Color != SolvedColor(sticker.Position, sticker.Normal));
    }

    public static CubeState Scramble(IReadOnlyList<CubeMove> moves)
    {
        var cube = new CubeState();
        cube.Apply(moves);
        return cube;
    }

    private static List<Sticker> CreateSolvedStickers()
    {
        var stickers = new List<Sticker>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    var position = new CubeVector(x, y, z);
                    if (x == 1) stickers.Add(new Sticker(position, new CubeVector(1, 0, 0), CubeColor.Red));
                    if (x == -1) stickers.Add(new Sticker(position, new CubeVector(-1, 0, 0), CubeColor.Orange));
                    if (y == 1) stickers.Add(new Sticker(position, new CubeVector(0, 1, 0), CubeColor.White));
                    if (y == -1) stickers.Add(new Sticker(position, new CubeVector(0, -1, 0), CubeColor.Yellow));
                    if (z == 1) stickers.Add(new Sticker(position, new CubeVector(0, 0, 1), CubeColor.Green));
                    if (z == -1) stickers.Add(new Sticker(position, new CubeVector(0, 0, -1), CubeColor.Blue));
                }
            }
        }

        return stickers;
    }

    private static CubeColor SolvedColor(CubeVector position, CubeVector normal)
    {
        if (normal.X == 1) return CubeColor.Red;
        if (normal.X == -1) return CubeColor.Orange;
        if (normal.Y == 1) return CubeColor.White;
        if (normal.Y == -1) return CubeColor.Yellow;
        if (normal.Z == 1) return CubeColor.Green;
        if (normal.Z == -1) return CubeColor.Blue;
        throw new InvalidOperationException($"Invalid sticker normal {normal}.");
    }

    private static CubeVector Rotate(CubeVector v, CubeAxis axis, int direction)
    {
        return (axis, direction) switch
        {
            (CubeAxis.X, 1) => new CubeVector(v.X, -v.Z, v.Y),
            (CubeAxis.X, -1) => new CubeVector(v.X, v.Z, -v.Y),
            (CubeAxis.Y, 1) => new CubeVector(v.Z, v.Y, -v.X),
            (CubeAxis.Y, -1) => new CubeVector(-v.Z, v.Y, v.X),
            (CubeAxis.Z, 1) => new CubeVector(-v.Y, v.X, v.Z),
            (CubeAxis.Z, -1) => new CubeVector(v.Y, -v.X, v.Z),
            _ => v
        };
    }
}

public sealed class Sticker
{
    public Sticker(CubeVector position, CubeVector normal, CubeColor color)
    {
        Position = position;
        Normal = normal;
        Color = color;
    }

    public CubeVector Position { get; set; }

    public CubeVector Normal { get; set; }

    public CubeColor Color { get; }

    public Sticker Clone() => new(Position, Normal, Color);
}

public readonly record struct CubeVector(int X, int Y, int Z);

public enum CubeAxis { X, Y, Z }

public enum CubeColor { White, Yellow, Red, Orange, Green, Blue }

public sealed record CubeMove(string Name, CubeAxis Axis, int Layer, int Direction)
{
    public static readonly CubeMove NoOp = new("-", CubeAxis.X, 0, 0);

    public static readonly CubeMove[] All =
    [
        new("U", CubeAxis.Y, 1, 1),
        new("U'", CubeAxis.Y, 1, -1),
        new("D", CubeAxis.Y, -1, -1),
        new("D'", CubeAxis.Y, -1, 1),
        new("R", CubeAxis.X, 1, 1),
        new("R'", CubeAxis.X, 1, -1),
        new("L", CubeAxis.X, -1, -1),
        new("L'", CubeAxis.X, -1, 1),
        new("F", CubeAxis.Z, 1, 1),
        new("F'", CubeAxis.Z, 1, -1),
        new("B", CubeAxis.Z, -1, -1),
        new("B'", CubeAxis.Z, -1, 1)
    ];

    public bool IsNoOp => Direction == 0;

    public bool IsInLayer(CubeVector position)
    {
        return Axis switch
        {
            CubeAxis.X => position.X == Layer,
            CubeAxis.Y => position.Y == Layer,
            CubeAxis.Z => position.Z == Layer,
            _ => false
        };
    }

    public CubeMove Inverse()
    {
        if (IsNoOp) return this;
        var inverseName = Name.EndsWith("'") ? Name.TrimEnd('\'') : Name + "'";
        return new CubeMove(inverseName, Axis, Layer, -Direction);
    }

    public static CubeMove FromGene(int gene)
    {
        return gene >= 0 && gene < All.Length ? All[gene] : NoOp;
    }

    public static int ToGene(CubeMove move)
    {
        var index = Array.FindIndex(All, candidate => candidate.Name == move.Name);
        return index >= 0 ? index : All.Length;
    }
}
