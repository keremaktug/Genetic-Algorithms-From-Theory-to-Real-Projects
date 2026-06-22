# Vehicle Routing Problem with Genetic Algorithms

This WPF project demonstrates how a Genetic Algorithm can solve a simplified Vehicle Routing Problem.

## Problem

The Vehicle Routing Problem asks how a fleet of vehicles should visit a set of customers while minimizing travel distance and respecting capacity constraints.

## Chromosome Representation

- Gene = customer
- Chromosome = customer visit order
- Routes are split according to vehicle capacity

The same chromosome can produce multiple vehicle routes.

## Fitness Function

The fitness value combines:

```text
fitness = total route distance + capacity penalties
```

Lower fitness is better.

## What to Observe

- Routes become shorter and more structured
- Customers are distributed across vehicles
- Capacity violations are penalized
- The route map and chromosome pool show different views of the same solution

## Run

```bash
dotnet run --project 17.VehicleRouting.Wpf/12.VehicleRouting.Wpf.csproj
```

## Links

- Book: https://leanpub.com/geneticalgorithmsfromtheorytorealprojects
- Demo videos: https://www.youtube.com/@keremaktug9822/playlists
- Repository: https://github.com/keremaktug/Genetic-Algorithms-From-Theory-to-Real-Projects
