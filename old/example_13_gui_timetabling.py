from __future__ import annotations

import queue
import random
import threading
import tkinter as tk
from dataclasses import dataclass
from tkinter import messagebox, ttk

import numpy as np
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from matplotlib.figure import Figure

from core.ga_solver1 import Chromosome, CrossoverType, GeneticSolver, MutationType, Population


# =========================================================
# Problem-specific data
# =========================================================

DAYS = ["Mon", "Tue", "Wed", "Thu", "Fri"]
HOURS = ["09:00", "10:00", "11:00", "13:00"]
ROOMS = ["R101", "R102", "Lab1"]

DAY_COUNT = len(DAYS)
HOUR_COUNT = len(HOURS)
ROOM_COUNT = len(ROOMS)
TIMESLOT_COUNT = DAY_COUNT * HOUR_COUNT


@dataclass(frozen=True)
class Session:
    session_id: int
    course: str
    teacher: str
    student_group: str
    preferred_room: str | None = None
    forbidden_timeslots: tuple[int, ...] = ()


def day_hour_to_timeslot(day_index: int, hour_index: int) -> int:
    return day_index * HOUR_COUNT + hour_index


SESSIONS: list[Session] = [
    Session(1, "Math", "T_Alice", "G1", forbidden_timeslots=(day_hour_to_timeslot(0, 0),)),
    Session(2, "Math", "T_Alice", "G2"),
    Session(3, "Math", "T_Alice", "G3"),
    Session(4, "Math", "T_Alice", "G4"),

    Session(5, "Physics", "T_Bob", "G1", "Lab1"),
    Session(6, "Physics", "T_Bob", "G2", "Lab1"),
    Session(7, "Physics", "T_Bob", "G3", "Lab1"),
    Session(8, "Physics", "T_Bob", "G4", "Lab1"),

    Session(9, "Programming", "T_Carla", "G1", "Lab1"),
    Session(10, "Programming", "T_Carla", "G2", "Lab1"),
    Session(11, "Programming", "T_Carla", "G3", "Lab1"),
    Session(12, "Programming", "T_Carla", "G4", "Lab1"),

    Session(13, "History", "T_David", "G1"),
    Session(14, "History", "T_David", "G2"),
    Session(15, "History", "T_David", "G3"),
    Session(16, "History", "T_David", "G4"),

    Session(17, "English", "T_Eva", "G1", forbidden_timeslots=(day_hour_to_timeslot(4, 3),)),
    Session(18, "English", "T_Eva", "G2"),
    Session(19, "English", "T_Eva", "G3"),
    Session(20, "English", "T_Eva", "G4"),

    Session(21, "Biology", "T_Frank", "G1", "Lab1"),
    Session(22, "Biology", "T_Frank", "G2", "Lab1"),
    Session(23, "Biology", "T_Frank", "G3", "Lab1"),
    Session(24, "Biology", "T_Frank", "G4", "Lab1"),
]

SESSION_COUNT = len(SESSIONS)


def gene_upper_bound() -> int:
    return TIMESLOT_COUNT * ROOM_COUNT - 1


def decode_assignment(gene: int) -> tuple[int, int]:
    timeslot_index = gene // ROOM_COUNT
    room_index = gene % ROOM_COUNT
    return timeslot_index, room_index


def decode_timeslot(timeslot_index: int) -> tuple[int, int]:
    day_index = timeslot_index // HOUR_COUNT
    hour_index = timeslot_index % HOUR_COUNT
    return day_index, hour_index


def random_gene() -> int:
    return random.randint(0, gene_upper_bound())


def timetable_generator() -> Chromosome[int]:
    genes = np.random.randint(0, gene_upper_bound() + 1, size=SESSION_COUNT, dtype=np.int32)
    return Chromosome(genes.tolist())


def get_assignments(chromosome: Chromosome[int]) -> list[tuple[Session, int, int, int]]:
    assignments = []
    for i, gene in enumerate(chromosome.data):
        session = SESSIONS[i]
        timeslot_index, room_index = decode_assignment(int(gene))
        assignments.append((session, int(gene), timeslot_index, room_index))
    return assignments


