from __future__ import annotations

import math
import random
from dataclasses import dataclass
from typing import Final

import numpy as np

from core.ga_solver import Chromosome


@dataclass(frozen=True)
class Customer:
    customer_id: int
    x: float
    y: float
    demand: int


VEHICLE_CAPACITY: Final[int] = 30
DEPOT_X: Final[float] = 0.0
DEPOT_Y: Final[float] = 0.0


CUSTOMERS: list[Customer] = [
    Customer(1, 20, 30, 4),
    Customer(2, 35, 25, 6),
    Customer(3, 45, 40, 7),
    Customer(4, 60, 20, 5),
    Customer(5, 70, 35, 8),
    Customer(6, 80, 15, 3),
    Customer(7, 25, -10, 5),
    Customer(8, 40, -20, 9),
    Customer(9, 55, -5, 4),
    Customer(10, 65, -25, 6),
    Customer(11, 15, 55, 7),
    Customer(12, 30, 65, 5),
    Customer(13, 50, 60, 8),
    Customer(14, 75, 55, 4),
    Customer(15, 85, 40, 6),
]

CUSTOMER_COUNT: Final[int] = len(CUSTOMERS)


def distance(ax: float, ay: float, bx: float, by: float) -> float:
    return math.hypot(ax - bx, ay - by)


def random_gene() -> int:
    return random.randrange(CUSTOMER_COUNT)


def vrp_generator() -> Chromosome[int]:
    genes = np.random.permutation(CUSTOMER_COUNT).astype(np.int32)
    return Chromosome(genes.tolist())


def decode(chromosome: Chromosome[int]) -> str:
    routes = split_routes(chromosome)
    parts: list[str] = []

    for i, route in enumerate(routes, start=1):
        ids = [str(CUSTOMERS[idx].customer_id) for idx in route]
        load = route_load(route)
        parts.append(f"V{i}[{'-'.join(ids)}] load={load}")

    total = total_distance(chromosome)
    return f"{' | '.join(parts)} | dist={total:.2f}"


def route_load(route: list[int]) -> int:
    return sum(CUSTOMERS[idx].demand for idx in route)


def split_routes(chromosome: Chromosome[int]) -> list[list[int]]:
    routes: list[list[int]] = []
    current_route: list[int] = []
    current_load = 0

    for idx in chromosome.data:
        demand = CUSTOMERS[idx].demand
        if current_route and current_load + demand > VEHICLE_CAPACITY:
            routes.append(current_route)
            current_route = [idx]
            current_load = demand
        else:
            current_route.append(idx)
            current_load += demand

    if current_route:
        routes.append(current_route)

    return routes


def route_distance(route: list[int]) -> float:
    if not route:
        return 0.0

    total = 0.0

    first = CUSTOMERS[route[0]]
    total += distance(DEPOT_X, DEPOT_Y, first.x, first.y)

    for i in range(len(route) - 1):
        a = CUSTOMERS[route[i]]
        b = CUSTOMERS[route[i + 1]]
        total += distance(a.x, a.y, b.x, b.y)

    last = CUSTOMERS[route[-1]]
    total += distance(last.x, last.y, DEPOT_X, DEPOT_Y)

    return total


def total_distance(chromosome: Chromosome[int]) -> float:
    routes = split_routes(chromosome)
    return float(sum(route_distance(route) for route in routes))


def calculate_fitness(chromosome: Chromosome[int]) -> float:
    routes = split_routes(chromosome)
    dist = sum(route_distance(route) for route in routes)

    penalty = 0.0
    for route in routes:
        load = route_load(route)
        if load > VEHICLE_CAPACITY:
            penalty += (load - VEHICLE_CAPACITY) * 1000.0

    # Small vehicle-count pressure to prefer fewer routes when distances are similar
    vehicle_penalty = max(0, len(routes) - 1) * 10.0

    return float(dist + penalty + vehicle_penalty)


def stop_condition(_best: Chromosome[int]) -> bool:
    return False


def population_to_index_matrix(population_snapshot: list[list[int]]) -> np.ndarray:
    if not population_snapshot:
        return np.empty((0, 0), dtype=np.int32)
    return np.asarray(population_snapshot, dtype=np.int32)


def generate_color_scheme(count: int) -> np.ndarray:
    import colorsys

    colors = np.zeros((count, 3), dtype=np.uint8)
    for i in range(count):
        h = i / max(1, count)
        r, g, b = colorsys.hls_to_rgb(h, 0.5, 0.75)
        colors[i] = (int(r * 255), int(g * 255), int(b * 255))
    return colors


def get_route_summary(chromosome: Chromosome[int]) -> list[str]:
    routes = split_routes(chromosome)
    summaries: list[str] = []

    for i, route in enumerate(routes, start=1):
        ids = [str(CUSTOMERS[idx].customer_id) for idx in route]
        summaries.append(
            f"Vehicle {i}: {' -> '.join(ids)} | load={route_load(route)} | dist={route_distance(route):.2f}"
        )

    return summaries