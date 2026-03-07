using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using WpfResourceGantt.ProjectManagement.Models;

#if ENABLE_MSPROJECT
using MSProject = Microsoft.Office.Interop.MSProject;
#endif

namespace WpfResourceGantt.ProjectManagement.Services
{
    public class MppExportService
    {
        public void ExportMppFile(string mppFullPath, SystemItem system, IEnumerable<User> users)
        {
#if ENABLE_MSPROJECT
            MSProject.Application projectApp = null;
            try
            {
                projectApp = new MSProject.Application { Visible = false };

                // 1. STOP AUTO-CALCULATION
                projectApp.Calculation = MSProject.PjCalculation.pjManual;

                // 2. DISABLE "Actual Costs are Calculated by Project" (CV Fix)
                // We use positional arguments because named parameters are failing in your Interop version.
                // Parameter Order: OptionsCalculation(Automatic, AutoCalcCosts, CalcActualCosts, ...)
                // We pass Type.Missing for the first two, and FALSE for the 3rd.
                projectApp.OptionsCalculation(
                    Type.Missing,
                    Type.Missing,
                    false // CalcActualCosts = false
                );

                projectApp.FileNew();
                MSProject.Project project = projectApp.ActiveProject;

                // 3. FORCE STANDARD GOVT CALENDAR (8h/day, 40h/week)
                project.HoursPerDay = 8;
                project.HoursPerWeek = 40;
                project.DaysPerMonth = 20;

                // 4. SET EVM METHOD TO % COMPLETE
                // This ensures Earned Value is based on physical work %, not duration %.
                try
                {
                    project.DefaultEarnedValueMethod = MSProject.PjEarnedValueMethod.pjPhysicalPercentComplete;
                }
                catch { } // Swallow error if property missing in old interop

                DateTime sysStart = system.Children?.Where(c => c.StartDate.HasValue).Select(c => c.StartDate.Value).DefaultIfEmpty(DateTime.Today).Min() ?? DateTime.Today;
                DateTime sysEnd = system.Children?.Where(c => c.EndDate.HasValue).Select(c => c.EndDate.Value).DefaultIfEmpty(DateTime.Today.AddDays(1)).Max() ?? DateTime.Today.AddDays(1);

                project.ProjectStart = sysStart;

                // Build Resource Dictionary
                var userDict = users?.ToDictionary(u => u.Id, u => u.Name) ?? new Dictionary<string, string>();
                if (users != null)
                {
                    foreach (var user in users)
                    {
                        var r = project.Resources.Add(user.Name);
                        // Standard Rate used for Planned Value (BCWS)
                        r.StandardRate = 195;
                    }
                }

                // --- ROOT SYSTEM ---
                var sysTask = project.Tasks.Add(system.Name);
                sysTask.OutlineLevel = 1;
                dynamic dSys = sysTask;
                dSys.Manual = true; // Manual scheduling allows us to override dates

                // Set Dates
                sysTask.Start = sysStart;
                sysTask.Finish = sysEnd;
                try { dSys.Constraint = 7; dSys.ConstraintDate = sysEnd; } catch { }

                // Recursively build the tree
                if (system.Children != null)
                {
                    foreach (var child in system.Children)
                    {
                        ExportWorkBreakdownItem(project, child, 2, userDict);
                    }
                }

                // --- FINAL CALCULATIONS & EVM SETUP ---

                // Turn Auto-Calc back on so MSP computes the rollup fields
                projectApp.Calculation = MSProject.PjCalculation.pjAutomatic;
                projectApp.CalculateAll();

                // 5. SET STATUS DATE
                // SV and CV are time-based. MSP must know "Today's Date" to calculate variances.
                project.StatusDate = DateTime.Today;

                // 6. SAVE BASELINE
                // SV cannot be calculated without a Baseline (The "Plan").
                // This snapshots the current Start/Finish/Cost into Baseline 0.
                try { projectApp.BaselineSave(); } catch { }

                projectApp.FileSaveAs(Name: mppFullPath);
            }
            finally
            {
                if (projectApp != null)
                {
                    projectApp.Quit(MSProject.PjSaveType.pjDoNotSave);
                    Marshal.ReleaseComObject(projectApp);
                }
            }
#endif
        }

#if ENABLE_MSPROJECT
        private void ExportWorkBreakdownItem(MSProject.Project project, WorkBreakdownItem item, short outlineLevel, Dictionary<string, string> userDict)
        {
            var task = project.Tasks.Add(item.Name);
            task.OutlineLevel = outlineLevel;
            dynamic dTask = task;
            dTask.Manual = true;

            // 1. SET TASK TYPE TO FIXED WORK (Type 2)
            try { dTask.Type = 2; } catch { }

            bool isLeaf = item.Children == null || item.Children.Count == 0;

            // 2. SET DATES
            if (item.StartDate.HasValue) task.Start = item.StartDate.Value;
            if (item.EndDate.HasValue) task.Finish = item.EndDate.Value;

            // 3. SET WORK (Planned Hours)
            if (isLeaf && item.Work.HasValue)
            {
                task.Work = item.Work.Value * 60;
            }

            // 4. ASSIGN RESOURCES
            bool hasResource = false;
            if (item.Assignments != null && item.Assignments.Any())
            {
                var names = item.Assignments.Select(a => userDict.GetValueOrDefault(a.DeveloperId)).Where(n => n != null);
                task.ResourceNames = string.Join(",", names);
                hasResource = true;
            }
            else if (!string.IsNullOrEmpty(item.AssignedDeveloperId))
            {
                task.ResourceNames = userDict.GetValueOrDefault(item.AssignedDeveloperId) ?? "";
                hasResource = true;
            }

            // 5. SET FIXED COST FOR UNASSIGNED EVM
            if (isLeaf && !hasResource && item.Work.HasValue)
            {
                try { dTask.FixedCost = item.Work.Value * 195.0; } catch { }
            }

            // 6. SET ACTUALS & EVM PROGRESS
            // This order is critical to keep Hours correct
            if (isLeaf)
            {
                // A. Set Actual Work FIRST. This locks in the 197 hours.
                if (item.ActualWork.HasValue)
                {
                    task.ActualWork = item.ActualWork.Value * 60;
                }

                // B. Set PHYSICAL Percent Complete.
                // This drives EVM (BCWP, SV, CV) without changing Actual Work.
                task.PhysicalPercentComplete = (int)(item.Progress * 100);

                // C. Do NOT set task.PercentComplete if we have Actuals.
                // Leaving this alone lets MSP calculate the "Work % Complete" (11.5%)
                // which accurately reflects that 197 is 11.5% of the total hours.
                if (!item.ActualWork.HasValue)
                {
                    task.PercentComplete = item.Progress * 100;
                }

                // D. Re-Assert Constraint
                if (item.EndDate.HasValue)
                {
                    task.Finish = item.EndDate.Value;
                    try { dTask.Constraint = 7; dTask.ConstraintDate = item.EndDate.Value; } catch { }
                }
            }
            else
            {
                // FOR SUMMARIES:
                // We set Physical Percent Complete so the EVM rollup is correct.
                task.PhysicalPercentComplete = (int)(item.Progress * 100);
            }

            if (item.Children != null)
            {
                foreach (var child in item.Children)
                    ExportWorkBreakdownItem(project, child, (short)(outlineLevel + 1), userDict);
            }
        }
#endif
    }
}
