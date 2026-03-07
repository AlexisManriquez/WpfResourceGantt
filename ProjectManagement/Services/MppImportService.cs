using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using WpfResourceGantt.ProjectManagement.Models;
#if ENABLE_MSPROJECT
using MSProject = Microsoft.Office.Interop.MSProject;
#endif

namespace WpfResourceGantt.ProjectManagement.Services
{
    public class MppImportService
    {

        public List<WorkBreakdownItem> ParseMppFile(string mppFullPath, string projectManagerId)
        {
#if ENABLE_MSPROJECT
            if (!File.Exists(mppFullPath))
                throw new FileNotFoundException($"File not found at '{mppFullPath}'");

            MSProject.Application projectApp = null;
            try
            {
                projectApp = new MSProject.Application { Visible = false };
                projectApp.FileOpen(mppFullPath);
                MSProject.Project project = projectApp.ActiveProject;

                var importedProjects = new List<WorkBreakdownItem>();
                var levelTracker = new Dictionary<int, WorkBreakdownItem>();

                foreach (MSProject.Task task in project.Tasks)
                {
                    if (task == null || string.IsNullOrEmpty(task.Name)) continue;

                    int currentLevel = task.OutlineLevel;

                    // --- ALL LEVELS: Now map to WorkBreakdownItem ---
                    var newItem = new WorkBreakdownItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        WbsValue = task.WBS,
                        Name = task.Name,
                        Level = currentLevel, // 1 becomes Project, 2 becomes Sub-Project, etc.
                        StartDate = GetDate(task.Start),
                        EndDate = GetDate(task.Finish),
                        Work = ConvertMinutesToHours(task.Work),
                        ActualWork = 0.0,
                        Bcws = 0.0,
                        Bcwp = 0.0,
                        Acwp = 0.0,
                        Children = new List<WorkBreakdownItem>(),
                        ProgressHistory = new List<ProgressHistoryItem>()
                    };

                    if (!task.Summary)
                    {
                        newItem.Progress = task.PercentComplete / 100.0;
                        if (task.PercentComplete == 100)
                        {
                            newItem.ActualFinishDate = GetDate(task.ActualFinish);
                        }

                        if (newItem.StartDate.HasValue && newItem.EndDate.HasValue)
                        {
                            newItem.DurationDays = WorkBreakdownItem.GetBusinessDaysSpan(
                                newItem.StartDate.Value, newItem.EndDate.Value);
                        }
                    }

                    if (currentLevel == 1)
                    {
                        newItem.Sequence = importedProjects.Count;
                        importedProjects.Add(newItem);
                    }
                    else
                    {
                        // Hierarchy logic: Link to parent item
                        int parentLevel = currentLevel - 1;
                        if (levelTracker.TryGetValue(parentLevel, out var parentItem))
                        {
                            newItem.Sequence = parentItem.Children.Count;
                            parentItem.Children.Add(newItem);
                        }
                    }

                    levelTracker[currentLevel] = newItem;
                }

                // Post-Process: Recalculate Rollups
                foreach (var p in importedProjects)
                {
                    RollupImportedWorkItem(p);
                }

                return importedProjects;
            }
            finally
            {
                if (projectApp != null)
                {
                    projectApp.Quit(MSProject.PjSaveType.pjDoNotSave);
                    Marshal.ReleaseComObject(projectApp);
                }
            }
#else
            throw new NotSupportedException("MS Project import is disabled in this build configuration. To enable, set <UseMsProject>true</UseMsProject> in the .csproj file.");
#endif
        }
        // NEW: Specific method for the Root System
        private void RollupImportedWorkItem(WorkBreakdownItem item)
        {
            if (item.Children == null || !item.Children.Any()) return;

            foreach (var child in item.Children)
            {
                RollupImportedWorkItem(child);
            }

            item.Work = item.Children.Sum(c => c.Work ?? 0);

            // FIX: Include the item's own imported date in the comparison
            var childrenMin = item.Children.Where(c => c.StartDate.HasValue).Select(c => c.StartDate.Value).Min();
            var childrenMax = item.Children.Where(c => c.EndDate.HasValue).Select(c => c.EndDate.Value).Max();

            // The Parent Start is the earlier of its own date OR the children
            if (item.StartDate.HasValue)
                item.StartDate = item.StartDate < childrenMin ? item.StartDate : childrenMin;
            else
                item.StartDate = childrenMin;

            // The Parent End is the later of its own date (Feb 23) OR the children (Feb 12)
            if (item.EndDate.HasValue)
                item.EndDate = item.EndDate > childrenMax ? item.EndDate : childrenMax;
            else
                item.EndDate = childrenMax;
            if (item.StartDate.HasValue && item.EndDate.HasValue)
            {
                item.DurationDays = WorkBreakdownItem.GetBusinessDaysSpan(item.StartDate.Value, item.EndDate.Value);
            }
        }

        // Apply the same logic change to RollupImportedSystem()
        private void RollupImportedSystem(SystemItem system)
        {
            if (system.Children == null || !system.Children.Any()) return;
            foreach (var child in system.Children) RollupImportedWorkItem(child);

        }
        private DateTime? GetDate(object dateObject)
        {
            try
            {
                DateTime date = (DateTime)dateObject;
                return date.Year < 1990 ? (DateTime?)null : date;
            }
            catch { return null; }
        }

        private double ConvertMinutesToHours(object minutesObject)
        {
            if (minutesObject == null) return 0.0;
            return Convert.ToDouble(minutesObject) / 60.0;
        }
    }
}
