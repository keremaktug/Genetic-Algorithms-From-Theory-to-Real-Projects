# Genetic Algorithms From Theory to Real Projects

**One reusable Genetic Algorithm engine. 27 real optimization projects. Complete C#/.NET source code.**

This repository contains the companion source code for the book **Genetic Algorithms From Theory to Real Projects**.

The book teaches Genetic Algorithms by building real projects, not by stopping at theory. It starts with a from-scratch implementation, grows into a reusable `GA.Core` library, and then applies the same engine to classic optimization, routing, scheduling, engineering, machine learning, neuroevolution, and game AI problems.

## Get the Book

- Leanpub: https://leanpub.com/geneticalgorithmsfromtheorytorealprojects
- Gumroad: https://keremaktug.gumroad.com/l/iygzyk
- Amazon Kindle: coming soon

## What You Will Learn

- How genes, chromosomes, populations, selection, crossover, mutation, elitism, and fitness functions work
- How to build a genetic algorithm from scratch in C#
- How to design a reusable `GA.Core` library
- How chromosome representation changes from problem to problem
- How to apply genetic algorithms to routing, scheduling, packing, engineering design, machine learning, and neuroevolution
- How to visualize evolutionary search with WPF applications

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

## Repository Structure

The examples are intentionally organized as a learning path. Start with the from-scratch projects, then move to the reusable core library and real-world applications.

| No | Project | Type | Main Idea |
|---:|---|---|---|
| 01 | `Phrase.FromScratch.Console` | Console | Build a genetic algorithm from scratch |
| 02 | `Phrase.FromScratch.Wpf` | WPF | Visualize phrase evolution |
| 03 | `GA.Core` | Library | Reusable genetic algorithm engine |
| 04 | `Phrase.WithCore.Wpf` | WPF | Rebuild phrase evolution with `GA.Core` |
| 05 | `Cards.Wpf` | WPF | Card grouping and search |
| 06 | `EightQueens.Wpf` | WPF | Constraint solving with integer chromosomes |
| 07 | `Knapsack.Wpf` | WPF | Binary chromosome optimization |
| 08 | `TSP.Wpf` | WPF | Traveling Salesman Problem with permutation chromosomes |
| 09 | `Sudoku.Wpf` | WPF | Sudoku solving with fixed puzzle values |
| 10 | `Timetabling.Wpf` | WPF | Course scheduling with hard and soft constraints |
| 11 | `Rectangles.Wpf` | WPF | Rectangle packing with mixed integer genes |
| 12 | `AnalogRC.Wpf` | WPF | RC filter component selection |
| 13 | `AnalogOpAmp.Wpf` | WPF | Op-amp resistor selection for target gain |
| 14 | `MLHyperparameters.Console` | Console | ML.NET hyperparameter search |
| 15 | `RandomForestHyperparameters.Console` | Console | Random forest tuning |
| 16 | `GraphColoring.Wpf` | WPF | Graph coloring with conflict minimization |
| 17 | `VehicleRouting.Wpf` | WPF | Vehicle routing with capacity constraints |
| 18 | `FeatureSelection.Console` | Console | Feature subset selection for ML |
| 19 | `ImageApproximation.Wpf` | WPF | Approximate an image with evolved shapes |
| 20 | `MazeSolver.Wpf` | WPF | Evolve movement commands through a maze |
| 21 | `Rastrigin.Wpf` | WPF | Continuous function minimization |
| 22 | `GearTrain.Wpf` | WPF | Engineering optimization for gear ratios |
| 23 | `RubiksCube.Wpf` | WPF | Rubik's Cube move-sequence search and 3D visualization |
| 24 | `LunarLanderPolicy.Wpf` | WPF | Policy search for a lunar lander |
| 25 | `NeuralArchitectureSearch.Wpf` | WPF | Evolve policy network architecture |
| 26 | `SnakeNeuroevolution.Wpf` | WPF | Neuroevolution for a Snake game agent |
| 27 | `EvolvedAntenna.Wpf` | WPF | Antenna design inspired by NASA evolutionary antenna research |

## Requirements

- Windows
- .NET 9 SDK or later
- Visual Studio 2022 or another .NET-capable IDE

Most visual examples are WPF applications, so they are intended to run on Windows.

## How to Run

Clone the repository:

```bash
git clone https://github.com/keremaktug/Genetic-Algorithms-From-Theory-to-Real-Projects.git
cd Genetic-Algorithms-From-Theory-to-Real-Projects
```

Open the solution:

```text
Codes.slnx
```

Then choose any project as the startup project and run it.

Or run a specific project from the command line:

```bash
dotnet run --project 08.TSP.Wpf/08.TSP.Wpf.csproj
```

For console examples:

```bash
dotnet run --project 01.Phrase.FromScratch.Console/01.Phrase.FromScratch.Console.csproj
```

## Recommended Learning Path

If you are new to genetic algorithms, use this order:

1. Start with `01.Phrase.FromScratch.Console`
2. Run `02.Phrase.FromScratch.Wpf` to see the same idea visually
3. Study `03.GA.Core` to understand the reusable solver design
4. Move to `06.EightQueens.Wpf`, `07.Knapsack.Wpf`, and `08.TSP.Wpf`
5. Continue with larger real-world examples such as timetabling, vehicle routing, and engineering optimization
6. Finish with machine learning and neuroevolution examples

## Core Design

The reusable library separates the genetic algorithm workflow from problem-specific logic.

Typical responsibilities are:

- `IProblem<TGene>` defines how chromosomes are created and evaluated
- selection chooses parents
- crossover combines parent chromosomes
- mutation introduces variation
- elitism preserves the best solutions
- the solver manages generations and tracks progress

This makes it possible to reuse the same solver across very different problems.

## Example Categories

### Classic Optimization

- Phrase evolution
- 8-Queens
- Knapsack
- Traveling Salesman Problem
- Sudoku
- Graph coloring

### Planning and Logistics

- Timetabling
- Vehicle routing
- Rectangle packing
- Maze solving

### Engineering Optimization

- Analog RC filter design
- Analog op-amp gain design
- Rastrigin function minimization
- Gear train optimization
- Evolved antenna design

### Machine Learning and AI

- ML.NET hyperparameter tuning
- Random forest hyperparameter tuning
- Feature selection
- Lunar lander policy search
- Neural architecture search
- Snake neuroevolution

### Creative and Visual Search

- Image approximation
- Rubik's Cube solver
- Interactive WPF visualizations

## Who This Is For

This repository is useful for:

- C# and .NET developers learning Genetic Algorithms
- Students studying optimization and evolutionary computation
- Engineers looking for practical optimization examples
- Developers interested in machine learning optimization
- Anyone who learns algorithms better by building visual projects

## Source Code Philosophy

The code is written to be:

- readable
- project-based
- easy to modify
- close to the explanations in the book
- useful for experimenting with GA parameters

The examples are not meant to hide the algorithm behind a black box. They are designed to make representation, fitness design, and evolutionary behavior visible.

## Companion Book

The full explanation, step-by-step implementation details, and project walkthroughs are included in the book:

**Genetic Algorithms From Theory to Real Projects**

- Leanpub: https://leanpub.com/geneticalgorithmsfromtheorytorealprojects
- Gumroad: https://keremaktug.gumroad.com/l/iygzyk

## Feedback

If you find an issue, have an improvement idea, or want to suggest a new genetic algorithm example, feel free to open an issue.

## License

This repository is provided as companion source code for the book. Check the repository license and book terms before using the code in commercial projects.
