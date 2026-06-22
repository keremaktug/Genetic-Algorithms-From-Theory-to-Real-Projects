# Maze Solver with Genetic Algorithms

This WPF project demonstrates how a Genetic Algorithm can evolve movement commands to reach the exit of a maze.

## Problem

The agent starts at `S` and must reach `E` by executing a sequence of movement commands. Walls block movement, and collisions are penalized.

## Chromosome Representation

- Gene = movement command
- `0` = up
- `1` = right
- `2` = down
- `3` = left
- Chromosome = full movement sequence

Example:

```text
R R D D L U R ...
```

## Fitness Function

The fitness function penalizes:

- distance to the exit
- wall collisions
- unnecessary moves

This project also uses a distance map from the exit so the fitness function gives better guidance in a complex maze.

## What to Observe

- Random movement sequences gradually become meaningful paths
- Collisions decrease over time
- The best path gets longer and closer to the exit
- Guided initialization helps the algorithm avoid being stuck at generation 0

## Run

```bash
dotnet run --project 20.MazeSolver.Wpf/13.MazeSolver.Wpf.csproj
```

## Links

- Book: https://leanpub.com/geneticalgorithmsfromtheorytorealprojects
- Demo videos: https://www.youtube.com/@keremaktug9822/playlists
- Repository: https://github.com/keremaktug/Genetic-Algorithms-From-Theory-to-Real-Projects
