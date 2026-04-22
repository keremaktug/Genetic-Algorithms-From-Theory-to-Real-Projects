from __future__ import annotations

import random
from dataclasses import dataclass
from typing import Callable, List


# =========================================================
# Core GA Solver (Chapter 2)
# =========================================================

@dataclass
class Chromosome:
    genes: list
    fitness: float = float("inf")

    def copy(self) -> "Chromosome":
        return Chromosome(self.genes.copy(), self.fitness)


class GeneticSolver:

    def __init__(
        self,
        population_size: int,
        iteration_count: int,
        elitism_ratio: float,
        mutation_ratio: float,
    ):
        self.population_size = population_size
        self.iteration_count = iteration_count
        self.elitism_ratio = elitism_ratio
        self.mutation_ratio = mutation_ratio

        # externally provided functions
        self.generator_function: Callable[[], Chromosome] = None
        self.fitness_function: Callable[[Chromosome], float] = None
        self.crossover_function: Callable[[Chromosome, Chromosome], Chromosome] = None
        self.mutation_function: Callable[[Chromosome, float], None] = None
        self.selection_function: Callable[[List[Chromosome]], Chromosome] = None

        self.iteration_callback: Callable[[int, List[Chromosome]], None] = None

    def initialize_population(self) -> List[Chromosome]:
        population = [self.generator_function() for _ in range(self.population_size)]

        for chromosome in population:
            chromosome.fitness = self.fitness_function(chromosome)

        return population

    def evolve(self) -> Chromosome:
        population = self.initialize_population()

        for iteration in range(self.iteration_count):

            population.sort(key=lambda c: c.fitness)

            if self.iteration_callback:
                self.iteration_callback(iteration, population)

            best = population[0]
            if best.fitness == 0:
                return best

            # elitism
            elite_count = max(1, int(self.population_size * self.elitism_ratio))
            next_population = [c.copy() for c in population[:elite_count]]

            while len(next_population) < self.population_size:
                parent1 = self.selection_function(population)
                parent2 = self.selection_function(population)

                child = self.crossover_function(parent1, parent2)
                self.mutation_function(child, self.mutation_ratio)

                child.fitness = self.fitness_function(child)
                next_population.append(child)

            population = next_population

        population.sort(key=lambda c: c.fitness)
        return population[0]