def decode(chromosome: Chromosome[int]) -> str:
    parts: list[str] = []
    for session, _, timeslot_index, room_index in get_assignments(chromosome):
        day_index, hour_index = decode_timeslot(timeslot_index)
        parts.append(
            f"{session.course}/{session.student_group}@{DAYS[day_index]} {HOURS[hour_index]} {ROOMS[room_index]}"
        )
    return " | ".join(parts)


def hard_conflicts(chromosome: Chromosome[int]) -> list[str]:
    assignments = get_assignments(chromosome)
    conflicts: list[str] = []

    room_time_map: dict[tuple[int, int], list[Session]] = {}
    teacher_time_map: dict[tuple[str, int], list[Session]] = {}
    group_time_map: dict[tuple[str, int], list[Session]] = {}

    for session, _, timeslot_index, room_index in assignments:
        room_time_map.setdefault((room_index, timeslot_index), []).append(session)
        teacher_time_map.setdefault((session.teacher, timeslot_index), []).append(session)
        group_time_map.setdefault((session.student_group, timeslot_index), []).append(session)

    for (room_index, timeslot_index), sessions in room_time_map.items():
        if len(sessions) > 1:
            d, h = decode_timeslot(timeslot_index)
            conflicts.append(f"Room clash {ROOMS[room_index]} at {DAYS[d]} {HOURS[h]}")

    for (teacher, timeslot_index), sessions in teacher_time_map.items():
        if len(sessions) > 1:
            d, h = decode_timeslot(timeslot_index)
            conflicts.append(f"Teacher clash {teacher} at {DAYS[d]} {HOURS[h]}")

    for (group, timeslot_index), sessions in group_time_map.items():
        if len(sessions) > 1:
            d, h = decode_timeslot(timeslot_index)
            conflicts.append(f"Group clash {group} at {DAYS[d]} {HOURS[h]}")

    return conflicts


def soft_penalty(chromosome: Chromosome[int]) -> tuple[float, list[str]]:
    assignments = get_assignments(chromosome)
    penalty = 0.0
    notes: list[str] = []

    teacher_day_hours: dict[tuple[str, int], list[int]] = {}
    group_day_hours: dict[tuple[str, int], list[int]] = {}
    group_day_count: dict[tuple[str, int], int] = {}

    for session, _, timeslot_index, room_index in assignments:
        day_index, hour_index = decode_timeslot(timeslot_index)

        if session.preferred_room is not None and ROOMS[room_index] != session.preferred_room:
            penalty += 8.0
            notes.append(f"Pref room miss: {session.course}/{session.student_group}")

        if timeslot_index in session.forbidden_timeslots:
            penalty += 15.0
            notes.append(f"Forbidden slot: {session.course}/{session.student_group}")

        if hour_index == HOUR_COUNT - 1:
            penalty += 2.0
            notes.append(f"Late class: {session.course}/{session.student_group}")

        teacher_day_hours.setdefault((session.teacher, day_index), []).append(hour_index)
        group_day_hours.setdefault((session.student_group, day_index), []).append(hour_index)
        group_day_count[(session.student_group, day_index)] = group_day_count.get((session.student_group, day_index), 0) + 1

    for (teacher, day_index), hours in teacher_day_hours.items():
        hours = sorted(hours)
        for i in range(len(hours) - 1):
            gap = hours[i + 1] - hours[i]
            if gap > 1:
                penalty += (gap - 1) * 3.0
                notes.append(f"Teacher gap: {teacher} {DAYS[day_index]}")

    for (group, day_index), hours in group_day_hours.items():
        hours = sorted(hours)
        for i in range(len(hours) - 1):
            gap = hours[i + 1] - hours[i]
            if gap > 1:
                penalty += (gap - 1) * 2.0
                notes.append(f"Group gap: {group} {DAYS[day_index]}")

    for (group, day_index), count in group_day_count.items():
        if count > 2:
            penalty += (count - 2) * 4.0
            notes.append(f"Heavy day: {group} {DAYS[day_index]}")

    return penalty, notes


def calculate_fitness(chromosome: Chromosome[int]) -> float:
    hard = hard_conflicts(chromosome)
    soft, _ = soft_penalty(chromosome)
    return float(len(hard) * 1000.0 + soft)


def stop_condition(best: Chromosome[int]) -> bool:
    return best.fitness == 0.0


