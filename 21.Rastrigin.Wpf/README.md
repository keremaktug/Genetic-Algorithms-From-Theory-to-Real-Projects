# Rastrigin Function Minimization with Genetic Algorithms

This WPF project demonstrates how a Genetic Algorithm can minimize a continuous mathematical function.

## Problem

The Rastrigin function is often used to test optimization algorithms. It has many local minima, which makes it harder than a simple smooth bowl-shaped function.

The global minimum is:

```text
f(0, 0) = 0
```

## Chromosome Representation

- Gene 0 = x
- Gene 1 = y
- Domain = `[-5.12, 5.12]`
- Chromosome = one point in the search space

## Fitness Function

The fitness value is the Rastrigin function value:

```text
fitness = f(x, y)
```

Lower fitness is better.

## What to Observe

- The search space contains many peaks and valleys
- The population gradually moves toward the global minimum
- The 3D chart shows the optimization landscape
- Mutation helps escape local minima

## Run

```bash
dotnet run --project 21.Rastrigin.Wpf/16.Rastrigin.Wpf.csproj
```

## Links

- Book: https://leanpub.com/geneticalgorithmsfromtheorytorealprojects
- Demo videos: https://www.youtube.com/@keremaktug9822/playlists
- Repository: https://github.com/keremaktug/Genetic-Algorithms-From-Theory-to-Real-Projects
