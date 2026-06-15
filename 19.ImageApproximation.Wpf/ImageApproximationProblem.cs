using GACore;

namespace _19.ImageApproximation.Wpf;

public sealed class ImageApproximationProblem : IGeneticProblem<ApproxCircle>
{
    public const int Width = 64;
    public const int Height = 40;

    private readonly int _circleCount;
    private readonly Pixel[] _targetPixels;

    public ImageApproximationProblem(int circleCount)
    {
        _circleCount = circleCount;
        _targetPixels = CreateTargetPixels();
    }

    public IReadOnlyList<Pixel> TargetPixels => _targetPixels;

    public Chromosome<ApproxCircle> CreateChromosome(Random random)
    {
        var circles = new ApproxCircle[_circleCount];

        for (int i = 0; i < circles.Length; i++)
        {
            circles[i] = ApproxCircle.Random(random);
        }

        return new Chromosome<ApproxCircle>(circles);
    }

    public double CalculateFitness(Chromosome<ApproxCircle> chromosome)
    {
        var rendered = Render(chromosome.Genes);
        var error = 0.0;

        for (int i = 0; i < rendered.Length; i++)
        {
            var dr = rendered[i].R - _targetPixels[i].R;
            var dg = rendered[i].G - _targetPixels[i].G;
            var db = rendered[i].B - _targetPixels[i].B;
            error += dr * dr + dg * dg + db * db;
        }

        return error / rendered.Length;
    }

    public static Pixel[] Render(IReadOnlyList<ApproxCircle> circles)
    {
        var pixels = Enumerable.Repeat(new Pixel(250, 250, 248), Width * Height).ToArray();

        foreach (var circle in circles)
        {
            var radiusSquared = circle.Radius * circle.Radius;
            var left = Math.Max(0, (int)Math.Floor(circle.X - circle.Radius));
            var right = Math.Min(Width - 1, (int)Math.Ceiling(circle.X + circle.Radius));
            var top = Math.Max(0, (int)Math.Floor(circle.Y - circle.Radius));
            var bottom = Math.Min(Height - 1, (int)Math.Ceiling(circle.Y + circle.Radius));

            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    var dx = x - circle.X;
                    var dy = y - circle.Y;

                    if (dx * dx + dy * dy > radiusSquared)
                    {
                        continue;
                    }

                    var index = y * Width + x;
                    pixels[index] = Blend(pixels[index], new Pixel(circle.R, circle.G, circle.B), circle.Alpha);
                }
            }
        }

        return pixels;
    }

    public static Pixel[] CreateTargetPixels()
    {
        var pixels = new Pixel[Width * Height];

        Array.Fill(pixels, new Pixel(250, 250, 248));

        DrawCircle(pixels, 18, 17, 10, new Pixel(37, 99, 235));
        DrawCircle(pixels, 42, 17, 10, new Pixel(245, 158, 11));
        DrawCircle(pixels, 31, 27, 9, new Pixel(236, 72, 153));
        DrawCircle(pixels, 16, 30, 7, new Pixel(34, 197, 94));
        DrawCircle(pixels, 47, 30, 7, new Pixel(20, 184, 166));

        return pixels;
    }

    private static void DrawCircle(Pixel[] pixels, double cx, double cy, double radius, Pixel color)
    {
        var radiusSquared = radius * radius;
        var left = Math.Max(0, (int)Math.Floor(cx - radius));
        var right = Math.Min(Width - 1, (int)Math.Ceiling(cx + radius));
        var top = Math.Max(0, (int)Math.Floor(cy - radius));
        var bottom = Math.Min(Height - 1, (int)Math.Ceiling(cy + radius));

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                var dx = x - cx;
                var dy = y - cy;

                if (dx * dx + dy * dy <= radiusSquared)
                {
                    pixels[y * Width + x] = color;
                }
            }
        }
    }

    private static Pixel Blend(Pixel background, Pixel foreground, double alpha)
    {
        var inverse = 1 - alpha;
        return new Pixel(
            (byte)Math.Round(background.R * inverse + foreground.R * alpha),
            (byte)Math.Round(background.G * inverse + foreground.G * alpha),
            (byte)Math.Round(background.B * inverse + foreground.B * alpha));
    }

}

public sealed class ApproxCircleMutation : IMutationOperator<ApproxCircle>
{
    public void Mutate(Chromosome<ApproxCircle> chromosome, double mutationRate, Random random)
    {
        for (int i = 0; i < chromosome.Genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                chromosome.Genes[i] = chromosome.Genes[i].Mutate(random);
            }
        }
    }
}

public sealed record ApproxCircle(double X, double Y, double Radius, byte R, byte G, byte B, double Alpha)
{
    public static ApproxCircle Random(Random random)
    {
        return new ApproxCircle(
            random.NextDouble() * ImageApproximationProblem.Width,
            random.NextDouble() * ImageApproximationProblem.Height,
            2 + random.NextDouble() * 14,
            (byte)random.Next(30, 256),
            (byte)random.Next(30, 256),
            (byte)random.Next(30, 256),
            0.18 + random.NextDouble() * 0.58);
    }

    public ApproxCircle Mutate(Random random)
    {
        return random.Next(7) switch
        {
            0 => this with { X = Clamp(X + NextDelta(random, 8), 0, ImageApproximationProblem.Width - 1) },
            1 => this with { Y = Clamp(Y + NextDelta(random, 6), 0, ImageApproximationProblem.Height - 1) },
            2 => this with { Radius = Clamp(Radius + NextDelta(random, 4), 1, 18) },
            3 => this with { R = MutateByte(R, random) },
            4 => this with { G = MutateByte(G, random) },
            5 => this with { B = MutateByte(B, random) },
            _ => this with { Alpha = Clamp(Alpha + NextDelta(random, 0.18), 0.08, 0.82) }
        };
    }

    private static byte MutateByte(byte value, Random random)
    {
        return (byte)Math.Clamp(value + random.Next(-42, 43), 0, 255);
    }

    private static double NextDelta(Random random, double amount)
    {
        return (random.NextDouble() * 2 - 1) * amount;
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Min(max, Math.Max(min, value));
    }
}

public sealed record Pixel(byte R, byte G, byte B);
