# Sudoku Solver with Genetic Algorithms

This WPF project demonstrates how a Genetic Algorithm can solve a Sudoku puzzle by evolving only the empty cells.

## Problem

Sudoku is a constraint satisfaction problem. The final grid must satisfy three rules:

- each row contains digits 1-9
- each column contains digits 1-9
- each 3x3 box contains digits 1-9

## Chromosome Representation

- Fixed puzzle values never move
- Genes represent only the empty cells
- Chromosome = one candidate Sudoku solution

This keeps the original puzzle clues intact and reduces the search space.

## Fitness Function

The fitness value counts Sudoku conflicts:

```text
fitness = row conflicts + column conflicts + box conflicts
```

The target fitness is `0`.

## What to Observe

- Fixed cells stay locked
- Candidate values evolve over time
- The chromosome pool shows population diversity
- The fitness chart shows conflict reduction

## Run

```bash
dotnet run --project 09.Sudoku.Wpf/08.Sudoku.Wpf.csproj
```

## Links

- Book: https://leanpub.com/geneticalgorithmsfromtheorytorealprojects
- Demo videos: https://www.youtube.com/@keremaktug9822/playlists
- Repository: https://github.com/keremaktug/Genetic-Algorithms-From-Theory-to-Real-Projects
