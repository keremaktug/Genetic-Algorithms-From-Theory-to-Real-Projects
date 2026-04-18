from __future__ import annotations
import random
from dataclasses import dataclass, field
from enum import Enum
from typing import Callable, Generic, List, Optional, Sequence, TypeVar


GeneT = TypeVar("GeneT")


class CrossoverType(Enum):
    ONE_POINT = "one_point"
    UNIFORM = "uniform"
    PMX = "pmx"


class MutationType(Enum):
    RANDOM_RESET = "random_reset"
    SWAP = "swap"
    SCRAMBLE = "scramble"
    INVERSE = "inverse"


@dataclass
class Chromosome(Generic[GeneT]):
    data: List[GeneT]
    fitness: float = float("inf")

    def copy(self) -> "Chromosome[GeneT]":
        return Chromosome(self.data.copy(), self.fitness)

    def __str__(self) -> str:
        return f"Chromosome(fitness={self.fitness}, data={self.data})"


@dataclass
class Population(Generic[GeneT]):
    chromosomes: List[Chromosome[GeneT]] = field(default_factory=list)

    def sort(self, reverse: bool = False) -> None:
        self.chromosomes.sort(key=lambda c: c.fitness, reverse=reverse)

    def get_fittest(self) -> Chromosome[GeneT]:
        if not self.chromosomes:
            raise ValueError("Population is empty")
        return self.chromosomes[0]

    def average_fitness(self) -> float:
        if not self.chromosomes:
            return 0.0
        return sum(c.fitness for c in self.chromosomes) / len(self.chromosomes)


def one_point_crossover(
    a: Sequence[GeneT],
    b: Sequence[GeneT],
    point: int,
) -> tuple[List[GeneT], List[GeneT]]:
    if len(a) != len(b):
        raise ValueError("Chromosome lengths are not equal")
    if point < 0 or point > len(a):
        raise ValueError("Invalid crossover point")

    child1 = list(a[:point]) + list(b[point:])
    child2 = list(b[:point]) + list(a[point:])
    return child1, child2


def uniform_crossover(
    a: Sequence[GeneT],
    b: Sequence[GeneT],
) -> tuple[List[GeneT], List[GeneT]]:
    if len(a) != len(b):
        raise ValueError("Chromosome lengths are not equal")

    child1: List[GeneT] = []
    child2: List[GeneT] = []

    for i in range(len(a)):
        if random.randint(0, 1) == 0:
            child1.append(a[i])
            child2.append(b[i])
        else:
            child1.append(b[i])
            child2.append(a[i])

    return child1, child2


def pmx_crossover(a: Sequence[GeneT], b: Sequence[GeneT]) -> List[GeneT]:
    if len(a) != len(b):
        raise ValueError("Chromosome lengths are not equal")

    n = len(a)
    i1, i2 = sorted(random.sample(range(n), 2))
    child: List[Optional[GeneT]] = [None] * n

    for i in range(i1, i2):
        child[i] = a[i]

    other_part: List[GeneT] = []
    for i in range(i1, i2):
        if b[i] not in child:
            other_part.append(b[i])

    other_index = 0
    for i in range(n):
        if child[i] is None and other_index < len(other_part):
            child[i] = other_part[other_index]
            other_index += 1

    remaining = [gene for gene in a if gene not in child]
    rem_index = 0
    for i in range(n):
        if child[i] is None:
            child[i] = remaining[rem_index]
            rem_index += 1

    return [x for x in child if x is not None]


def swap_mutation(genes: Sequence[GeneT]) -> List[GeneT]:
    result = list(genes)
    i, j = sorted(random.sample(range(len(result)), 2))
    result[i], result[j] = result[j], result[i]
    return result


def scramble_mutation(genes: Sequence[GeneT]) -> List[GeneT]:
    result = list(genes)
    i, j = sorted(random.sample(range(len(result)), 2))
    middle = result[i:j]
    random.shuffle(middle)
    result[i:j] = middle
    return result


def inversion_mutation(genes: Sequence[GeneT]) -> List[GeneT]:
    result = list(genes)
    i, j = sorted(random.sample(range(len(result)), 2))
    result[i:j] = list(reversed(result[i:j]))
    return result


def random_reset_mutation(
    genes: Sequence[GeneT],
    gene_factory: Callable[[], GeneT],
) -> List[GeneT]:
    result = list(genes)
    i = random.randrange(len(result))
    result[i] = gene_factory()
    return result