def population_to_index_matrix(population_snapshot: list[list[int]]) -> np.ndarray:
    if not population_snapshot:
        return np.empty((0, 0), dtype=np.int32)
    return np.asarray(population_snapshot, dtype=np.int32)


def generate_color_scheme(count: int) -> np.ndarray:
    import colorsys

    colors = np.zeros((count, 3), dtype=np.uint8)
    for i in range(count):
        h = i / max(1, count)
        r, g, b = colorsys.hls_to_rgb(h, 0.5, 0.75)
        colors[i] = (int(r * 255), int(g * 255), int(b * 255))
    return colors


# =========================================================
# GUI
# =========================================================

class TimetablingApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("Scheduling & Timetabling")
        self.root.geometry("1380x820")
        self.root.resizable(False, False)

        self.solver: GeneticSolver[int] | None = None
        self.is_running = False
        self.best_solution: Chromosome[int] | None = None

        self.best_history: list[float] = []
        self.avg_history: list[float] = []
        self.iteration_history: list[int] = []

        self.ui_queue: queue.Queue = queue.Queue()

        # optimized values
        self.ui_update_every = 8
        self.pool_redraw_every = 30
        self.queue_poll_ms = 60
        self.max_queue_items_per_tick = 2
        self.max_result_items = 80
        self.timetable_redraw_every = 16

        self.fitness_var = tk.StringVar(value="Fitness: -")
        self.hard_var = tk.StringVar(value="Hard Conflicts: -")
        self.soft_var = tk.StringVar(value="Soft Penalty: -")

        self.pool_color_scheme = generate_color_scheme(gene_upper_bound() + 1)
        self.pool_image: tk.PhotoImage | None = None

        self.build_ui()
        self.redraw_timetable()
        self.root.after(self.queue_poll_ms, self.process_ui_queue)

    def build_ui(self) -> None:
        controls_frame = tk.LabelFrame(self.root, text="Controls", padx=10, pady=10)
        controls_frame.place(x=10, y=10, width=210, height=310)

        tk.Label(controls_frame, text="Crossover Type").pack(anchor="w")
        self.cmb_crossover = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["OnePointCrossover", "UniformCrossover"],
        )
        self.cmb_crossover.current(1)
        self.cmb_crossover.pack(fill="x", pady=(4, 10))

        tk.Label(controls_frame, text="Mutation Type").pack(anchor="w")
        self.cmb_mutation = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["RandomReset"],
        )
        self.cmb_mutation.current(0)
        self.cmb_mutation.pack(fill="x", pady=(4, 10))

        tk.Label(controls_frame, text="Elitism Rate").pack(anchor="w")
        self.cmb_elitism = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["0.1", "0.2", "0.3", "0.4", "0.5"],
        )
        self.cmb_elitism.current(2)
        self.cmb_elitism.pack(fill="x", pady=(4, 10))

        tk.Label(controls_frame, text="Population Size (1024)").pack(anchor="w")
        self.cmb_population = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["1", "2", "4", "8", "16"],
        )
        self.cmb_population.current(2)
        self.cmb_population.pack(fill="x", pady=(4, 10))

        self.btn_start = tk.Button(controls_frame, text="Evolve", command=self.start_solver)
        self.btn_start.pack(fill="x", pady=(10, 0))

        stats_frame = tk.LabelFrame(self.root, text="Best Solution", padx=10, pady=10)
        stats_frame.place(x=10, y=335, width=210, height=120)

        tk.Label(stats_frame, textvariable=self.fitness_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(stats_frame, textvariable=self.hard_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(stats_frame, textvariable=self.soft_var, anchor="w").pack(fill="x", pady=2)

        best_frame = tk.LabelFrame(self.root, text="Best Chromosomes", padx=5, pady=5)
        best_frame.place(x=230, y=10, width=1140, height=170)

        self.result_list = tk.Listbox(best_frame, font=("Consolas", 9))
        self.result_list.pack(fill="both", expand=True)

        timetable_frame = tk.LabelFrame(self.root, text="Timetable", padx=8, pady=8)
        timetable_frame.place(x=230, y=190, width=700, height=610)

        self.timetable_canvas = tk.Canvas(
            timetable_frame,
            width=660,
            height=570,
            bg="white",
            highlightthickness=0,
        )
        self.timetable_canvas.pack(fill="both", expand=True)

        chart_frame = tk.LabelFrame(self.root, text="Fitness Chart", padx=8, pady=8)
        chart_frame.place(x=950, y=190, width=420, height=240)

        self.fig = Figure(figsize=(4.2, 2.2), dpi=100)
        self.ax = self.fig.add_subplot(111)
        self.ax.set_xlabel("Iteration")
        self.ax.set_ylabel("Fitness")
        self.ax.grid(True, alpha=0.3)
        self.ax.set_xlim(0, 1)
        self.ax.set_ylim(0, 1)

        self.best_line, = self.ax.plot([], [], label="Best Fitness")
        self.avg_line, = self.ax.plot([], [], label="Average Fitness")
        self.ax.legend()

        self.canvas_chart = FigureCanvasTkAgg(self.fig, master=chart_frame)
        self.canvas_chart.get_tk_widget().pack(fill="both", expand=True)

        pool_frame = tk.LabelFrame(self.root, text="Population Chart", padx=8, pady=8)
        pool_frame.place(x=950, y=440, width=420, height=120)

        self.pool_canvas = tk.Canvas(
            pool_frame,
            width=380,
            height=80,
            bg="white",
            highlightthickness=0,
        )
        self.pool_canvas.pack(fill="both", expand=True)

        conflict_frame = tk.LabelFrame(self.root, text="Constraint Summary", padx=8, pady=8)
        conflict_frame.place(x=950, y=570, width=420, height=230)

        self.conflict_list = tk.Listbox(conflict_frame, font=("Consolas", 9))
        self.conflict_list.pack(fill="both", expand=True)

    def clear_ui(self) -> None:
        self.result_list.delete(0, tk.END)
        self.conflict_list.delete(0, tk.END)

        self.best_history.clear()
        self.avg_history.clear()
        self.iteration_history.clear()

        self.best_line.set_data([], [])
        self.avg_line.set_data([], [])
        self.ax.set_xlim(0, 1)
        self.ax.set_ylim(0, 1)
        self.canvas_chart.draw_idle()

        self.pool_canvas.delete("all")
        self.pool_image = None

        self.fitness_var.set("Fitness: -")
        self.hard_var.set("Hard Conflicts: -")
        self.soft_var.set("Soft Penalty: -")

        self.best_solution = None
        self.redraw_timetable()

    def flush_ui_queue(self) -> None:
        while True:
            try:
                self.ui_queue.get_nowait()
            except queue.Empty:
                break

    def map_crossover_type(self) -> CrossoverType:
        if self.cmb_crossover.get() == "OnePointCrossover":
            return CrossoverType.ONE_POINT
        return CrossoverType.UNIFORM

    def start_solver(self) -> None:
        if self.is_running:
            return

        self.clear_ui()
        self.flush_ui_queue()

        self.is_running = True
        self.btn_start.config(state="disabled")

        population_factor = int(self.cmb_population.get())
        elitism_rate = float(self.cmb_elitism.get())
        crossover_type = self.map_crossover_type()

        self.solver = GeneticSolver[int](
            population_size=1024 * population_factor,
            iteration_count=3000,
            elitism_ratio=elitism_rate,
            mutation_ratio=0.08,
            crossover_type=crossover_type,
            mutation_type=MutationType.RANDOM_RESET,
            maximize_fitness=False,
        )

        self.solver.generator_function = timetable_generator
        self.solver.fitness_function = calculate_fitness
        self.solver.random_gene_function = random_gene
        self.solver.stop_condition_function = stop_condition
        self.solver.iteration_completed_callback = self.on_iteration_completed

        threading.Thread(target=self.run_solver, daemon=True).start()

    def run_solver(self) -> None:
        try:
            if self.solver is not None:
                result = self.solver.evolve()
                if result is not None:
                    self.ui_queue.put(("finished", result.copy()))
        except Exception as exc:
            self.ui_queue.put(("error", str(exc)))
        finally:
            self.ui_queue.put(("run_finished",))

    def finish_run(self) -> None:
        self.is_running = False
        self.btn_start.config(state="normal")

    def on_iteration_completed(self, iteration: int, average_fitness: float, best) -> None:
        if iteration != 0 and iteration % self.ui_update_every != 0:
            return

        redraw_timetable = (iteration % self.timetable_redraw_every == 0) or (best.fitness == 0)

        pool_snapshot = None
        if self.solver is not None and iteration % self.pool_redraw_every == 0:
            pool_snapshot = self.snapshot_population(self.solver.population)

        self.ui_queue.put(("iteration", iteration, average_fitness, best.copy(), pool_snapshot, redraw_timetable))

    def process_ui_queue(self) -> None:
        processed = 0
        while processed < self.max_queue_items_per_tick:
            try:
                item = self.ui_queue.get_nowait()
            except queue.Empty:
                break

            kind = item[0]

            if kind == "iteration":
                _, iteration, average_fitness, best, pool_snapshot, redraw_timetable = item
                self.update_ui(iteration, average_fitness, best, pool_snapshot, redraw_timetable)
            elif kind == "finished":
                _, solution = item
                self.update_solution_panels(solution, redraw_timetable=True)
            elif kind == "error":
                _, error_message = item
                messagebox.showerror("Error", error_message)
            elif kind == "run_finished":
                self.finish_run()

            processed += 1

        self.root.after(self.queue_poll_ms, self.process_ui_queue)

    def append_result(self, text: str) -> None:
        self.result_list.insert(tk.END, text)
        if self.result_list.size() > self.max_result_items:
            overflow = self.result_list.size() - self.max_result_items
            self.result_list.delete(0, overflow - 1)
        self.result_list.yview_moveto(1.0)

    def update_chart(self) -> None:
        self.best_line.set_data(self.iteration_history, self.best_history)
        self.avg_line.set_data(self.iteration_history, self.avg_history)

        max_x = max(1, self.iteration_history[-1] + 1) if self.iteration_history else 1
        min_y = min(self.best_history + self.avg_history, default=0.0)
        max_y = max(self.best_history + self.avg_history, default=1.0)

        if min_y == max_y:
            min_y -= 1
            max_y += 1

        self.ax.set_xlim(0, max_x)
        self.ax.set_ylim(min_y * 0.98, max_y * 1.02)
        self.canvas_chart.draw_idle()

    def update_solution_panels(self, chromosome, redraw_timetable: bool = True) -> None:
        self.best_solution = chromosome

        hard = hard_conflicts(chromosome)
        soft, notes = soft_penalty(chromosome)

        self.fitness_var.set(f"Fitness: {calculate_fitness(chromosome):.2f}")
        self.hard_var.set(f"Hard Conflicts: {len(hard)}")
        self.soft_var.set(f"Soft Penalty: {soft:.2f}")

        self.conflict_list.delete(0, tk.END)
        if not hard and not notes:
            self.conflict_list.insert(tk.END, "No conflicts / penalties")
        else:
            for line in hard[:10]:
                self.conflict_list.insert(tk.END, f"HARD: {line}")
            for line in notes[:15]:
                self.conflict_list.insert(tk.END, f"SOFT: {line}")

        if redraw_timetable:
            self.redraw_timetable()

    def update_ui(self, iteration: int, average_fitness: float, best, population_snapshot, redraw_timetable: bool) -> None:
        self.append_result(f"{iteration:04d}: {decode(best)}")
        self.iteration_history.append(iteration)
        self.best_history.append(best.fitness)
        self.avg_history.append(average_fitness)

        self.update_chart()
        self.update_solution_panels(best, redraw_timetable=redraw_timetable)

        if population_snapshot is not None:
            self.draw_population_chart(population_snapshot)

    def snapshot_population(self, population: Population[int]) -> np.ndarray:
        return population_to_index_matrix([chromosome.data for chromosome in population.chromosomes])

    def _safe_canvas_size(self, canvas: tk.Canvas, default_w: int, default_h: int) -> tuple[int, int]:
        width = int(canvas.winfo_width())
        height = int(canvas.winfo_height())
        if width <= 2:
            width = default_w
        if height <= 2:
            height = default_h
        return width, height

    def redraw_timetable(self) -> None:
        self.timetable_canvas.delete("all")

        width, height = self._safe_canvas_size(self.timetable_canvas, 660, 570)

        left_margin = 90
        top_margin = 40
        grid_w = width - left_margin - 10
        grid_h = height - top_margin - 10

        col_count = DAY_COUNT * ROOM_COUNT
        row_count = HOUR_COUNT

        cell_w = grid_w / col_count
        cell_h = grid_h / row_count

        for d in range(DAY_COUNT):
            block_x1 = left_margin + d * ROOM_COUNT * cell_w
            block_x2 = block_x1 + ROOM_COUNT * cell_w
            self.timetable_canvas.create_rectangle(block_x1, 5, block_x2, top_margin, fill="#ddebf7", outline="black")
            self.timetable_canvas.create_text((block_x1 + block_x2) / 2, 22, text=DAYS[d], font=("Arial", 10, "bold"))

            for r in range(ROOM_COUNT):
                x1 = left_margin + (d * ROOM_COUNT + r) * cell_w
                x2 = x1 + cell_w
                self.timetable_canvas.create_rectangle(x1, top_margin, x2, top_margin + 22, fill="#f2f2f2", outline="black")
                self.timetable_canvas.create_text((x1 + x2) / 2, top_margin + 11, text=ROOMS[r], font=("Arial", 8))

        for h in range(HOUR_COUNT):
            y1 = top_margin + 22 + h * cell_h
            y2 = y1 + cell_h
            self.timetable_canvas.create_rectangle(5, y1, left_margin, y2, fill="#f2f2f2", outline="black")
            self.timetable_canvas.create_text(left_margin / 2, (y1 + y2) / 2, text=HOURS[h], font=("Arial", 9, "bold"))

        for d in range(DAY_COUNT):
            for r in range(ROOM_COUNT):
                for h in range(HOUR_COUNT):
                    col_index = d * ROOM_COUNT + r
                    x1 = left_margin + col_index * cell_w
                    x2 = x1 + cell_w
                    y1 = top_margin + 22 + h * cell_h
                    y2 = y1 + cell_h
                    self.timetable_canvas.create_rectangle(x1, y1, x2, y2, fill="white", outline="#cccccc")

        if self.best_solution is None:
            return

        assignments = get_assignments(self.best_solution)

        for session, _, timeslot_index, room_index in assignments:
            day_index, hour_index = decode_timeslot(timeslot_index)
            col_index = day_index * ROOM_COUNT + room_index

            x1 = left_margin + col_index * cell_w
            x2 = x1 + cell_w
            y1 = top_margin + 22 + hour_index * cell_h
            y2 = y1 + cell_h

            color = self.group_color(session.student_group)

            self.timetable_canvas.create_rectangle(x1, y1, x2, y2, fill=color, outline="black")
            self.timetable_canvas.create_text((x1 + x2) / 2, y1 + 13, text=session.course, font=("Arial", 8, "bold"))
            self.timetable_canvas.create_text((x1 + x2) / 2, y1 + 28, text=session.teacher, font=("Arial", 7))
            self.timetable_canvas.create_text((x1 + x2) / 2, y1 + 43, text=session.student_group, font=("Arial", 7))

    def group_color(self, group: str) -> str:
        mapping = {
            "G1": "#cfe2f3",
            "G2": "#d9ead3",
            "G3": "#f4cccc",
            "G4": "#fff2cc",
        }
        return mapping.get(group, "#eeeeee")

    def draw_population_chart(self, population_snapshot: np.ndarray) -> None:
        if population_snapshot.size == 0:
            return

        canvas_width, canvas_height = self._safe_canvas_size(self.pool_canvas, 380, 80)

        rows, cols = population_snapshot.shape
        if rows == 0 or cols == 0:
            return

        img_w = canvas_width
        img_h = canvas_height

        scaled = np.clip(population_snapshot, 0, gene_upper_bound()).astype(np.int32)

        row_idx = np.linspace(0, rows - 1, img_h).astype(np.int32)
        col_idx = np.linspace(0, cols - 1, img_w).astype(np.int32)

        sampled = scaled[row_idx][:, col_idx]
        rgb = self.pool_color_scheme[sampled]

        ppm_header = f"P6 {img_w} {img_h} 255\n".encode("ascii")
        ppm_data = ppm_header + rgb.tobytes()

        self.pool_image = tk.PhotoImage(data=ppm_data, format="PPM")
        self.pool_canvas.delete("all")
        self.pool_canvas.create_image(0, 0, anchor="nw", image=self.pool_image)

    def run(self) -> None:
        self.root.mainloop()

if __name__ == "__main__":
    root = tk.Tk()
    app = TimetablingApp(root)
    app.run()