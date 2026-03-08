# Expand Impact Bar — Full EVM Suite

The current Impact Bar in [SimulationView.xaml](file:///d:/Project%20Management%20Application/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Features/Simulation/SimulationView.xaml) shows only **3 metrics** (Live SV, Simulated SV, Financial Impact Δ). This plan expands it to a full **6-metric EVM dashboard** with color-coded deltas, giving supervisors a single-glance view of schedule *and* cost impact.

## Proposed Changes

### ViewModel — Computed EVM Metrics

#### [MODIFY] [SimulationViewModel.cs](file:///d:/Project%20Management%20Application/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Features/Simulation/SimulationViewModel.cs)

Add the following **derived metrics** to the existing Live/Sim property groups. All formulas follow DoD/DAU EVMS standards.

**New Live Baseline Properties** (computed once in [InitializeSandbox()](file:///d:/Project%20Management%20Application/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Features/Simulation/SimulationViewModel.cs#107-129)):
```csharp
// Existing:
public double LiveTotalBcws { get; private set; }
public double LiveTotalBcwp { get; private set; }
public double LiveTotalSv => LiveTotalBcwp - LiveTotalBcws;

// NEW:
public double LiveTotalAcwp { get; private set; }
public double LiveTotalCv   => LiveTotalBcwp - LiveTotalAcwp;
public double LiveTotalSpi  => LiveTotalBcws != 0 ? Math.Round(LiveTotalBcwp / LiveTotalBcws, 2) : 0;
public double LiveTotalCpi  => LiveTotalAcwp != 0 ? Math.Round(LiveTotalBcwp / LiveTotalAcwp, 2) : 0;
public double LiveTotalBac  { get; private set; }   // Sum of all BACs
public double LiveTotalEac  => LiveTotalCpi != 0 ? Math.Round(LiveTotalBac / LiveTotalCpi, 2) : LiveTotalBac;
public double LiveTotalTcpi { get; private set; }    // Computed: (BAC - BCWP) / (BAC - ACWP)
```

**New Simulated Properties** (updated in [RecalculateSandbox()](file:///d:/Project%20Management%20Application/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Features/Simulation/SimulationViewModel.cs#130-145)):
```csharp
// Existing:
public double SimTotalBcws { ... }
public double SimTotalBcwp { ... }
public double SimTotalSv   => SimTotalBcwp - SimTotalBcws;

// NEW:
public double SimTotalAcwp { ... }   // with OnPropertyChanged chain
public double SimTotalCv   => SimTotalBcwp - SimTotalAcwp;
public double SimTotalSpi  => SimTotalBcws != 0 ? Math.Round(SimTotalBcwp / SimTotalBcws, 2) : 0;
public double SimTotalCpi  => SimTotalAcwp != 0 ? Math.Round(SimTotalBcwp / SimTotalAcwp, 2) : 0;
public double SimTotalBac  { ... }
public double SimTotalEac  => SimTotalCpi != 0 ? Math.Round(SimTotalBac / SimTotalCpi, 2) : SimTotalBac;
public double SimTotalTcpi { ... }   // Computed: (BAC - BCWP) / (BAC - ACWP)
```

**New Delta (Impact) Properties**:
```csharp
public double ImpactSv   => SimTotalSv - LiveTotalSv;        // Existing
public double ImpactCv   => SimTotalCv - LiveTotalCv;        // NEW
public double ImpactSpi  => SimTotalSpi - LiveTotalSpi;      // NEW
public double ImpactCpi  => SimTotalCpi - LiveTotalCpi;      // NEW
public double ImpactEac  => SimTotalEac - LiveTotalEac;      // NEW (positive = bad for EAC)
```

**TCPI Calculation Logic** (in both [InitializeSandbox](file:///d:/Project%20Management%20Application/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Features/Simulation/SimulationViewModel.cs#107-129) and [RecalculateSandbox](file:///d:/Project%20Management%20Application/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Features/Simulation/SimulationViewModel.cs#130-145)):
```csharp
// TCPI = (BAC - BCWP) / (BAC - ACWP)
// If denominator <= 0, the budget is fully consumed → display ∞ or "N/A"
double denominator = totalBac - totalAcwp;
tcpi = denominator > 0 ? Math.Round((totalBac - totalBcwp) / denominator, 2) : double.PositiveInfinity;
```

**[RecalculateSandbox()](file:///d:/Project%20Management%20Application/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Features/Simulation/SimulationViewModel.cs#130-145) Updates:**
Add the same ACWP/BAC summation pattern already used for BCWS/BCWP, then raise `OnPropertyChanged` for all new computed properties.

---

### Value Converter — Color-Coded Deltas

#### [NEW] [EvmDeltaColorConverter.cs](file:///d:/Project%20Management%20Application/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Converters/EvmDeltaColorConverter.cs)

A `IValueConverter` that converts a `double` delta value to a `SolidColorBrush`:
- **Positive delta on SV/CV/SPI/CPI** → Green (`#4CAF50`) — metric improved
- **Negative delta on SV/CV/SPI/CPI** → Red (`#FF5555`) — metric worsened
- **Near-zero** (abs < 0.005 or $50) → Gold (`TacticalGoldBrush`) — neutral
- **For EAC**: logic is inverted (positive delta = bad, because EAC increased)

Uses a `ConverterParameter` string (`"Normal"` or `"Inverted"`) to handle the EAC flip.

---

### XAML — Redesigned Impact Bar

#### [MODIFY] [SimulationView.xaml](file:///d:/Project%20Management%20Application/Merged/ResourceAllocation/WpfResourceGantt/ProjectManagement/Features/Simulation/SimulationView.xaml)

Replace the current **Section 2: Impact Metrics Bar** (lines 132-156, the 3-column layout) with a new **6-column grid**:

```
┌──────────┬──────────┬──────────┬──────────┬──────────┬──────────┐
│    SV    │    CV    │   SPI    │   CPI    │   EAC    │   TCPI   │
├──────────┼──────────┼──────────┼──────────┼──────────┼──────────┤
│ Live: $X │ Live: $X │ Live: X  │ Live: X  │ Live: $X │ Live: X  │
│ Sim:  $X │ Sim:  $X │ Sim:  X  │ Sim:  X  │ Sim:  $X │ Sim:  X  │
│ Δ: $X    │ Δ: $X    │ Δ: 0.XX  │ Δ: 0.XX  │ Δ: $X    │          │
└──────────┴──────────┴──────────┴──────────┴──────────┴──────────┘
```

Each column is a styled card (`Border` with `CornerRadius`) containing:
1. **Header** label (e.g., "SV", "CPI")
2. **Live** value (white text)
3. **Simulated** value (gold text)
4. **Delta** value (color-coded via `EvmDeltaColorConverter`)

**TCPI column special treatment:**
- If `SimTotalTcpi > 1.10`, show a **warning banner** below: *"⚠ Recovery requires >10% efficiency gain"*
- If `SimTotalTcpi` is infinity, show *"BUDGET CONSUMED"* in red

**Converter registration** — add to `UserControl.Resources`:
```xml
<local:EvmDeltaColorConverter x:Key="EvmDeltaColorConverter"/>
```

> [!NOTE]
> The converter namespace will be `xmlns:converters="clr-namespace:WpfResourceGantt.ProjectManagement.Converters"` instead of `local:`, since the converter lives in a different folder. The key stays the same.

---

## Verification Plan

### Automated Tests
- **Build**: `dotnet build` — confirms no compilation errors from the new properties, converter, and XAML bindings.

### Manual Verification
> [!IMPORTANT]
> Since this is a WPF desktop app with no headless test harness, verification requires launching the application.

1. Launch the app
2. Click **PLAYGROUND** in the Ribbon to enter the Temporal Sandbox
3. **Verify the Impact Bar** now shows 6 metric columns: SV, CV, SPI, CPI, EAC, TCPI
4. Verify all 6 show **Live** values (white), **Simulated** values (gold), and **Δ** values
5. Select a leaf task → drag a progress dot **down** → verify:
   - SV and CV deltas turn **red** (negative)
   - SPI and CPI deltas turn **red** (below 1.0)
   - EAC delta turns **red** (estimate increased)
6. If TCPI > 1.10, verify the warning banner appears below the TCPI card
7. Click **Reset Sandbox** → verify all metrics return to baseline (Δ = 0)
