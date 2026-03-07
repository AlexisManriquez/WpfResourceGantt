using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WpfResourceGantt.ProjectManagement.Models
{
    /// <summary>
    /// A frozen, point-in-time record of EVM metrics for a SubProject (Level 2)
    /// captured at the end of a reporting week (Sunday).
    ///
    /// PURPOSE:
    ///   Provides the time-series data needed for the EVM S-Curve.
    ///   Once a snapshot is locked by a PM, the data is immutable —
    ///   giving reproducible, auditable metrics for customer reports.
    ///
    /// BUSINESS RULES:
    ///   - One snapshot per SubProject per week (unique constraint on SubProjectId + WeekEndingDate)
    ///   - WeekEndingDate is always a Sunday
    ///   - All values are CUMULATIVE (not period-only) to match DoD S-curve format
    ///   - ACWP = cumulative SMTS hours × rate as of week end
    ///   - IsLocked = true means PM has reviewed — record cannot be overwritten
    /// </summary>
    public class EvmWeeklySnapshot
    {
        [Key]
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// Foreign key to the SubProject WorkBreakdownItem (Level 2).
        /// Snapshots are always taken at SubProject level.
        /// </summary>
        [JsonPropertyName("subProjectId")]
        public string? SubProjectId { get; set; }

        /// <summary>
        /// The Sunday that ends the reporting week.
        /// Business rule enforced in DataService.GetWeekEndingDate().
        /// </summary>
        [JsonPropertyName("weekEndingDate")]
        public DateTime WeekEndingDate { get; set; }

        // ── Cumulative EVM Values (stored for report reproducibility) ────────

        /// <summary>Budget at Completion as of this snapshot (from baseline).</summary>
        [JsonPropertyName("bac")]
        public decimal BAC { get; set; }

        /// <summary>Planned Value — cumulative BCWS as of WeekEndingDate.</summary>
        [JsonPropertyName("bcws")]
        public double BCWS { get; set; }

        /// <summary>Earned Value — cumulative BCWP as of WeekEndingDate.</summary>
        [JsonPropertyName("bcwp")]
        public double BCWP { get; set; }

        /// <summary>
        /// Actual Cost — cumulative ACWP from SMTS imports up to WeekEndingDate.
        /// This is the total dollars charged, never manually entered.
        /// </summary>
        [JsonPropertyName("acwp")]
        public double ACWP { get; set; }

        // ── Derived Indices (stored to avoid re-computation on report load) ──

        /// <summary>Schedule Performance Index = BCWP / BCWS. Stored at snapshot time.</summary>
        [JsonPropertyName("spi")]
        public double SPI { get; set; }

        /// <summary>Cost Performance Index = BCWP / ACWP. Stored at snapshot time.</summary>
        [JsonPropertyName("cpi")]
        public double CPI { get; set; }

        /// <summary>Physical percent complete at time of snapshot (0.0 – 1.0).</summary>
        [JsonPropertyName("progress")]
        public double Progress { get; set; }

        // ── Audit Fields ─────────────────────────────────────────────────────

        /// <summary>UTC timestamp when this snapshot was created or last updated.</summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>User ID of the PM who triggered the snapshot.</summary>
        [JsonPropertyName("createdByUserId")]
        public string? CreatedByUserId { get; set; }

        /// <summary>
        /// When true, this snapshot has been reviewed and locked by a PM.
        /// Locked snapshots cannot be overwritten by subsequent Close Week operations.
        /// </summary>
        [JsonPropertyName("isLocked")]
        public bool IsLocked { get; set; }
    }
}
