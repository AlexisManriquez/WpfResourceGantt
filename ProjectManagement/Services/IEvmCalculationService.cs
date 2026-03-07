using System.Collections.Generic;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Services
{
    /// <summary>
    /// The single authoritative source for all EVM metric calculations.
    /// Every screen in the application must read EVM values that were
    /// produced by this service — never from a parallel calculation.
    /// </summary>
    public interface IEvmCalculationService
    {
        /// <summary>
        /// Recalculates and SETS all EVM fields (BAC, BCWS, BCWP, ACWP, Progress,
        /// dates, SV, CV) on every node in the given system collection.
        /// This is the primary entry point — call after any data load or change.
        /// </summary>
        void RecalculateAll(IEnumerable<SystemItem> systems);

        /// <summary>
        /// Recalculates a single WorkBreakdownItem and all its descendants.
        /// Use for targeted refresh without reloading the full tree.
        /// </summary>
        void RecalculateSubTree(WorkBreakdownItem item);
    }
}
