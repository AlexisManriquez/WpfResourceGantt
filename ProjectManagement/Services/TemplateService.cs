using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WpfResourceGantt.ProjectManagement.Data;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.Models.Templates;

namespace WpfResourceGantt.ProjectManagement.Services
{
    public class TemplateService
    {
        private readonly DataService _dataService;

        public TemplateService(DataService dataService)
        {
            _dataService = dataService;
        }

        /// <summary>
        /// Gets all available templates for selection in the UI.
        /// </summary>
        public async Task<List<ProjectTemplate>> GetAllTemplatesAsync()
        {
            using (var context = new AppDbContext())
            {
                return await context.ProjectTemplates
                    .Include(t => t.Gates)
                        .ThenInclude(g => g.Blocks)
                            .ThenInclude(b => b.Items)
                    .AsNoTracking()
                    .ToListAsync();
            }
        }

        /// <summary>
        /// Applies a template structure to a Sub-Project.
        /// Creates Gates as WorkBreakdownItems, and—when gates have Tasks—
        /// creates leaf-level WorkBreakdownItems under them for GAO-compliant scheduling.
        /// </summary>
        public async Task ApplyTemplateAsync(int templateId, WorkBreakdownItem targetSubProject)
        {
            if (targetSubProject == null) return;

            ProjectTemplate template;
            using (var context = new AppDbContext())
            {
                template = await context.ProjectTemplates
                    .Include(t => t.Gates)
                        .ThenInclude(g => g.Blocks)
                            .ThenInclude(b => b.Items)
                    .Include(t => t.Gates)
                        .ThenInclude(g => g.Tasks)
                    .FirstOrDefaultAsync(t => t.Id == templateId);
            }

            if (template == null) return;

            // Ensure children list is initialized
            if (targetSubProject.Children == null)
                targetSubProject.Children = new List<WorkBreakdownItem>();

            // Determine starting sequence for new Gates
            int gateSequence = targetSubProject.Children.Count;

            // Phase 1: Build a map of template-relative indices → generated IDs
            // so task-level predecessors (e.g., "0.0", "1.2") can resolve to actual IDs.
            // Key = "GateSortOrder" for gate-level, or "GateSortOrder.TaskSortOrder" for task-level
            var idMap = new Dictionary<string, string>();

            // Phase 2: Create all gates and tasks
            foreach (var tGate in template.Gates.OrderBy(g => g.SortOrder))
            {
                string gateId = $"{targetSubProject.Id}|{gateSequence}";
                idMap[$"{tGate.SortOrder}"] = gateId;

                bool hasTasks = tGate.Tasks != null && tGate.Tasks.Any();
                int durationDays = tGate.DurationDays > 0 ? tGate.DurationDays : 30;

                // Resolve gate-level predecessors (e.g., "0" → actual gate ID)
                var gatePredIds = string.IsNullOrWhiteSpace(tGate.Predecessors)
                    ? new List<string>()
                    : tGate.Predecessors.Split(',')
                        .Select(p => p.Trim())
                        .Where(p => idMap.ContainsKey(p))
                        .Select(p => idMap[p])
                        .ToList();

                var newGate = new WorkBreakdownItem
                {
                    Id = gateId,
                    WbsValue = "TEMP",
                    Sequence = gateSequence,
                    Name = tGate.Name,
                    Level = targetSubProject.Level + 1,
                    Status = WorkItemStatus.Active,
                    ScheduleMode = targetSubProject.ScheduleMode,
                    DurationDays = hasTasks ? 0 : durationDays, // Summary gates get 0; engine rolls up
                    Predecessors = !hasTasks && gatePredIds.Any() ? string.Join(", ", gatePredIds) : null,
                    StartDate = targetSubProject.StartDate ?? DateTime.Today,
                    EndDate = WorkBreakdownItem.AddBusinessDays(targetSubProject.StartDate ?? DateTime.Today, durationDays),
                    Work = hasTasks ? 0 : durationDays * 8,
                    Progress = 0,
                    Children = new List<WorkBreakdownItem>(),
                    ProgressBlocks = new List<ProgressBlock>(),
                    Assignments = new List<ResourceAssignment>(),
                    ProgressHistory = new List<ProgressHistoryItem>
                    {
                        new ProgressHistoryItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            Date = DateTime.Today,
                            ActualProgress = 0,
                            ExpectedProgress = 0
                        }
                    }
                };

                // === Path A: Gate has child Tasks → becomes a Summary Node ===
                if (hasTasks)
                {
                    int taskSequence = 0;
                    foreach (var tTask in tGate.Tasks.OrderBy(t => t.SortOrder))
                    {
                        string taskId = $"{gateId}|{taskSequence}_{Guid.NewGuid().ToString("N").Substring(0, 4)}";
                        idMap[$"{tGate.SortOrder}.{tTask.SortOrder}"] = taskId;

                        // Resolve task-level predecessors (format: "GateSortOrder.TaskSortOrder")
                        var taskPredIds = new List<string>();
                        if (!string.IsNullOrWhiteSpace(tTask.Predecessors))
                        {
                            foreach (var predRef in tTask.Predecessors.Split(',').Select(p => p.Trim()))
                            {
                                if (idMap.ContainsKey(predRef))
                                    taskPredIds.Add(idMap[predRef]);
                            }
                        }

                        int taskDuration = tTask.DurationDays > 0 ? tTask.DurationDays : 10;
                        var childTask = new WorkBreakdownItem
                        {
                            Id = taskId,
                            WbsValue = "TEMP",
                            Sequence = taskSequence,
                            Name = tTask.Name,
                            Level = newGate.Level + 1,
                            Status = WorkItemStatus.Active,
                            ScheduleMode = targetSubProject.ScheduleMode,
                            ItemType = tTask.ItemType,
                            DurationDays = taskDuration,
                            Predecessors = taskPredIds.Any() ? string.Join(", ", taskPredIds) : null,
                            StartDate = newGate.StartDate,
                            EndDate = WorkBreakdownItem.AddBusinessDays(newGate.StartDate ?? DateTime.Today, taskDuration),
                            Work = tTask.WorkHours,
                            Progress = 0,
                            Children = new List<WorkBreakdownItem>(),
                            ProgressBlocks = new List<ProgressBlock>(),
                            Assignments = new List<ResourceAssignment>(),
                            ProgressHistory = new List<ProgressHistoryItem>
                            {
                                new ProgressHistoryItem
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Date = DateTime.Today,
                                    ActualProgress = 0,
                                    ExpectedProgress = 0
                                }
                            }
                        };

                        newGate.Children.Add(childTask);
                        taskSequence++;
                    }
                }
                else
                {
                    // === Path B: Gate has no tasks → stays a flat leaf with ProgressBlocks ===
                    int blockSequence = 0;
                    foreach (var tBlock in tGate.Blocks.OrderBy(b => b.SortOrder))
                    {
                        var newBlock = new ProgressBlock
                        {
                            Id = "PB-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                            Name = tBlock.Name,
                            Sequence = blockSequence,
                            Items = new List<ProgressItem>()
                        };

                        var sortedTemplateItems = tBlock.Items.OrderBy(i => i.SortOrder).ToList();
                        for (int i = 0; i < sortedTemplateItems.Count; i++)
                        {
                            var tItem = sortedTemplateItems[i];
                            newBlock.Items.Add(new ProgressItem
                            {
                                Id = Guid.NewGuid().ToString(),
                                Name = tItem.Description,
                                IsCompleted = false,
                                Sequence = i
                            });
                        }

                        newGate.ProgressBlocks.Add(newBlock);
                        blockSequence++;
                    }
                }

                targetSubProject.Children.Add(newGate);
                gateSequence++;
            }

            // Recalculate rollup for the sub-project to reflect new children
            targetSubProject.RecalculateRollup();

            // 1. Extract System ID (The first part of the ID: e.g., "SYS-ABCD" from "SYS-ABCD|0|1")
            string systemId = targetSubProject.Id.Split('|')[0];

            // 2. Tell DataService to generate correct 1.1.2.1 style numbers for the whole tree
            _dataService.RegenerateWbsValues(systemId);
            _dataService.MarkSystemDirty(systemId);

            // Persist everything to the database
            await _dataService.SaveDataAsync();
        }
    }
}
