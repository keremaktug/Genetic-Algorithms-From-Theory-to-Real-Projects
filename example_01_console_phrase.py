from __future__ import annotations
import random
from dataclasses import dataclass, field
from enum import Enum
from typing import Callable, List

LETTERS = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ 1234567890,."
PHRASE = "Those who live in glass houses should not throw stones"

class CrossoverType(Enum):
    ONE_POINT = "one_point"
    UNIFORM = "uniform"

class MutationType(Enum):
    RANDOM_RESET = "random_reset"
    SWAP = "swap"

@dataclass
class Chromosome:
    data: List[str]
    fitness: float = float("inf")

    def decode(self) -> str:
        return "".join(self.data)

    def copy(self) -> "Chromosome":
        return Chromosome(self.data.copy(), self.fitness)

@dataclass
class Population:
    chromosomes: List[Chromosome] = field(default_factory=list)

    def sort(self) -> None:
        self.chromosomes.sort(key=lambda c: c.fitness)

    def get_fittest(self) -> Chromosome:
        return self.chromosomes[0]

    def average_fitness(self) -> float:
        return sum(c.fitness for c in self.chromosomes) / len(self.chromosomes)

def one_point_crossover(a: List[str], b: List[str], point: int) -> List[str]:
    if len(a) != len(b):
        raise ValueError("Chromosome lengths are not equal")
    if not (0 <= point <= len(a)):
        raise ValueError("Invalid crossover point")
    return a[:point] + b[point:]

def uniform_crossover(a: List[str], b: List[str]) -> List[str]:
    if len(a) != len(b):
        raise ValueError("Chromosome lengths are not equal")
    return [a[i] if random.random() < 0.5 else b[i] for i in range(len(a))]

def swap_mutation(genes: List[str]) -> List[str]:
    result = genes.copy()
    i, j = random.sample(range(len(result)), 2)
    result[i], result[j] = result[j], result[i]
    return result

def random_reset_mutation(genes: List[str], letters: str) -> List[str]:
    result = genes.copy()
    i = random.randrange(len(result))
    result[i] = random.choice(letters)
    return result

class GeneticSolver:
    def __init__(
        self,
        population_size: int = 1024,
        iteration_count: int = 1000,
        elitism_ratio: float = 0.10,
        mutation_ratio: float = 0.10,
        crossover_type: CrossoverType = CrossoverType.UNIFORM,
        mutation_type: MutationType = MutationType.RANDOM_RESET,
    ) -> None:
        self.population_size = population_size
        self.iteration_count = iteration_count
        self.elitism_ratio = elitism_ratio
        self.mutation_ratio = mutation_ratio
        self.crossover_type = crossover_type
        self.mutation_type = mutation_type

        self.population = Population()
        self.generator_function: Callable[[], Chromosome] | None = None
        self.fitness_function: Callable[[Chromosome], float] | None = None

    def init_population(self) -> None:
        if self.generator_function is None:
            raise ValueError("generator_function is not set")

        self.population.chromosomes = [
            self.generator_function() for _ in range(self.population_size)
        ]

    def calculate_fitness(self) -> None:
        if self.fitness_function is None:
            raise ValueError("fitness_function is not set")

        for chromosome in self.population.chromosomes:
            chromosome.fitness = self.fitness_function(chromosome)

    def crossover(self, parent1: Chromosome, parent2: Chromosome) -> Chromosome:
        if self.crossover_type == CrossoverType.ONE_POINT:
            point = random.randint(1, len(parent1.data) - 1)
            child_data = one_point_crossover(parent1.data, parent2.data, point)
        else:
            child_data = uniform_crossover(parent1.data, parent2.data)

        return Chromosome(child_data)

    def mutate(self, chromosome: Chromosome) -> Chromosome:
        if self.mutation_type == MutationType.SWAP:
            return Chromosome(swap_mutation(chromosome.data))
        return Chromosome(random_reset_mutation(chromosome.data, LETTERS))

    def evolve(self) -> Chromosome | None:
        self.init_population()
        self.calculate_fitness()
        self.population.sort()

        elite_count = max(1, int(self.population_size * self.elitism_ratio))

        for iteration in range(self.iteration_count):
            best = self.population.get_fittest()
            avg = self.population.average_fitness()
            print(
                f"iter={iteration:04d} "
                f"best_fitness={best.fitness:8.2f} "
                f"avg_fitness={avg:8.2f} "
                f"text='{best.decode()}'"
            )

            if best.fitness == 0:
                print("\nSolution found!")
                return best

            next_generation: List[Chromosome] = [
                c.copy() for c in self.population.chromosomes[:elite_count]
            ]

            parent_pool = self.population.chromosomes[: max(2, self.population_size // 2)]

            while len(next_generation) < self.population_size:
                p1, p2 = random.sample(parent_pool, 2)
                child = self.crossover(p1, p2)

                if random.random() < self.mutation_ratio:
                    child = self.mutate(child)

                next_generation.append(child)

            self.population.chromosomes = next_generation
            self.calculate_fitness()
            self.population.sort()

        return None

def phrase_generator() -> Chromosome:
    return Chromosome([random.choice(LETTERS) for _ in range(len(PHRASE))])

def calculate_fitness(chromosome: Chromosome) -> float:
    # Hedefe ne kadar yakınsa fitness o kadar düşük
    return sum(abs(ord(PHRASE[i]) - ord(gene)) for i, gene in enumerate(chromosome.data))

def main() -> None:

    random.seed()

    solver = GeneticSolver(
        population_size=1024 * 8,
        iteration_count=1000,
        elitism_ratio=0.10,
        mutation_ratio=0.10,
        crossover_type=CrossoverType.UNIFORM,
        mutation_type=MutationType.RANDOM_RESET,
    )

    solver.generator_function = phrase_generator
    solver.fitness_function = calculate_fitness

    solution = solver.evolve()

    if solution is not None:
        print(solution.decode())
    else:
        print("\nNo exact solution found.")

if __name__ == "__main__":
    main()