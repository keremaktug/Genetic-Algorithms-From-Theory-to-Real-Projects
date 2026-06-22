# Traveling Salesman Problem with Genetic Algorithms

This WPF project demonstrates how a Genetic Algorithm can search for a short route through a set of cities.

## Problem

The Traveling Salesman Problem asks for a route that visits every city once and returns a short total distance. It is a classic combinatorial optimization problem because the number of possible routes grows very quickly as the number of cities increases.

## Chromosome Representation

- Gene = city index
- Chromosome = complete visit order
- Chromosome type = permutation

Example:

```text
12 -> 4 -> 7 -> 1 -> 9 -> ...
```

## Fitness Function

The fitness value is the total route distance.

```text
fitness = total distance of the route
```

Lower fitness is better.

## What to Observe

- Random routes gradually become shorter
- Crossings disappear over generations
- The chromosome pool shows how route order evolves across the population
- Circular and random city layouts behave differently

## Run

```bash
dotnet run --project 08.TSP.Wpf/07.TSP.Wpf.csproj
```

## Links

- Book: https://leanpub.com/geneticalgorithmsfromtheorytorealprojects
- Demo videos: https://www.youtube.com/@keremaktug9822/playlists
- Repository: https://github.com/keremaktug/Genetic-Algorithms-From-Theory-to-Real-Projects
