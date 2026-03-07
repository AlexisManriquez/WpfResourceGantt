# Temporal Sandbox (Simulation)

> See also: [[Gantt View]] for the base project engine, [[EVM]] for the financial metrics.

## 1. Overview
The **Temporal Sandbox** is a powerful "What-If" simulation environment that allows Project Managers to model potential schedule changes, progress delays, or resource reallocations without affecting the live production database.

It provides a safe playground to answer questions like:
- *"What is the financial impact if this flight-test gate slips by 3 weeks?"*
- *"Can we recover the schedule by increasing labor hours on the critical path?"*
- *"How does current progress affect our projected SV at the end of the quarter?"*

---

## 2. Key Components

### A. Simulation Engine (`SimulationViewModel.cs`)
The orchestrator of the sandbox. It performs the following critical roles:
1.  **Data Isolation**: Upon entering the sandbox, it deep-clones the entire live project hierarchy using `CloneHelper.cs`. This ensures that any "damage" done in the sandbox (deleting tasks, changing dates) is local to that session.
2.  **Time Travel**: It maintains a `SimulatedDate` (different from `DateTime.Today`). The sandbox pushes this status date into the `ScheduleCalculationService` and `EvmCalculationService` to recalculate "forecasted" reality.
3.  **Sandbox Injection**: It initializes a dedicated `GanttViewModel` instance and injects the cloned data into it. This allows the sandbox to have its own fully functional, but isolated, Gantt chart.

### B. Interactive Manipulator (`InteractiveManipulatorGraph.xaml`)
A custom high-fidelity WPF control that provides the "controls" for the simulation.
- **Visual Curve Editing**: Users can drag "dots" on a timeline to manually adjust the progress (%) or actual work hours of a specific task over its duration.
- **Dual Mode**: Supports toggling between **Progress (%)** manipulation and **Actual Hours** manipulation.
- **History vs. Future**: The graph identifies the "Status Date" (red line). Adjusting dots to the left of the line simulates history; adjusting dots to the right models future projections.

### C. Simulation View (`SimulationView.xaml`)
A comprehensive dashboard that combines:
- **Master Tree**: A navigation tree of the cloned project hierarchy.
- **IM Interface**: The large interactive graph area for the selected task.
- **Impact Bar**: Real-time KPI comparison (Live SV vs. Simulated SV) and the calculated "Financial Impact" delta.
- **Sandbox Gantt**: A read-only preview of the resulting Gantt chart, updated live as dots are dragged.

---

## 3. Workflow & Logic

1.  **Entry**: User clicks the **PLAYGROUND** button in the Ribbon.
2.  **Cloning**: All Systems/Tasks are cloned. The Gantt chart enters **Sandbox Mode** (destructive operations like DB Save/MPP Import are disabled).
3.  **Selection**: User selects a Task (Leaf node) in the tree.
4.  **Manipulation**: User drags a progress dot up or down.
    *   The `SimulationViewModel` catches the change.
    *   It updates the `Progress` or `ActualWork` on the cloned object.
    *   It triggers `RecalculateSandbox()`.
5.  **Recalculation**:
    *   `ScheduleCalculationService` runs a full CPM pass on the cloned tree.
    *   `EvmCalculationService` recalculates SV/CV/SPI/CPI using the `SimulatedDate`.
6.  **Comparison**: The "Impact Bar" updates to show the delta between the Live (DB) metrics and your new Sandbox reality.

---

## 4. Visual Styles
The Sandbox uses a "Tactical Gold" and "High-Contrast Dark" aesthetic to visually differentiate it from live operational views, signaling to the user that they are in a simulation.

---

## 5. Metadata & Storage
- **Persistence**: Simulated curves are cached in memory for the duration of the session via `_simulationProfiles`.
- **Reset**: Clicking "Reset Sandbox" clears all clones and re-fetches fresh data from the live `DataService`.
- **Exit**: Closing the view discards all simulated changes. Simulation results are **never** written back to the primary database.
