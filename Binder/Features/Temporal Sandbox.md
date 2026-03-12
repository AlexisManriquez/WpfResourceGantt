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
1.  **Data Isolation**: Upon entering the sandbox, it deep-clones the entire live project hierarchy using `CloneHelper.cs`.
2.  **Time Travel**: It maintains a `SimulatedDate` (separate from `DateTime.Today`). The sandbox pushes this status date into the calculation engines to recalculate "forecasted" reality.
3.  **Simulation-Aware Scheduling**: Unlike the live engine, the sandbox dynamically adjusts task durations:
    - **Shrinking**: Completed tasks (100% progress) have their durations shrunk to match their `ActualFinishDate`.
    - **Extension**: Overdue, incomplete tasks have their durations automatically extended to the `SimulatedDate`.
4.  **Critical Path Alert**: The engine monitors shifts in the project network and displays a high-visibility alert banner if the critical path changes or if a delay pushes the final delivery date.

### B. Interactive Manipulator (`InteractiveManipulatorGraph.xaml`)
A custom high-fidelity WPF control for visual timeline manipulation.
- **Visual Curve Editing**: Users drag "dots" to adjust % Progress or Actual Hours.
- **Bidirectional Cascade**: Dragging a dot enforces a monotonic curve. Moving a dot up pushes future dots up; moving a dot down pulls past dots down.
- **Status Locking**: Dots at or before the `SimulatedDate` are considered "Reported History" and are locked during automated stress tests.

### C. Pre-Built Stress Test Scenarios
One-click presets that transform the sandbox into an automated risk assessment engine:
- **⚠ Critical Path Slip (+2 Weeks)**: Adds 10 business days to all current critical path leaf tasks.
- **👤 Resource Loss (20%)**: Reduces progress by 20% on all in-progress tasks, simulating a workforce reduction.
- **📉 Flat Progress (30 Days)**: Advances the simulated clock by 30 days with zero progress, showing the impact of a full work stoppage.
- **🚀 Optimistic Recovery**: Bumps behind-schedule tasks to their expected progress based on the current date and original end date.
- **🏛 Gov Shutdown (15 Days)**: Freezes progress and extends all incomplete task durations by 15 business days while advancing the clock.

### D. Impact Bar
The simulation dashboard's real-time KPI monitor.
- **Metrics**: Displays Live vs. Simulated values for SV, CV, SPI, CPI, EAC, and VAC.
- **Financial Delta**: Calculates the total "Cost of Delay" or "Recovery Gain" between the live project and the current simulation.

---

## 3. Workflow & Logic

1.  **Entry**: User clicks the **PLAYGROUND** button in the Ribbon.
2.  **Cloning**: All Systems/Tasks are cloned. `InitializeSandbox` captures the "Original Durations" to allow reset-free toggling.
3.  **Selection**: User selects a Task (Leaf node) in the tree.
4.  **Manipulation**: User drags a dot or runs a **Stress Test**.
5.  **Recalculation**:
    - `RecalculateSandbox()` restores original durations, then applies simulation-aware extensions/shrinks.
    - `ScheduleCalculationService` runs a CPM pass on the modified tree.
    - `EvmCalculationService` calculates performance metrics using the `SimulatedDate`.
6.  **Alerting**: If the end date slips or the CP changes, the **Critical Path Alert** banner appears.
7.  **Comparison**: The "Impact Bar" updates the deltas.

---

## 4. Visual Styles
The Sandbox uses a "Tactical Gold" and "High-Contrast Dark" aesthetic. Interactive elements use high-contrast primary buttons for reality-altering actions.

---

## 5. Metadata & Storage
- **Persistence**: Simulated curves are cached in memory via `_simulationProfiles`.
- **Reset**: "Reset Sandbox" clears all clones and re-fetches fresh data.
- **Exit**: Exit discards all changes. Simulation results are **never** written back to the SQL database.
