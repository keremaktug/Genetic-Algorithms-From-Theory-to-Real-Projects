from __future__ import annotations

import random
import time
from typing import Final

from core.ga_solver import Chromosome, GeneticSolver


# =========================================================
# Example 02 - Using GA Solver
# =========================================================

LETTERS: Final[str] = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ 1234567890,._-"
TARGET: Final[str] = "Genetic-Algorithms-From-Theory-to-Real-Projects"

POPULATION_SIZE = 400
ITERATIONS = 600
ELITISM = 0.2
MUTATION = 0.06
TOURNAMENT_SIZE = 5


# =========================================================
# Problem-specific functions
# =========================================================

def random_gene():
    return random.choice(LETTERS)


def generator():
    genes = [random_gene() for _ in range(len(TARGET))]
    return Chromosome(genes)


def fitness(chromosome: Chromosome) -> float:
    mismatch = 0
    distance = 0

    for a, b in zip(chromosome.genes, TARGET):
        if a != b:
            mismatch += 1
        distance += abs(ord(a) - ord(b))

    return mismatch * 1000 + distance


def crossover(p1: Chromosome, p2: Chromosome) -> Chromosome:
    point = random.randint(1, len(TARGET) - 1)
    genes = p1.genes[:point] + p2.genes[point:]
    return Chromosome(genes)


def mutate(chromosome: Chromosome, rate: float):
    for i in range(len(chromosome.genes)):
        if random.random() < rate:
            chromosome.genes[i] = random_gene()


def tournament_selection(population):
    candidates = random.sample(population, TOURNAMENT_SIZE)
    candidates.sort(key=lambda c: c.fitness)
    return candidates[0]


def decode(chromosome: Chromosome) -> str:
    return "".join(chromosome.genes)


# =========================================================
# Logging
# =========================================================

def on_iteration(iteration, population):
    best = population[0]
    avg = sum(c.fitness for c in population) / len(population)

    if iteration % 5 == 0:
        print(
            f"iter={iteration:04d} "
            f"best={best.fitness:8.2f} "
            f"avg={avg:10.2f} "
            f"text='{decode(best)}'"
        )


# =========================================================
# Main
# =========================================================

def main():
    random.seed()

    print("=" * 80)
    print("Chapter 2 - GA Solver abstraction")
    print("=" * 80)
    print(f"Target: {TARGET}")
    print()

    solver = GeneticSolver(
        population_size=POPULATION_SIZE,
        iteration_count=ITERATIONS,
        elitism_ratio=ELITISM,
        mutation_ratio=MUTATION,
    )

    solver.generator_function = generator
    solver.fitness_function = fitness
    solver.crossover_function = crossover
    solver.mutation_function = mutate
    solver.selection_function = tournament_selection
    solver.iteration_callback = on_iteration

    start = time.time()
    best = solver.evolve()
    elapsed = time.time() - start

    print("\nFinal result")
    print(f"Phrase  : {decode(best)}")
    print(f"Fitness : {best.fitness}")
    print(f"Time    : {elapsed:.2f}s")


if __name__ == "__main__":
    main()