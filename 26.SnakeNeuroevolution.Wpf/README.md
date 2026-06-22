# Snake Agent with Neuroevolution

This WPF project demonstrates how a Genetic Algorithm can evolve neural-network weights for a Snake game agent.

## Problem

The agent must control the snake, avoid collisions, survive longer, and eat food. Instead of writing the policy manually, the project evolves a neural-network policy.

## Chromosome Representation

- Gene = neural-network weight
- Chromosome = complete policy network
- Inputs = danger, food direction, body direction
- Outputs = turn left, go straight, turn right

## Fitness Function

The fitness function rewards:

- food eaten
- survival time
- movement toward food

Crashing too early produces a weak fitness score.

## What to Observe

- Early agents behave randomly
- Better policies survive longer
- The neural network visualization shows the evolved policy structure
- Fitness improves as useful behavior emerges

## Run

```bash
dotnet run --project 26.SnakeNeuroevolution.Wpf/26.SnakeNeuroevolution.Wpf.csproj
```

## Links

- Book: https://leanpub.com/geneticalgorithmsfromtheorytorealprojects
- Demo videos: https://www.youtube.com/@keremaktug9822/playlists
- Repository: https://github.com/keremaktug/Genetic-Algorithms-From-Theory-to-Real-Projects
