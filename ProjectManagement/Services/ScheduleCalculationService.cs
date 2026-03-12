using System;
using System.Collections.Generic;
using System.Linq;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Services
{
    public class ScheduleCalculationService : IScheduleCalculationService
    {
        public void CalculateSchedule(IEnumerable<SystemItem> systems, DateTime? statusDate = null)
        {
            DateTime today = statusDate ?? DateTime.Today;
            var allLeafItems = GetAllLeafItems(systems).ToList();
            if (!allLeafItems.Any()) return;

            var manualItemIds = new HashSet<string>();
            foreach (var system in systems)
            {
                if (system.Children == null) continue;
                // Level 1 children are the "Projects" that hold the ScheduleMode
                foreach (var project in system.Children.Where(p => p.ScheduleMode == ScheduleMode.Manual))
                {
                    foreach (var leaf in GetLeaves(project))
                    {
                        manualItemIds.Add(leaf.Id);
                    }
                }
            }

            // 1. Create Lookup Maps
            // Support both internal ID and WBS code for predecessors
            var idMap = allLeafItems.ToDictionary(i => i.Id, i => i);
            var wbsMap = allLeafItems.Where(i => !string.IsNullOrEmpty(i.WbsValue))
                                    .ToDictionary(i => i.WbsValue, i => i);

            // 2. FORWARD PASS: Calculate Early Start (ES) and Early Finish (EF)
            // Initial pass: Set dates for items without predecessors or with SNET
            foreach (var item in allLeafItems)
            {
                if (manualItemIds.Contains(item.Id)) continue; // Skip overriding manual dates

                // MILESTONE ENFORCEMENT: Milestones are zero-duration, point-in-time items.
                if (item.IsMilestone)
                {
                    item.DurationDays = 0;
                    if (!item.StartDate.HasValue)
                        item.StartDate = item.StartNoEarlierThan ?? today;
                    item.EndDate = item.StartDate; // Point-in-time: Start == End
                    continue;
                }

                var predecessors = PredecessorParser.Parse(item.Predecessors);
                if (!predecessors.Any())
                {
                    // No predecessors: respect existing dates (e.g. from MPP import)
                    // Only default to today if the task has no start date at all
                    if (!item.StartDate.HasValue)
                        item.StartDate = item.StartNoEarlierThan ?? today;
                    else if (item.StartNoEarlierThan.HasValue && item.StartNoEarlierThan > item.StartDate)
                        item.StartDate = item.StartNoEarlierThan; // SNET constraint wins
                }
                else
                {
                    // For logic-driven items, start with SNET or existing date, then push forward
                    item.StartDate = item.StartNoEarlierThan ?? item.StartDate ?? today;
                }
                item.EndDate = WorkBreakdownItem.AddBusinessDays(item.StartDate.Value, item.DurationDays);
            }

            // Iterative Forward Pass (handles network ripple)
            // Simplified approach: iterate a few times or use a queue to handle dependencies
            bool changed = true;
            int maxIterations = 1000; // Guard against circular deps
            int iterations = 0;

            while (changed && iterations < maxIterations)
            {
                changed = false;
                iterations++;

                foreach (var item in allLeafItems)
                {
                    if (manualItemIds.Contains(item.Id)) continue; // Skip overriding manual dates

                    var deps = PredecessorParser.Parse(item.Predecessors);
                    if (!deps.Any()) continue;

                    DateTime earliestStart = item.StartNoEarlierThan ?? item.StartDate ?? today;

                    foreach (var dep in deps)
                    {
                        WorkBreakdownItem pred = null;
                        if (wbsMap.ContainsKey(dep.PredecessorId)) pred = wbsMap[dep.PredecessorId];
                        else if (idMap.ContainsKey(dep.PredecessorId)) pred = idMap[dep.PredecessorId];

                        if (pred != null)
                        {
                            DateTime calculationSource;
                            switch (dep.Type)
                            {
                                case DependencyType.SS: calculationSource = pred.StartDate ?? today; break;
                                case DependencyType.FF: calculationSource = (pred.EndDate ?? today).AddDays(-item.DurationDays); break;
                                case DependencyType.SF: calculationSource = pred.EndDate ?? today; break;
                                case DependencyType.FS:
                                default:
                                    // FS: Start = Finish of Pred + Lag
                                    // Finish of Pred is pred.EndDate. Start of this one is Finish + Lag
                                    calculationSource = WorkBreakdownItem.AddBusinessDays(pred.EndDate ?? today, 1);
                                    break;
                            }


                            DateTime potentialStart = WorkBreakdownItem.AddBusinessDays(calculationSource, dep.LagDays);
                            if (potentialStart > earliestStart) earliestStart = potentialStart;
                        }
                    }

                    if (earliestStart != DateTime.MinValue && item.StartDate != earliestStart)
                    {
                        item.StartDate = earliestStart;
                        item.EndDate = WorkBreakdownItem.AddBusinessDays(item.StartDate.Value, item.DurationDays);
                        changed = true;
                    }
                }
            }

            // 3. BACKWARD PASS: Calculate Late Start (LS) and Late Finish (LF)
            // CHANGE: Create a lookup for finish dates per project to support multiple critical paths
            var projectFinishes = new Dictionary<string, DateTime>();
            var itemsByProject = allLeafItems.GroupBy(i => GetProjectRootId(i.Id));

            foreach (var group in itemsByProject)
            {
                var rootId = group.Key;
                var groupFinish = group.Max(i => i.EndDate ?? today);
                projectFinishes[rootId] = groupFinish;

                foreach (var item in group)
                {
                    item.LateFinish = groupFinish;
                    item.LateStart = WorkBreakdownItem.AddBusinessDays(item.LateFinish.Value, -item.DurationDays);
                }
            }



            changed = true;
            iterations = 0;
            while (changed && iterations < maxIterations)
            {
                changed = false;
                iterations++;

                foreach (var item in allLeafItems)
                {
                    // Find successors (items that have THIS item as a predecessor)
                    var successors = allLeafItems.Where(s => IsPredecessorOf(item, s, wbsMap, idMap)).ToList();

                    if (!successors.Any())
                    {
                        // No successors? LF remains project finish or specific deadline if added later
                        continue;
                    }

                    string rootId = GetProjectRootId(item.Id);
                    DateTime latestFinish = projectFinishes.ContainsKey(rootId) ? projectFinishes[rootId] : today;

                    foreach (var succ in successors)
                    {
                        var deps = PredecessorParser.Parse(succ.Predecessors);
                        var dep = deps.FirstOrDefault(d => d.PredecessorId == item.WbsValue || d.PredecessorId == item.Id);

                        if (dep != null)
                        {
                            DateTime potentialLF;
                            switch (dep.Type)
                            {
                                case DependencyType.SS: potentialLF = WorkBreakdownItem.AddBusinessDays(succ.LateStart ?? today, item.DurationDays); break;
                                case DependencyType.FF: potentialLF = succ.LateFinish ?? today; break;
                                case DependencyType.SF: potentialLF = succ.LateStart ?? today; break;
                                case DependencyType.FS:
                                default:
                                    potentialLF = WorkBreakdownItem.AddBusinessDays(succ.LateStart ?? today, -1);
                                    break;
                            }

                            DateTime calculatedLF = WorkBreakdownItem.AddBusinessDays(potentialLF, -dep.LagDays);
                            if (calculatedLF < latestFinish) latestFinish = calculatedLF;
                        }
                    }

                    if (item.LateFinish != latestFinish)
                    {
                        item.LateFinish = latestFinish;
                        item.LateStart = WorkBreakdownItem.AddBusinessDays(item.LateFinish.Value, -item.DurationDays);
                        changed = true;
                    }
                }
            }

            // 4. CALCULATE FLOAT AND CRITICAL PATH
            foreach (var item in allLeafItems)
            {
                if (item.LateStart.HasValue && item.StartDate.HasValue)
                {
                    int span = WorkBreakdownItem.GetBusinessDaysSpan(item.StartDate.Value, item.LateStart.Value);
                    item.TotalFloat = item.LateStart.Value >= item.StartDate.Value ? span - 1 : -(WorkBreakdownItem.GetBusinessDaysSpan(item.LateStart.Value, item.StartDate.Value) - 1);
                    item.IsCritical = item.TotalFloat <= 0;
                }
            }

            // 5. ROLLUP FLOAT TO SUMMARY NODES
            // A parent's float = Min(children's float). A parent is critical if ANY child is critical.
            foreach (var system in systems)
            {
                if (system.Children != null)
                {
                    foreach (var child in system.Children)
                    {
                        RollupFloatRecursive(child);
                    }
                }
            }
        }

        private bool IsPredecessorOf(WorkBreakdownItem potPred, WorkBreakdownItem item, Dictionary<string, WorkBreakdownItem> wbsMap, Dictionary<string, WorkBreakdownItem> idMap)
        {
            var deps = PredecessorParser.Parse(item.Predecessors);
            return deps.Any(d => d.PredecessorId == potPred.WbsValue || d.PredecessorId == potPred.Id);
        }
        // --- NEW HELPER METHOD ---
        // extracts "SYS-XXX|0" (Level 1 Project ID) from any child ID
        private string GetProjectRootId(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            var parts = id.Split('|');
            if (parts.Length >= 2)
            {
                // Return SystemID + Sequence (e.g. "SYS-GUID|0")
                return $"{parts[0]}|{parts[1]}";
            }
            return id; // Fallback for root items
        }
        /// <summary>
        /// Recursively rolls up TotalFloat and IsCritical from leaf nodes to summary nodes.
        /// A summary's float = Min(children's float). A summary is critical if ANY child is critical.
        /// </summary>
        private void RollupFloatRecursive(WorkBreakdownItem item)
        {
            if (item.Children == null || !item.Children.Any())
                return; // Leaf node — already calculated above

            // First, recurse into children so their values are up to date
            foreach (var child in item.Children)
            {
                RollupFloatRecursive(child);
            }

            // Now rollup: parent float = minimum of all children's float
            item.TotalFloat = item.Children.Min(c => c.TotalFloat);
            item.IsCritical = item.Children.Any(c => c.IsCritical);
        }

        private IEnumerable<WorkBreakdownItem> GetAllLeafItems(IEnumerable<SystemItem> systems)
        {
            foreach (var system in systems)
            {
                if (system.Children == null) continue;
                foreach (var child in system.Children)
                {
                    foreach (var leaf in GetLeaves(child))
                    {
                        yield return leaf;
                    }
                }
            }
        }

        private IEnumerable<WorkBreakdownItem> GetLeaves(WorkBreakdownItem item)
        {
            if (item.Children == null || !item.Children.Any())
            {
                yield return item;
            }
            else
            {
                foreach (var child in item.Children)
                {
                    foreach (var leaf in GetLeaves(child))
                    {
                        yield return leaf;
                    }
                }
            }
        }
    }
}
