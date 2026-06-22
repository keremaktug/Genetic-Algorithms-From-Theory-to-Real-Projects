# Rectangle Packing with Genetic Algorithms

This WPF project demonstrates how a Genetic Algorithm can search for compact rectangle layouts.

## Problem

Rectangle packing tries to place multiple rectangles inside a limited area while avoiding overlaps and wasted space. This is useful as a simplified model for layout optimization, cutting stock problems, packing, and spatial planning.

## Chromosome Representation

Each gene stores the placement information for one rectangle:

- x position
- y position
- rotation

The chromosome represents a full layout.

## Fitness Function

The fitness function penalizes:

- overlapping rectangles
- rectangles outside the allowed area
- large bounding box area
- inefficient space usage

Lower fitness is better.

## What to Observe

- Early layouts contain overlaps and wasted space
- Better layouts survive through selection and elitism
- The packing surface becomes more compact over generations
- The chromosome pool visualizes placement values across the population

## Run

```bash
dotnet run --project 11.Rectangles.Wpf/10.Rectangles.Wpf.csproj
```

## Links

- Book: https://leanpub.com/geneticalgorithmsfromtheorytorealprojects
- Demo videos: https://www.youtube.com/@keremaktug9822/playlists
- Repository: https://github.com/keremaktug/Genetic-Algorithms-From-Theory-to-Real-Projects
