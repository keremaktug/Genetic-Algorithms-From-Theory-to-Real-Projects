using GACore;

namespace _10.Timetabling.Wpf;

public sealed class TimetablingProblem : IGeneticProblem<CourseAssignment>
{
    public static readonly string[] Days = ["Mon", "Tue", "Wed", "Thu", "Fri"];
    public const int SlotsPerDay = 6;
    public const int TimeSlotCount = 30;

    public static readonly Room[] Rooms =
    [
        new("R101", 30, false),
        new("R202", 45, false),
        new("Lab1", 24, true),
        new("Auditorium", 80, false)
    ];

    public static readonly Course[] Courses =
    [
        new("MATH101", "Mathematics", "Ada", 28, false),
        new("PHY101", "Physics", "Grace", 34, false),
        new("CS101", "Programming", "Linus", 24, true),
        new("CS102", "Data Structures", "Linus", 24, true),
        new("ENG101", "English", "Mary", 32, false),
        new("HIS101", "History", "Mary", 35, false),
        new("BIO101", "Biology", "Grace", 30, false),
        new("CHEM101", "Chemistry", "Ada", 26, true),
        new("ART101", "Design", "Alan", 20, false),
        new("STAT101", "Statistics", "Ada", 38, false),
        new("AI101", "Intro AI", "Alan", 24, true),
        new("ECON101", "Economics", "Grace", 40, false)
    ];

    public Chromosome<CourseAssignment> CreateChromosome(Random random)
    {
        var genes = new CourseAssignment[Courses.Length];

        for (int i = 0; i < genes.Length; i++)
        {
            genes[i] = CourseAssignment.Random(random);
        }

        return new Chromosome<CourseAssignment>(genes);
    }

    public double CalculateFitness(Chromosome<CourseAssignment> chromosome)
    {
        var result = Evaluate(chromosome.Genes);
        return result.HardPenalty * 100 + result.SoftPenalty;
    }

    public EvaluationResult Evaluate(IReadOnlyList<CourseAssignment> assignments)
    {
        var hardPenalty = 0;
        var softPenalty = 0;
        var violations = new List<string>();

        for (int i = 0; i < assignments.Count; i++)
        {
            var course = Courses[i];
            var room = Rooms[assignments[i].RoomIndex];

            if (course.StudentCount > room.Capacity)
            {
                hardPenalty++;
                violations.Add($"{course.Code}: room {room.Name} capacity is too small");
            }

            if (course.RequiresLab && !room.HasLab)
            {
                hardPenalty++;
                violations.Add($"{course.Code}: requires a lab room");
            }

            if (assignments[i].TimeSlot % SlotsPerDay == SlotsPerDay - 1)
            {
                softPenalty++;
                violations.Add($"{course.Code}: scheduled in the last slot of the day");
            }
        }

        for (int left = 0; left < assignments.Count; left++)
        {
            for (int right = left + 1; right < assignments.Count; right++)
            {
                if (assignments[left].TimeSlot != assignments[right].TimeSlot) continue;

                if (assignments[left].RoomIndex == assignments[right].RoomIndex)
                {
                    hardPenalty++;
                    violations.Add($"{Courses[left].Code} and {Courses[right].Code}: same room and time");
                }

                if (Courses[left].Teacher == Courses[right].Teacher)
                {
                    hardPenalty++;
                    violations.Add($"{Courses[left].Code} and {Courses[right].Code}: same teacher and time");
                }
            }
        }

        return new EvaluationResult(hardPenalty, softPenalty, violations);
    }
}

public sealed class TimetablingMutation : IMutationOperator<CourseAssignment>
{
    public void Mutate(Chromosome<CourseAssignment> chromosome, double mutationRate, Random random)
    {
        for (int i = 0; i < chromosome.Genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                chromosome.Genes[i] = CourseAssignment.Random(random);
            }
        }
    }
}

public sealed record Course(string Code, string Name, string Teacher, int StudentCount, bool RequiresLab);

public sealed record Room(string Name, int Capacity, bool HasLab);

public sealed record CourseAssignment(int TimeSlot, int RoomIndex)
{
    public static CourseAssignment Random(Random random)
    {
        return new CourseAssignment(random.Next(TimetablingProblem.TimeSlotCount), random.Next(TimetablingProblem.Rooms.Length));
    }
}

public sealed record EvaluationResult(int HardPenalty, int SoftPenalty, IReadOnlyList<string> Violations);
