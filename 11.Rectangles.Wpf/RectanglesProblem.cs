using System.Windows.Media;
using GACore;

namespace _11.Rectangles.Wpf;

public sealed class RectanglesProblem : IGeneticProblem<RectanglePlacement>
{
    public const int BoardWidth = 19;
    public const int BoardHeight = 19;

    public static readonly RectangleShape[] Shapes =
    [
        new(1, 8, 7, Colors.Blue),
        new(2, 5, 3, Colors.Red),
        new(3, 2, 6, Colors.Green),
        new(4, 6, 4, Colors.Brown),
        new(5, 3, 3, Colors.Chartreuse),
        new(6, 6, 5, Colors.DarkBlue),
        new(7, 1, 2, Colors.DarkCyan),
        new(8, 2, 1, Colors.DarkOrange),
        new(9, 1, 3, Colors.DarkOrchid),
        new(10, 1, 1, Colors.BurlyWood),
        new(11, 2, 1, Colors.Cyan)
    ];

    public Chromosome<RectanglePlacement> CreateChromosome(Random random)
    {
        var genes = new RectanglePlacement[Shapes.Length];

        for (int i = 0; i < genes.Length; i++)
        {
            genes[i] = RectanglePlacement.Random(random);
        }

        return new Chromosome<RectanglePlacement>(genes);
    }

    public double CalculateFitness(Chromosome<RectanglePlacement> chromosome)
    {
        var evaluation = Evaluate(chromosome.Genes);
        return evaluation.OverlapArea * 5 + evaluation.BoundingBoxArea * 2 + evaluation.OutsidePenalty * 10;
    }

    public PackingEvaluation Evaluate(IReadOnlyList<RectanglePlacement> placements)
    {
        var rects = ToPlacedRectangles(placements);
        var bbox = CalculateBoundingBox(rects);
        var bboxArea = Math.Max(0, bbox.Right - bbox.Left) * Math.Max(0, bbox.Bottom - bbox.Top);
        var overlapArea = CalculateOverlapArea(rects);
        var outsidePenalty = CalculateOutsidePenalty(rects);
        return new PackingEvaluation(bbox, bboxArea, overlapArea, outsidePenalty);
    }

    public IReadOnlyList<PlacedRectangle> ToPlacedRectangles(IReadOnlyList<RectanglePlacement> placements)
    {
        var result = new List<PlacedRectangle>();

        for (int i = 0; i < placements.Count && i < Shapes.Length; i++)
        {
            var shape = Shapes[i];
            var placement = placements[i];
            var width = placement.Rotated ? shape.Height : shape.Width;
            var height = placement.Rotated ? shape.Width : shape.Height;
            result.Add(new PlacedRectangle(i, placement.X, placement.Y, width, height));
        }

        return result;
    }

    public HashSet<int> GetOverlappingRectangleIds(IReadOnlyList<PlacedRectangle> rects)
    {
        var result = new HashSet<int>();

        for (int left = 0; left < rects.Count; left++)
        {
            for (int right = left + 1; right < rects.Count; right++)
            {
                if (OverlapArea(rects[left], rects[right]) > 0)
                {
                    result.Add(rects[left].Index);
                    result.Add(rects[right].Index);
                }
            }
        }

        return result;
    }

    private static PackingBox CalculateBoundingBox(IReadOnlyList<PlacedRectangle> rects)
    {
        if (rects.Count == 0)
        {
            return new PackingBox(0, 0, 0, 0);
        }

        return new PackingBox(
            rects.Min(rect => rect.X),
            rects.Min(rect => rect.Y),
            rects.Max(rect => rect.X + rect.Width),
            rects.Max(rect => rect.Y + rect.Height));
    }

    private static int CalculateOverlapArea(IReadOnlyList<PlacedRectangle> rects)
    {
        var area = 0;

        for (int left = 0; left < rects.Count; left++)
        {
            for (int right = left + 1; right < rects.Count; right++)
            {
                area += OverlapArea(rects[left], rects[right]);
            }
        }

        return area;
    }

    private static int CalculateOutsidePenalty(IReadOnlyList<PlacedRectangle> rects)
    {
        var penalty = 0;

        foreach (var rect in rects)
        {
            if (rect.X < 0) penalty++;
            if (rect.Y < 0) penalty++;
            if (rect.X + rect.Width > BoardWidth) penalty++;
            if (rect.Y + rect.Height > BoardHeight) penalty++;
        }

        return penalty;
    }

    private static int OverlapArea(PlacedRectangle left, PlacedRectangle right)
    {
        var overlapWidth = Math.Max(0, Math.Min(left.X + left.Width, right.X + right.Width) - Math.Max(left.X, right.X));
        var overlapHeight = Math.Max(0, Math.Min(left.Y + left.Height, right.Y + right.Height) - Math.Max(left.Y, right.Y));
        return overlapWidth * overlapHeight;
    }
}

public sealed class RectanglesMutation : IMutationOperator<RectanglePlacement>
{
    public void Mutate(Chromosome<RectanglePlacement> chromosome, double mutationRate, Random random)
    {
        for (int i = 0; i < chromosome.Genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                chromosome.Genes[i] = RectanglePlacement.Random(random);
            }
        }
    }
}

public sealed record RectangleShape(int Id, int Width, int Height, Color Color);

public sealed record RectanglePlacement(int X, int Y, bool Rotated)
{
    public static RectanglePlacement Random(Random random)
    {
        return new RectanglePlacement(
            random.Next(RectanglesProblem.BoardWidth),
            random.Next(RectanglesProblem.BoardHeight),
            random.Next(2) == 1);
    }
}

public sealed record PlacedRectangle(int Index, int X, int Y, int Width, int Height);

public sealed record PackingBox(int Left, int Top, int Right, int Bottom);

public sealed record PackingEvaluation(PackingBox BoundingBox, int BoundingBoxArea, int OverlapArea, int OutsidePenalty);
