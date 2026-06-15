using GACore;

namespace _16.GraphColoring.Wpf;

public sealed class GraphColoringProblem : IGeneticProblem<int>
{
    private readonly int _colorCount;

    private GraphColoringProblem(IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges, int colorCount)
    {
        Nodes = nodes;
        Edges = edges;
        _colorCount = colorCount;
    }

    public IReadOnlyList<GraphNode> Nodes { get; }

    public IReadOnlyList<GraphEdge> Edges { get; }

    public Chromosome<int> CreateChromosome(Random random)
    {
        var genes = new int[Nodes.Count];

        for (int i = 0; i < genes.Length; i++)
        {
            genes[i] = random.Next(_colorCount);
        }

        return new Chromosome<int>(genes);
    }

    public double CalculateFitness(Chromosome<int> chromosome)
    {
        return GetConflictingEdges(chromosome.Genes).Count;
    }

    public IReadOnlyList<GraphEdge> GetConflictingEdges(IReadOnlyList<int> colors)
    {
        return Edges
            .Where(edge => colors.Count > edge.From && colors.Count > edge.To && colors[edge.From] == colors[edge.To])
            .ToArray();
    }

    public static GraphColoringProblem CreateMapGraph(int colorCount)
    {
        var nodes = new[]
        {
            new GraphNode("A", 0.20, 0.22),
            new GraphNode("B", 0.46, 0.16),
            new GraphNode("C", 0.72, 0.25),
            new GraphNode("D", 0.28, 0.48),
            new GraphNode("E", 0.55, 0.45),
            new GraphNode("F", 0.82, 0.52),
            new GraphNode("G", 0.18, 0.76),
            new GraphNode("H", 0.44, 0.72),
            new GraphNode("I", 0.68, 0.78),
            new GraphNode("J", 0.88, 0.82)
        };

        var edges = CreateEdges(
            (0, 1), (0, 3), (1, 2), (1, 3), (1, 4), (2, 4), (2, 5),
            (3, 4), (3, 6), (3, 7), (4, 5), (4, 7), (4, 8),
            (5, 8), (5, 9), (6, 7), (7, 8), (8, 9));

        return new GraphColoringProblem(nodes, edges, colorCount);
    }

    public static GraphColoringProblem CreateDenseGraph(int colorCount)
    {
        var nodes = Enumerable.Range(0, 18)
            .Select(index =>
            {
                var angle = Math.PI * 2 * index / 18;
                return new GraphNode(((char)('A' + index)).ToString(), 0.50 + Math.Cos(angle) * 0.38, 0.50 + Math.Sin(angle) * 0.38);
            })
            .ToArray();

        var edges = new HashSet<GraphEdge>();

        for (int i = 0; i < nodes.Length; i++)
        {
            edges.Add(new GraphEdge(i, (i + 1) % nodes.Length).Normalize());
            edges.Add(new GraphEdge(i, (i + 2) % nodes.Length).Normalize());
            edges.Add(new GraphEdge(i, (i + 4) % nodes.Length).Normalize());

            if (i % 2 == 0)
            {
                edges.Add(new GraphEdge(i, (i + 7) % nodes.Length).Normalize());
            }
        }

        return new GraphColoringProblem(nodes, edges.ToArray(), colorCount);
    }

    private static GraphEdge[] CreateEdges(params (int From, int To)[] edges)
    {
        return edges.Select(edge => new GraphEdge(edge.From, edge.To).Normalize()).ToArray();
    }
}

public sealed record GraphNode(string Name, double X, double Y);

public sealed record GraphEdge(int From, int To)
{
    public GraphEdge Normalize()
    {
        return From <= To ? this : new GraphEdge(To, From);
    }
}
