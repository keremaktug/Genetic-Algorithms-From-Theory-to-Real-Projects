# Genetic Algorithms From Theory to Real Projects

**One reusable Genetic Algorithm engine. 20+ real optimization projects. Complete C#/.NET source code.**

This repository contains the companion source code for the book **Genetic Algorithms From Theory to Real Projects**.

The book teaches Genetic Algorithms by building real projects, not by stopping at theory. It starts with a from-scratch implementation, then grows into a reusable `GA.Core` library and applies the same engine to classic optimization, routing, scheduling, engineering, machine learning, neuroevolution, and game AI problems.

## Get The Book

- Leanpub: https://leanpub.com/geneticalgorithmsfromtheorytorealprojects
- Gumroad: add your Gumroad link here
- Amazon Kindle: coming soon

## What You Will Build

- A Genetic Algorithm from scratch
- A reusable `GA.Core` library
- Phrase evolution examples
- 8-Queens solver
- Knapsack optimizer
- Traveling Salesman Problem solver
- Sudoku solver
- Timetabling optimizer
- Rectangle packing optimizer
- Graph coloring solver
- Vehicle routing optimizer
- Maze solver
- Analog RC filter optimizer
- Analog op-amp gain optimizer
- Rastrigin function minimizer
- Gear train optimizer
- Image approximation with evolved shapes
- Rubik's Cube solver visualization
- ML.NET hyperparameter optimization
- Random Forest hyperparameter optimization
- Feature selection with Genetic Algorithms
- Lunar Lander policy search
- Neural Architecture Search
- Snake agent with neuroevolution

## Why This Project Exists

Most Genetic Algorithm resources explain the core concepts with small isolated examples.

This project takes a different path:

- Build the algorithm from scratch.
- Refactor it into a reusable solver.
- Apply the same ideas to many different problem types.
- Visualize how populations evolve over time.
- Keep the code readable enough to learn from and modify.

If you want to understand how genes, chromosomes, fitness functions, selection, crossover, mutation, elitism, and parameter tuning work in real projects, this repository is designed for that.

## Screenshots

### Vehicle Routing

<img width="1920" height="1041" alt="Vehicle Routing with Genetic Algorithms" src="https://github.com/user-attachments/assets/072f2826-c89e-49b7-bd83-c6a7ab2e2ed4" />

### Sudoku

<img width="1920" height="1040" alt="Sudoku solved with Genetic Algorithms" src="https://github.com/user-attachments/assets/c0f77500-0787-4bb7-b8f1-f9cf10e0e4e6" />

### Rubik's Cube

<img width="1920" height="1040" alt="Rubik's Cube solver with Genetic Algorithms" src="https://github.com/user-attachments/assets/ffdcffa4-2e3f-453b-b250-36ed7cf47111" />

### Rectangle Packing

<img width="1920" height="1040" alt="Rectangle packing with Genetic Algorithms" src="https://github.com/user-attachments/assets/925a1965-f754-4002-9a44-8c26e1e23170" />

### Phrase Evolution

<img width="1920" height="1039" alt="Phrase evolution from scratch" src="https://github.com/user-attachments/assets/82dea637-b8b3-40b2-bfe4-2b143684cd26" />

## Project Structure

```text
01.Phrase.FromScratch.Console
02.Phrase.FromScratch.Wpf
03.GA.Core
04.Phrase.WithCore.Wpf
05.Cards.Wpf
06.EightQueens.Wpf
07.Knapsack.Wpf
08.TSP.Wpf
09.Sudoku.Wpf
10.Timetabling.Wpf
11.Rectangles.Wpf
12.AnalogRC.Wpf
13.AnalogOpAmp.Wpf
14.MLHyperparameters.Console
15.RandomForestHyperparameters.Console
16.GraphColoring.Wpf
17.VehicleRouting.Wpf
18.FeatureSelection.Console
19.ImageApproximation.Wpf
20.MazeSolver.Wpf
21.Rastrigin.Wpf
22.GearTrain.Wpf
23.RubiksCube.Wpf
24.LunarLanderPolicy.Wpf
25.NeuralArchitectureSearch.Wpf
26.SnakeNeuroevolution.Wpf
```

## How To Run

Open the solution in Visual Studio:

```text
Codes.slnx
```

Then choose any project as the startup project and run it.

Recommended first projects:

1. `01.Phrase.FromScratch.Console`
2. `02.Phrase.FromScratch.Wpf`
3. `03.GA.Core`
4. `04.Phrase.WithCore.Wpf`
5. `09.Sudoku.Wpf`
6. `17.VehicleRouting.Wpf`
7. `26.SnakeNeuroevolution.Wpf`

## Requirements

- .NET 9
- Visual Studio 2022 or newer
- Windows, for the WPF examples

## Who This Is For

This repository is useful for:

- C# and .NET developers learning Genetic Algorithms
- Students studying optimization and evolutionary computation
- Engineers looking for practical optimization examples
- Developers interested in machine learning optimization
- Anyone who learns algorithms better by building visual projects

## Companion Book

The full explanation, step-by-step implementation details, and project walkthroughs are included in the book:

**Genetic Algorithms From Theory to Real Projects**

Buy the book on Leanpub:

https://leanpub.com/geneticalgorithmsfromtheorytorealprojects

## License

This repository is provided as companion source code for the book. See the repository license or book terms before using the code in commercial projects.

