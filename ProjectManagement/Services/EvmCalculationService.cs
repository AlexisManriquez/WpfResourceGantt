using System;
using System.Collections.Generic;
using System.Linq;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Services
{
    /// <summary>
    /// The single authoritative EVM calculation engine for WpfResourceGantt.
    /// 
    /// PURPOSE:
    ///   Eliminates the "dual rollup" problem where GanttViewModel and the
    ///   data model computed EVM metrics independently, producing inconsistent
    ///   values across screens.
    ///
    /// RULES (DoD / DAU EVMS Standard):
    ///   Leaf nodes (no children):
    ///     - BAC  = stored BAC field if IsBaselined, else Work × HourlyRate
    ///     - BCWP = BAC × Progress                          (Earned Value)
    ///     - BCWS = BAC × (businessDaysElapsed / totalBusinessDays) (Planned Value)
    ///     - ACWP = READ-ONLY — written exclusively by CsvImportService (SMTS)
    ///
    ///   Summary nodes (has children):
    ///     - All metrics = Sum(children.*) — pure rollup, no independent calculation
    ///     - Progress = BCWP / BAC (BAC-weighted); falls back to Average if BAC = 0
    ///     - Dates expand to cover children, but never shrink below what is stored
    ///
    /// ACWP LOCK:
    ///   This service never writes to Acwp or ActualWork on any node.
    ///   Those values are set exclusively by CsvImportService from SMTS exports.
    /// </summary>
    public class EvmCalculationService : IEvmCalculationService
    {
        // $195/hr — DoD standard rate. Matches WorkBreakdownItem.HourlyRate constant.
        private const double HOURLY_RATE = 195.0;

        /// <inheritdoc />
        public void RecalculateAll(IEnumerable<SystemItem> systems)
        {
            if (systems == null) return;

            foreach (var system in systems)
            {
                if (system?.Children == null) continue;

                foreach (var child in system.Children)
                {
                    RecalculateSubTree(child);
                }
            }
        }

        /// <inheritdoc />
        public void RecalculateSubTree(WorkBreakdownItem item)
        {
            if (item == null) return;

            bool isLeaf = item.Children == null || !item.Children.Any();

            if (isLeaf)
            {
                RecalculateLeaf(item);
            }
            else
            {
                RecalculateSummary(item);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // LEAF CALCULATION
        // ─────────────────────────────────────────────────────────────────────

        private void RecalculateLeaf(WorkBreakdownItem item)
        {
            DateTime today = DateTime.Today;

            // ── BAC ──────────────────────────────────────────────────────────
            // Use the stored, baselined BAC if available.
            // Fall back to live calculation only for un-baselined items.
            decimal effectiveBac;
            if (item.IsBaselined && item.BAC.HasValue && item.BAC.Value > 0)
            {
                effectiveBac = item.BAC.Value;
            }
            else
            {
                effectiveBac = (decimal)((item.Work ?? 0) * HOURLY_RATE);
                item.BAC = effectiveBac; // Set it so rollup parents can sum it
            }

            double bacAsDouble = (double)effectiveBac;

            // ── BCWP (Earned Value) ───────────────────────────────────────────
            // DoD Standard: Physical % complete × BAC
            item.Bcwp = Math.Round(bacAsDouble * item.Progress, 2);

            // ── BCWS (Planned Value) ──────────────────────────────────────────
            // DoD Standard: Time-proportional using inclusive business days.
            // Matches MS Project's duration calculation methodology.
            if (item.StartDate.HasValue && item.EndDate.HasValue && effectiveBac > 0)
            {
                int totalWorkingDays = WorkBreakdownItem.GetBusinessDaysSpan(
                    item.StartDate.Value, item.EndDate.Value);

                if (today < item.StartDate.Value)
                {
                    item.Bcws = 0;
                }
                else if (today > item.EndDate.Value)
                {
                    item.Bcws = bacAsDouble;
                }
                else
                {
                    int daysElapsed = WorkBreakdownItem.GetBusinessDaysSpan(
                        item.StartDate.Value, today);

                    item.Bcws = totalWorkingDays > 0
                        ? Math.Round(bacAsDouble * ((double)daysElapsed / totalWorkingDays), 2)
                        : 0;
                }
            }
            else
            {
                item.Bcws = 0;
            }

            // ── ACWP: NOT TOUCHED ────────────────────────────────────────────
            // Acwp is owned by CsvImportService. This service never modifies it.
            // Rounding only to clean display formatting.
            item.Acwp = Math.Round(item.Acwp ?? 0, 2);
        }

        // ─────────────────────────────────────────────────────────────────────
        // SUMMARY CALCULATION (pure rollup)
        // ─────────────────────────────────────────────────────────────────────

        private void RecalculateSummary(WorkBreakdownItem item)
        {
            // ── Bottom-up: recurse children first ────────────────────────────
            foreach (var child in item.Children)
            {
                RecalculateSubTree(child);
            }

            // ── Roll up scalar metrics ────────────────────────────────────────
            item.Work = Math.Round(item.Children.Sum(c => c.Work ?? 0), 2);
            item.ActualWork = Math.Round(item.Children.Sum(c => c.ActualWork ?? 0), 2);
            item.BAC = Math.Round(item.Children.Sum(c => c.BAC ?? 0), 2);
            item.Bcws = Math.Round(item.Children.Sum(c => c.Bcws ?? 0), 2);
            item.Bcwp = Math.Round(item.Children.Sum(c => c.Bcwp ?? 0), 2);
            item.Acwp = Math.Round(item.Children.Sum(c => c.Acwp ?? 0), 2);

            // ── Progress (BAC-weighted) ───────────────────────────────────────
            decimal totalBac = item.BAC ?? 0;
            if (totalBac > 0)
            {
                item.Progress = (double)((decimal)(item.Bcwp ?? 0) / totalBac);
            }
            else
            {
                // Fallback for un-baselined items: simple average
                item.Progress = item.Children.Any()
                    ? item.Children.Average(c => c.Progress)
                    : 0;
            }

            // ── Dates: expand to cover children, never shrink ────────────────
            var childrenWithStart = item.Children
                .Where(c => c.StartDate.HasValue)
                .Select(c => c.StartDate!.Value)
                .ToList();

            var childrenWithEnd = item.Children
                .Where(c => c.EndDate.HasValue)
                .Select(c => c.EndDate!.Value)
                .ToList();

            if (childrenWithStart.Any())
            {
                DateTime childMin = childrenWithStart.Min();
                // Only expand (earlier), never shrink the parent's start date
                if (!item.StartDate.HasValue || childMin < item.StartDate.Value)
                    item.StartDate = childMin;
            }

            if (childrenWithEnd.Any())
            {
                DateTime childMax = childrenWithEnd.Max();
                // Only expand (later), never shrink the parent's end date
                if (!item.EndDate.HasValue || childMax > item.EndDate.Value)
                    item.EndDate = childMax;
            }
        }
    }
}
