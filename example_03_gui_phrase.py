import random
import threading
import tkinter as tk
from tkinter import ttk, messagebox

from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
import matplotlib.pyplot as plt

from core.ga_solver import (
    Chromosome,
    CrossoverType,
    GeneticSolver,
    MutationType,
)

LETTERS = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ 1234567890,."
PHRASE = "Those who live in glass houses should not throw stones"

def decode(chromosome: Chromosome[str]) -> str:
    return "".join(chromosome.data)

def random_gene() -> str:
    return random.choice(LETTERS)

def phrase_generator() -> Chromosome[str]:
    return Chromosome([random_gene() for _ in range(len(PHRASE))])

def calculate_fitness(chromosome: Chromosome[str]) -> float:
    return float(
        sum(abs(ord(PHRASE[i]) - ord(chromosome.data[i])) for i in range(len(PHRASE)))
    )

def stop_condition(best: Chromosome[str]) -> bool:
    return best.fitness == 0

class PhraseEvolutionApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("Phrase Evolution GUI")
        self.root.geometry("1100x700")
        self.root.resizable(False, False)

        self.solver: GeneticSolver[str] | None = None
        self.is_running = False

        self.best_history: list[float] = []
        self.avg_history: list[float] = []

        self.build_ui()

    def build_ui(self) -> None:
        left_frame = tk.LabelFrame(self.root, text="Parameters", padx=10, pady=10)
        left_frame.place(x=10, y=10, width=180, height=330)

        tk.Label(left_frame, text="Crossover Type").pack(anchor="w")
        self.cmb_crossover = ttk.Combobox(
            left_frame,
            state="readonly",
            values=["OnePointCrossover", "UniformCrossover", "PMX"],
        )
        self.cmb_crossover.current(1)
        self.cmb_crossover.pack(fill="x", pady=(0, 10))

        tk.Label(left_frame, text="Elitism Rate").pack(anchor="w")
        self.cmb_elitism = ttk.Combobox(
            left_frame,
            state="readonly",
            values=["0.1", "0.2", "0.3", "0.4", "0.5", "0.6", "0.7", "0.8", "0.9", "1.0"],
        )
        self.cmb_elitism.current(2)
        self.cmb_elitism.pack(fill="x", pady=(0, 10))

        tk.Label(left_frame, text="Mutation Type").pack(anchor="w")
        self.cmb_mutation = ttk.Combobox(
            left_frame,
            state="readonly",
            values=["RandomReset", "Swap", "Scramble", "Inverse"],
        )
        self.cmb_mutation.current(0)
        self.cmb_mutation.pack(fill="x", pady=(0, 10))

        tk.Label(left_frame, text="Population Size Factor (1024)").pack(anchor="w")
        self.cmb_population = ttk.Combobox(
            left_frame,
            state="readonly",
            values=["1", "2", "4", "8", "16", "32", "64", "128"],
        )
        self.cmb_population.current(5)
        self.cmb_population.pack(fill="x", pady=(0, 15))

        self.btn_start = tk.Button(left_frame, text="Start", command=self.start_solver)
        self.btn_start.pack(fill="x")

        result_frame = tk.LabelFrame(self.root, text="Best Chromosomes", padx=5, pady=5)
        result_frame.place(x=200, y=10, width=880, height=220)

        self.result_list = tk.Listbox(result_frame, font=("Consolas", 10))
        self.result_list.pack(fill="both", expand=True)

        chart_frame = tk.LabelFrame(self.root, text="Fitness Chart", padx=5, pady=5)
        chart_frame.place(x=200, y=240, width=430, height=440)

        self.fig, self.ax = plt.subplots(figsize=(5, 4), dpi=100)
        self.canvas = FigureCanvasTkAgg(self.fig, master=chart_frame)
        self.canvas.get_tk_widget().pack(fill="both", expand=True)

        diversity_frame = tk.LabelFrame(self.root, text="Population Diversity", padx=5, pady=5)
        diversity_frame.place(x=650, y=240, width=430, height=440)

        self.population_canvas = tk.Canvas(diversity_frame, bg="white")
        self.population_canvas.pack(fill="both", expand=True)

    def clear_ui(self) -> None:
        self.result_list.delete(0, tk.END)
        self.best_history.clear()
        self.avg_history.clear()
        self.ax.clear()
        self.canvas.draw()
        self.population_canvas.delete("all")

    def map_crossover_type(self) -> CrossoverType:
        value = self.cmb_crossover.get()
        if value == "OnePointCrossover":
            return CrossoverType.ONE_POINT
        if value == "UniformCrossover":
            return CrossoverType.UNIFORM
        return CrossoverType.PMX

    def map_mutation_type(self) -> MutationType:
        value = self.cmb_mutation.get()
        if value == "RandomReset":
            return MutationType.RANDOM_RESET
        if value == "Swap":
            return MutationType.SWAP
        if value == "Scramble":
            return MutationType.SCRAMBLE
        return MutationType.INVERSE

    def start_solver(self) -> None:
        if self.is_running:
            return

        self.clear_ui()
        self.is_running = True
        self.btn_start.config(state="disabled")

        population_factor = int(self.cmb_population.get())
        elitism_rate = float(self.cmb_elitism.get())
        crossover_type = self.map_crossover_type()
        mutation_type = self.map_mutation_type()

        self.solver = GeneticSolver[str](
            population_size=1024 * population_factor,
            iteration_count=1000,
            elitism_ratio=elitism_rate,
            mutation_ratio=0.01,
            crossover_type=crossover_type,
            mutation_type=mutation_type,
            maximize_fitness=False,
        )

        self.solver.generator_function = phrase_generator
        self.solver.fitness_function = calculate_fitness
        self.solver.random_gene_function = random_gene
        self.solver.stop_condition_function = stop_condition
        self.solver.iteration_completed_callback = self.on_iteration_completed
        self.solver.solution_found_callback = self.on_solution_found

        threading.Thread(target=self.run_solver, daemon=True).start()

    def run_solver(self) -> None:
        try:
            if self.solver is not None:
                result = self.solver.evolve()
                if result is None:
                    self.root.after(
                        0,
                        lambda: messagebox.showinfo(
                            "Finished",
                            "Exact solution was not found within the iteration limit.",
                        ),
                    )
        finally:
            self.root.after(0, self.finish_run)

    def finish_run(self) -> None:
        self.is_running = False
        self.btn_start.config(state="normal")

    def on_iteration_completed(
        self,
        iteration: int,
        average_fitness: float,
        best: Chromosome[str],
    ) -> None:
        self.root.after(
            0,
            lambda: self.update_ui(iteration, average_fitness, best),
        )

    def on_solution_found(self, iteration: int, solution: Chromosome[str]) -> None:
        self.root.after(
            0,
            lambda: messagebox.showinfo(
                "Solution Found",
                f"Iteration Count: {iteration}\n{decode(solution)}",
            ),
        )

    def update_ui(
        self,
        iteration: int,
        average_fitness: float,
        best: Chromosome[str],
    ) -> None:
        self.result_list.insert(tk.END, decode(best))
        self.result_list.yview_moveto(1.0)

        self.best_history.append(best.fitness)
        self.avg_history.append(average_fitness)

        self.ax.clear()
        self.ax.plot(self.best_history, label="Best Fitness")
        self.ax.plot(self.avg_history, label="Average Fitness")
        self.ax.set_xlabel("Iteration")
        self.ax.set_ylabel("Fitness")
        self.ax.legend()
        self.ax.grid(True, alpha=0.3)
        self.canvas.draw()

        if self.solver is not None:
            self.draw_population_diversity(self.solver.population)

    def draw_population_diversity(self, population) -> None:
        self.population_canvas.delete("all")

        width = int(self.population_canvas.winfo_width())
        height = int(self.population_canvas.winfo_height())

        if width <= 1 or height <= 1:
            return

        letters_subset = LETTERS
        counts = {ch: 0 for ch in letters_subset}

        for chromosome in population.chromosomes:
            for gene in chromosome.data:
                if gene in counts:
                    counts[gene] += 1

        max_count = max(counts.values()) if counts else 1
        bar_count = len(letters_subset)
        bar_width = max(1, width / bar_count)

        for i, ch in enumerate(letters_subset):
            value = counts[ch]
            bar_height = 0 if max_count == 0 else (value / max_count) * (height - 20)

            x1 = i * bar_width
            y1 = height - bar_height
            x2 = (i + 1) * bar_width - 1
            y2 = height

            self.population_canvas.create_rectangle(x1, y1, x2, y2, fill="steelblue", outline="")

    def run(self) -> None:
        self.root.mainloop()

if __name__ == "__main__":
    root = tk.Tk()
    app = PhraseEvolutionApp(root)
    app.run()