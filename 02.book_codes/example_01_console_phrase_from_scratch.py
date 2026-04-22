from __future__ import annotations

import random
import time
from dataclasses import dataclass
from typing import Final


# =========================================================
# Chapter 1 Example
# Genetic Algorithm from scratch:
# Evolving a target phrase
# =========================================================

LETTERS: Final[str] = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ 1234567890,."
TARGET_PHRASE: Final[str] = "Genetic Algorithms From Theory to Real Projects"

POPULATION_SIZE: Final[int] = 400
ITERATION_COUNT: Final[int] = 600
ELITISM_RATIO: Final[float] = 0.20
MUTATION_RATIO: Final[float] = 0.06
TOURNAMENT_SIZE: Final[int] = 5

TARGET_LENGTH: Final[int] = len(TARGET_PHRASE)

@dataclass
class Chromosome:
    genes: list[str]
    fitness: float = float("inf")

    def copy(self) -> "Chromosome":
        return Chromosome(self.genes.copy(), self.fitness)

def random_gene() -> str:
    return random.choice(LETTERS)

def create_random_chromosome() -> Chromosome:
    genes = [random_gene() for _ in range(TARGET_LENGTH)]
    return Chromosome(genes=genes)

def decode(chromosome: Chromosome) -> str:
    return "".join(chromosome.genes)

def calculate_fitness(chromosome: Chromosome) -> float:
    """
    Lower is better.

    Primary objective:
        minimize number of wrong characters

    Secondary objective:
        among equally wrong candidates, prefer characters
        whose ASCII codes are closer to target characters

    Perfect solution => 0.0
    """
    mismatch_count = 0
    ascii_distance_sum = 0

    for actual_char, target_char in zip(chromosome.genes, TARGET_PHRASE):
        if actual_char != target_char:
            mismatch_count += 1
        ascii_distance_sum += abs(ord(actual_char) - ord(target_char))

    chromosome.fitness = float(mismatch_count * 1000 + ascii_distance_sum)
    return chromosome.fitness

def initialize_population(size: int) -> list[Chromosome]:
    population = [create_random_chromosome() for _ in range(size)]

    for chromosome in population:
        calculate_fitness(chromosome)

    return population

def sort_population(population: list[Chromosome]) -> None:
    population.sort(key=lambda chromosome: chromosome.fitness)

def one_point_crossover(parent1: Chromosome, parent2: Chromosome) -> Chromosome:
    point = random.randint(1, TARGET_LENGTH - 1)
    child_genes = parent1.genes[:point] + parent2.genes[point:]
    return Chromosome(genes=child_genes)

def mutate(chromosome: Chromosome, mutation_ratio: float) -> None:
    for i in range(len(chromosome.genes)):
        if random.random() < mutation_ratio:
            chromosome.genes[i] = random_gene()

def tournament_selection(population: list[Chromosome], tournament_size: int) -> Chromosome:
    contestants = random.sample(population, tournament_size)
    contestants.sort(key=lambda chromosome: chromosome.fitness)
    return contestants[0]

def make_next_generation(
    population: list[Chromosome],
    population_size: int,
    elitism_ratio: float,
    mutation_ratio: float,
) -> list[Chromosome]:
    sort_population(population)

    elite_count = max(1, int(population_size * elitism_ratio))
    elites = [chromosome.copy() for chromosome in population[:elite_count]]

    next_generation: list[Chromosome] = elites

    while len(next_generation) < population_size:
        parent1 = tournament_selection(population, TOURNAMENT_SIZE)
        parent2 = tournament_selection(population, TOURNAMENT_SIZE)

        child = one_point_crossover(parent1, parent2)
        mutate(child, mutation_ratio)
        calculate_fitness(child)

        next_generation.append(child)

    return next_generation

def print_iteration(iteration: int, population: list[Chromosome]) -> None:
    best = population[0]
    average_fitness = sum(chromosome.fitness for chromosome in population) / len(population)

    print(
        f"iter={iteration:04d} "
        f"best={best.fitness:8.2f} "
        f"avg={average_fitness:10.2f} "
        f"text='{decode(best)}'"
    )

def exact_match(best: Chromosome) -> bool:
    return decode(best) == TARGET_PHRASE

def main() -> None:
    random.seed()

    print("=" * 80)
    print("Chapter 1 - Genetic Algorithm from scratch")
    print("Problem: Evolve a target phrase")
    print("=" * 80)
    print(f"Target phrase   : {TARGET_PHRASE}")
    print(f"Population size : {POPULATION_SIZE}")
    print(f"Iterations      : {ITERATION_COUNT}")
    print(f"Elitism ratio   : {ELITISM_RATIO}")
    print(f"Mutation ratio  : {MUTATION_RATIO}")
    print(f"Tournament size : {TOURNAMENT_SIZE}")
    print()

    start_time = time.time()

    population = initialize_population(POPULATION_SIZE)
    sort_population(population)

    best_so_far = population[0].fitness
    no_improvement_count = 0
    no_improvement_limit = 120

    solution_found = False

    for iteration in range(ITERATION_COUNT):
        sort_population(population)
        best = population[0]

        if iteration % 5 == 0 or iteration == 0:
            print_iteration(iteration, population)

        if best.fitness < best_so_far:
            best_so_far = best.fitness
            no_improvement_count = 0
        else:
            no_improvement_count += 1

        if exact_match(best):
            solution_found = True
            print("\nSolution found")
            print(f"Iteration : {iteration}")
            print(f"Phrase    : {decode(best)}")
            break

        if no_improvement_count >= no_improvement_limit:
            print("\nStopped early: no improvement for a long time.")
            break

        population = make_next_generation(
            population=population,
            population_size=POPULATION_SIZE,
            elitism_ratio=ELITISM_RATIO,
            mutation_ratio=MUTATION_RATIO,
        )

    sort_population(population)
    best = population[0]

    elapsed = time.time() - start_time

    print("\nFinal result")
    print(f"Best phrase : {decode(best)}")
    print(f"Fitness     : {best.fitness:.2f}")
    print(f"Elapsed     : {elapsed:.2f} sec")

    if not solution_found:
        print("Exact solution was not found within the iteration limit.")

if __name__ == "__main__":
    main()