class GeneticSolver(Generic[GeneT]):

    def __init__(
        self,
        population_size: int = 1024,
        iteration_count: int = 1000,
        elitism_ratio: float = 0.1,
        mutation_ratio: float = 0.01,
        crossover_type: CrossoverType = CrossoverType.ONE_POINT,
        mutation_type: MutationType = MutationType.RANDOM_RESET,
        maximize_fitness: bool = False,
    ) -> None:
        self.population_size = population_size
        self.iteration_count = iteration_count
        self.elitism_ratio = elitism_ratio
        self.mutation_ratio = mutation_ratio
        self.crossover_type = crossover_type
        self.mutation_type = mutation_type
        self.maximize_fitness = maximize_fitness

        self.population = Population[GeneT]()

        self.generator_function: Optional[Callable[[], Chromosome[GeneT]]] = None
        self.fitness_function: Optional[Callable[[Chromosome[GeneT]], float]] = None
        self.random_gene_function: Optional[Callable[[], GeneT]] = None
        self.stop_condition_function: Optional[Callable[[Chromosome[GeneT]], bool]] = None

        self.iteration_completed_callback: Optional[
            Callable[[int, float, Chromosome[GeneT]], None]
        ] = None
        self.solution_found_callback: Optional[
            Callable[[int, Chromosome[GeneT]], None]
        ] = None

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

    def sort_population(self) -> None:
        self.population.sort(reverse=self.maximize_fitness)

    def crossover(
        self,
        parent1: Chromosome[GeneT],
        parent2: Chromosome[GeneT],
    ) -> Chromosome[GeneT]:
        if self.crossover_type == CrossoverType.ONE_POINT:
            point = random.randint(1, len(parent1.data) - 1)
            child_data = one_point_crossover(parent1.data, parent2.data, point)[0]
        elif self.crossover_type == CrossoverType.UNIFORM:
            child_data = uniform_crossover(parent1.data, parent2.data)[0]
        elif self.crossover_type == CrossoverType.PMX:
            child_data = pmx_crossover(parent1.data, parent2.data)
        else:
            raise ValueError(f"Unsupported crossover type: {self.crossover_type}")

        return Chromosome(child_data)

    def mutate(self, chromosome: Chromosome[GeneT]) -> Chromosome[GeneT]:
        if self.mutation_type == MutationType.RANDOM_RESET:
            if self.random_gene_function is None:
                raise ValueError("random_gene_function must be set for RANDOM_RESET mutation")
            return Chromosome(
                random_reset_mutation(chromosome.data, self.random_gene_function)
            )

        if self.mutation_type == MutationType.SWAP:
            return Chromosome(swap_mutation(chromosome.data))

        if self.mutation_type == MutationType.SCRAMBLE:
            return Chromosome(scramble_mutation(chromosome.data))

        if self.mutation_type == MutationType.INVERSE:
            return Chromosome(inversion_mutation(chromosome.data))

        raise ValueError(f"Unsupported mutation type: {self.mutation_type}")

    def elitism(self) -> None:
        new_generation: List[Chromosome[GeneT]] = []

        elite_count = max(1, int(self.population_size * self.elitism_ratio))
        new_generation.extend(c.copy() for c in self.population.chromosomes[:elite_count])

        parent_pool = self.population.chromosomes[: max(2, len(self.population.chromosomes) // 2)]

        while len(new_generation) < self.population_size:
            parent1, parent2 = random.sample(parent_pool, 2)
            child = self.crossover(parent1, parent2)
            new_generation.append(child)

        self.population.chromosomes = new_generation

    def should_stop(self, best: Chromosome[GeneT]) -> bool:
        if self.stop_condition_function is None:
            return False
        return self.stop_condition_function(best)

    def evolve(self) -> Optional[Chromosome[GeneT]]:
        self.init_population()
        self.calculate_fitness()
        self.sort_population()

        for iteration in range(self.iteration_count):
            best = self.population.get_fittest()
            avg = self.population.average_fitness()

            if self.iteration_completed_callback is not None:
                self.iteration_completed_callback(iteration, avg, best)

            if self.should_stop(best):
                if self.solution_found_callback is not None:
                    self.solution_found_callback(iteration, best)
                return best

            self.elitism()

            for i in range(len(self.population.chromosomes)):
                if random.random() < self.mutation_ratio:
                    self.population.chromosomes[i] = self.mutate(
                        self.population.chromosomes[i]
                    )

            self.calculate_fitness()
            self.sort_population()

        return None