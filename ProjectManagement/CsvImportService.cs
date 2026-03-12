using Microsoft.VisualBasic.FileIO; // Built-in .NET parser
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement
{
    public class CsvImportResult
    {
        public int RecordsProcessed { get; set; }
        public int MatchesFound { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class CsvImportService
    {
        private readonly DataService _dataService;

        public CsvImportService(DataService dataService)
        {
            _dataService = dataService;
        }
        public async Task<CsvImportResult> ImportHoursFromCsvAsync(string filePath)
        {
            var result = new CsvImportResult();
            var allWorkItems = new List<WorkBreakdownItem>();

            // 1. Flatten hierarchy to find all potential tasks
            foreach (var system in _dataService.GetSystemsForUser(new User { Role = Role.FlightChief }))
            {
                CollectLeaves(system.Children, allWorkItems);
            }

            return await Task.Run(async () =>
            {
                try
                {
                    // --- PASS 1: SCAN AND GROUP CSV DATA INTO MEMORY ---
                    var csvData = new List<(string TaskName, DateTime Date, double Hours, string Resource)>();

                    using (TextFieldParser parser = new TextFieldParser(filePath))
                    {
                        parser.TextFieldType = FieldType.Delimited;
                        parser.SetDelimiters(",");

                        if (!parser.EndOfData) parser.ReadFields(); // Skip Header
                        if (!parser.EndOfData) parser.ReadFields(); // Skip Empty row

                        while (!parser.EndOfData)
                        {
                            string[] fields = parser.ReadFields();
                            if (fields != null && fields.Length >= 21)
                            {
                                if (double.TryParse(fields[7], out double h) && DateTime.TryParse(fields[2], out DateTime d))
                                {
                                    csvData.Add((fields[20], d.Date, h, fields[0]));
                                }
                            }
                        }
                    }

                    result.RecordsProcessed = csvData.Count;
                    var affectedTasks = new HashSet<WorkBreakdownItem>();

                    // --- PASS 2: WIPE AND REPLACE ---
                    // Group by Task Name so we can clean each task once
                    var dataByTask = csvData.GroupBy(x => x.TaskName);

                    foreach (var taskGroup in dataByTask)
                    {
                        var task = allWorkItems.FirstOrDefault(w => w.Name.Equals(taskGroup.Key, StringComparison.OrdinalIgnoreCase));
                        if (task == null) continue;

                        affectedTasks.Add(task);

                        // NUCLEAR RESET: Clear all existing history for this task.
                        // This ensures dates from previous imports that are NOT in this CSV are deleted.
                        task.ProgressHistory?.Clear();
                        if (task.ProgressHistory == null) task.ProgressHistory = new List<ProgressHistoryItem>();

                        // Group entries for this task by Date to consolidate resource hours
                        var dataByDate = taskGroup.GroupBy(x => x.Date);

                        foreach (var dateGroup in dataByDate)
                        {
                            DateTime entryDate = dateGroup.Key;
                            double totalHoursForThisDay = dateGroup.Sum(x => x.Hours);

                            // Add as a fresh entry
                            task.ProgressHistory.Add(new ProgressHistoryItem
                            {
                                Id = Guid.NewGuid().ToString(), // New ID ensures fresh DB record
                                Date = entryDate,
                                ActualWork = totalHoursForThisDay
                            });

                            result.MatchesFound++;
                        }

                        // Update the main Task property by summing the fresh history
                        task.ActualWork = task.ProgressHistory.Sum(h => h.ActualWork);

                        // Calculate ACWP using per-resource hourly rates (mirrors ReconstructProjectFromCsvAsync logic).
                        // This is a full overwrite so re-importing the same file is idempotent.
                        double totalAcwp = 0;
                        foreach (var entry in taskGroup)
                        {
                            var user = _dataService.AllUsers.FirstOrDefault(u =>
                                u.Name.Contains(entry.Resource.Split(',')[0].Trim(),
                                                StringComparison.OrdinalIgnoreCase));
                            double rate = (double)(user?.HourlyRate ?? 195m);
                            totalAcwp += entry.Hours * rate;
                        }
                        task.Acwp = totalAcwp;

                        // Audit Trail
                        task.LastAcwpImportDate = DateTime.Now;
                        task.LastAcwpImportSource = Path.GetFileName(filePath);
                    }

                    // --- PASS 3: RE-CALCULATE INTERPOLATED PROGRESS ---
                    foreach (var task in affectedTasks)
                    {
                        if (task.ProgressHistory == null || !task.ProgressHistory.Any()) continue;

                        DateTime anchorDate = task.ProgressHistory.Max(h => h.Date);
                        var sortedHistory = task.ProgressHistory.OrderBy(h => h.Date).ToList();

                        foreach (var h in sortedHistory)
                        {
                            h.ActualProgress = CalculateInterpolatedProgress(task, h.Date, anchorDate);
                        }
                    }

                    // 4. SAVE — mark each affected system dirty so only touched rows get UPDATE/RowVersion checks
                    foreach (var sysId in affectedTasks.Select(t => t.Id.Split('|')[0]).Distinct())
                        _dataService.MarkSystemDirty(sysId);
                    await _dataService.SaveDataAsync();
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Import Failed: {ex.Message}");
                }

                return result;
            });
        }
        private string NormalizeName(string csvName)
        {
            // Input: "Thomas, Kenneth"
            // Output: "Kenneth Thomas"
            if (string.IsNullOrWhiteSpace(csvName)) return "";
            var parts = csvName.Split(',');
            if (parts.Length == 2)
            {
                return $"{parts[1].Trim()} {parts[0].Trim()}";
            }
            return csvName; // Fallback
        }

        private void CollectLeaves(IEnumerable<WorkBreakdownItem> items, List<WorkBreakdownItem> flatList)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item.Children == null || !item.Children.Any())
                {
                    flatList.Add(item);
                }
                else
                {
                    CollectLeaves(item.Children, flatList);
                }
            }
        }

        /// <summary>
        /// Calculates estimated progress for a past date based on linear interpolation.
        /// Logic: (Time Passed / Total Duration So Far) * Current Progress
        /// </summary>
        private double CalculateInterpolatedProgress(WorkBreakdownItem task, DateTime historyDate, DateTime dataCutoff)
        {
            if (task.StartDate == null) return 0.0;
            DateTime start = task.StartDate.Value.Date;

            if (historyDate <= start) return 0.0;
            if (historyDate >= dataCutoff) return task.Progress;

            double totalDays = (dataCutoff - start).TotalDays;
            double daysPassed = (historyDate - start).TotalDays;

            return totalDays > 0 ? task.Progress * (daysPassed / totalDays) : task.Progress;
        }

        // NEW: Helper to scan file just for the "000-000-0000" pattern
        // ... existing code ...

        public (string SystemNum, string ProjectNum) ScanCsvForNumbers(string filePath)
        {
            try
            {
                using (TextFieldParser parser = new TextFieldParser(filePath))
                {
                    parser.TextFieldType = FieldType.Delimited;
                    parser.SetDelimiters(",");

                    if (!parser.EndOfData) parser.ReadFields(); // Skip Header

                    while (!parser.EndOfData)
                    {
                        string[] fields = parser.ReadFields();
                        // FIX: Ensure the row isn't just commas
                        if (fields == null || fields.Length < 2) continue;

                        string candidate = fields[fields.Length - 1];

                        // CRITICAL: Ensure the Task Heading column isn't blank
                        if (!string.IsNullOrWhiteSpace(candidate))
                        {
                            var match = Regex.Match(candidate, @"^(\d{3})-(\d{3})-(\d{4})");
                            if (match.Success)
                            {
                                return (match.Groups[1].Value, match.Groups[2].Value);
                            }
                        }
                    }
                }
            }
            catch { /* Ignore scan errors */ }
            return ("000", "000");
        }

        public async Task<CsvImportResult> ReconstructProjectFromCsvAsync(
    string filePath,
    string systemId,
    string newSystemName,
    string newSystemNumber,
    string projectName,
    string projectNumber)
        {
            var result = new CsvImportResult();

            return await Task.Run(async () =>
            {
                try
                {
                    // 1. READ CSV & CLEAN DATA
                    var rawRows = new List<string[]>();
                    using (TextFieldParser parser = new TextFieldParser(filePath))
                    {
                        parser.TextFieldType = FieldType.Delimited;
                        parser.SetDelimiters(",");
                        if (!parser.EndOfData) parser.ReadFields(); // Skip Header row

                        while (!parser.EndOfData)
                        {
                            string[] fields = parser.ReadFields();
                            // Validation: Ensure row has content and the specific Task/SubProj columns aren't empty
                            if (fields != null && fields.Length >= 2)
                            {
                                string taskHeading = fields[fields.Length - 1]; // SubProject column
                                string taskName = fields[fields.Length - 2];    // Task Name column
                                if (!string.IsNullOrWhiteSpace(taskHeading) && !string.IsNullOrWhiteSpace(taskName))
                                {
                                    rawRows.Add(fields);
                                }
                            }
                        }
                    }

                    // 2. IDENTIFY OR CREATE SYSTEM
                    SystemItem systemItem;
                    if (!string.IsNullOrEmpty(systemId))
                    {
                        systemItem = _dataService.GetSystemById(systemId);
                    }
                    else
                    {
                        // Calculate next WBS number for a new System
                        int nextWbsNumber = 1;
                        var existingSystems = _dataService.AllSystems;
                        if (existingSystems != null && existingSystems.Any())
                        {
                            var numericValues = existingSystems.Select(s => int.TryParse(s.WbsValue, out int val) ? val : 0);
                            if (numericValues.Any()) nextWbsNumber = numericValues.Max() + 1;
                        }

                        systemItem = new SystemItem
                        {
                            Id = $"SYS-{Guid.NewGuid().ToString().Substring(0, 8)}",
                            Name = $"{newSystemNumber} {newSystemName}".Trim(),
                            WbsValue = nextWbsNumber.ToString(),
                            Status = WorkItemStatus.Active
                        };
                        _dataService.AddSystem(systemItem);
                    }

                    // 3. CREATE PROJECT (Level 1)
                    var projectId = $"{systemItem.Id}|{systemItem.Children.Count}";
                    var projectItem = new WorkBreakdownItem
                    {
                        Id = projectId,
                        Name = $"{newSystemNumber}-{projectNumber} {projectName}".Trim(),
                        Level = 1,
                        ItemType = WorkItemType.Summary,
                        Status = WorkItemStatus.Active,
                        ScheduleMode = ScheduleMode.Manual
                    };
                    systemItem.Children.Add(projectItem);

                    // 4. GROUP DATA BY SUBPROJECT (Heading)
                    var groupedBySubProject = rawRows.GroupBy(r => r[r.Length - 1]).ToList();
                    int subProjSeq = 0;

                    foreach (var subGroup in groupedBySubProject)
                    {
                        // Create SubProject (Level 2)
                        var subProjectItem = new WorkBreakdownItem
                        {
                            Id = $"{projectId}|{subProjSeq}",
                            Name = subGroup.Key.Trim(),
                            Level = 2,
                            ItemType = WorkItemType.Summary,
                            Status = WorkItemStatus.Active,
                            Sequence = subProjSeq,
                            ScheduleMode = ScheduleMode.Manual
                        };
                        projectItem.Children.Add(subProjectItem);

                        // GROUP BY TASK NAME (Leaf)
                        var leafGroups = subGroup.GroupBy(r => r[r.Length - 2]);
                        int leafSeq = 0;

                        foreach (var leafGroup in leafGroups)
                        {
                            string rawLeafName = leafGroup.Key ?? "";
                            string leafName = rawLeafName;

                            // Remove prefix (e.g., "1.2.3 ")
                            int firstSpaceIndex = rawLeafName.IndexOf(' ');
                            if (firstSpaceIndex > 0 && firstSpaceIndex < rawLeafName.Length - 1)
                                leafName = rawLeafName.Substring(firstSpaceIndex + 1).Trim();

                            var leafItem = new WorkBreakdownItem
                            {
                                Id = $"{subProjectItem.Id}|{leafSeq}",
                                Name = leafName,
                                Level = 3,
                                ItemType = WorkItemType.Leaf,
                                Status = WorkItemStatus.Active,
                                Sequence = leafSeq,
                                ScheduleMode = ScheduleMode.Manual,
                                ProgressHistory = new List<ProgressHistoryItem>(),
                                Progress = 0, // Starts at 0%
                                ActualWork = 0,
                                Acwp = 0
                            };

                            // --- RECONSTRUCT TOTALS AND CUMULATIVE HISTORY ---
                            var hoursByDate = new Dictionary<DateTime, double>();
                            DateTime minDate = DateTime.MaxValue;
                            DateTime maxDate = DateTime.MinValue;

                            foreach (var row in leafGroup)
                            {
                                if (row.Length > 7 && double.TryParse(row[7], out double hoursToAdd) &&
                                    row.Length > 2 && DateTime.TryParse(row[2], out DateTime entryDate))
                                {
                                    // 1. Group individual entries by date (to handle multiple resources on same day)
                                    if (hoursByDate.ContainsKey(entryDate.Date))
                                        hoursByDate[entryDate.Date] += hoursToAdd;
                                    else
                                        hoursByDate[entryDate.Date] = hoursToAdd;

                                    // 2. Update ACWP (Cost) using user rate logic
                                    string resource = row[0];
                                    var user = _dataService.AllUsers.FirstOrDefault(u => u.Name.Contains(resource.Split(',')[0]));
                                    decimal rate = user?.HourlyRate ?? 195m;
                                    leafItem.Acwp = (leafItem.Acwp ?? 0) + (hoursToAdd * (double)rate);

                                    // 3. Track Date Bounds
                                    if (entryDate < minDate) minDate = entryDate;
                                    if (entryDate > maxDate) maxDate = entryDate;
                                }
                            }

                            // 4. Create History as a RUNNING TOTAL (The "S-Curve")
                            double runningTotalHours = 0;
                            var sortedDates = hoursByDate.Keys.OrderBy(d => d).ToList();

                            foreach (var date in sortedDates)
                            {
                                runningTotalHours += hoursByDate[date];
                                leafItem.ProgressHistory.Add(new ProgressHistoryItem
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Date = date,
                                    ActualWork = runningTotalHours // Cumulative dot for the chart
                                });
                            }

                            // 5. Finalize Leaf Item Properties
                            leafItem.ActualWork = runningTotalHours;
                            leafItem.StartDate = minDate == DateTime.MaxValue ? DateTime.Today : minDate;
                            leafItem.EndDate = maxDate == DateTime.MinValue ? DateTime.Today : maxDate;
                            leafItem.DurationDays = WorkBreakdownItem.GetBusinessDaysSpan(leafItem.StartDate.Value, leafItem.EndDate.Value);
                            leafItem.LastAcwpImportDate = DateTime.Now;
                            leafItem.LastAcwpImportSource = Path.GetFileName(filePath);
                            leafItem.Work = 1.0; // Default placeholder for BAC calculation

                            // --- INTERPOLATE PROGRESS TREND ---
                            if (leafItem.ProgressHistory.Any())
                            {
                                DateTime anchorDate = leafItem.ProgressHistory.Max(h => h.Date);
                                foreach (var h in leafItem.ProgressHistory)
                                {
                                    // If leafItem.Progress is 0, this results in 0. 
                                    // If PM updates Progress later, this creates the curve.
                                    h.ActualProgress = CalculateInterpolatedProgress(leafItem, h.Date, anchorDate);
                                }
                            }

                            subProjectItem.Children.Add(leafItem);
                            leafSeq++;
                        }
                        subProjSeq++;
                    }

                    // 5. FINALIZATION: Roll up values and refresh WBS
                    systemItem.RecalculateRollup();
                    _dataService.RegenerateWbsValues(systemItem.Id);
                    _dataService.MarkSystemDirty(systemItem.Id);
                    await _dataService.SaveDataAsync();

                    result.RecordsProcessed = rawRows.Count;
                    result.MatchesFound = rawRows.Count;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Reconstruct Failed: {ex.Message}");
                }
                return result;
            });
        }
    }
}
