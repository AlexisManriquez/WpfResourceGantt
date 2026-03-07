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

            // 1. Flatten hierarchy to find tasks
            foreach (var system in _dataService.GetSystemsForUser(new User { Role = Role.FlightChief }))
            {
                CollectLeaves(system.Children, allWorkItems);
            }

            return await Task.Run(async () =>
            {
                try
                {
                    // --- PASS 1: SCAN FILE ---
                    var csvRows = new List<string[]>();
                    using (TextFieldParser parser = new TextFieldParser(filePath))
                    {
                        parser.TextFieldType = FieldType.Delimited;
                        parser.SetDelimiters(",");

                        if (!parser.EndOfData) parser.ReadFields(); // Header
                        if (!parser.EndOfData) parser.ReadFields(); // Empty row

                        while (!parser.EndOfData)
                        {
                            string[] fields = parser.ReadFields();
                            if (fields != null && fields.Length >= 21) csvRows.Add(fields);
                        }
                    }

                    result.RecordsProcessed = csvRows.Count;
                    var affectedTasks = new HashSet<WorkBreakdownItem>();

                    // --- PASS 2: UPDATE ACTUAL WORK ---
                    foreach (var fields in csvRows)
                    {
                        string dateStr = fields[2];
                        string hoursStr = fields[7];
                        string taskName = fields[20];
                        string resource = fields[0];

                        if (!double.TryParse(hoursStr, out double hoursToAdd)) continue;
                        if (!DateTime.TryParse(dateStr, out DateTime entryDate)) continue;

                        var task = allWorkItems.FirstOrDefault(w => w.Name.Equals(taskName, StringComparison.OrdinalIgnoreCase));
                        if (task == null) continue;

                        affectedTasks.Add(task);

                        // Update Task Totals
                        task.ActualWork = (task.ActualWork ?? 0) + hoursToAdd;

                        // Update ACWP (Cost) — SMTS hours × user hourly rate.
                        // ACWP is the ONLY location where this property is written.
                        // SMTS is the system of record for all actual costs.
                        var user = _dataService.AllUsers.FirstOrDefault(u => u.Name.Contains(resource.Split(',')[0]));
                        decimal rate = user?.HourlyRate ?? 195m;
                        task.Acwp = (task.Acwp ?? 0) + (hoursToAdd * (double)rate);

                        // Stamp the audit trail: record which file and when it was imported.
                        // This is visible to PMs as a tooltip in the Gantt ACWP column.
                        task.LastAcwpImportDate = DateTime.Now;
                        task.LastAcwpImportSource = Path.GetFileName(filePath);

                        // Update/Create History Entry
                        if (task.ProgressHistory == null) task.ProgressHistory = new List<ProgressHistoryItem>();
                        var history = task.ProgressHistory.FirstOrDefault(h => h.Date.Date == entryDate.Date);

                        if (history != null)
                        {
                            history.ActualWork += hoursToAdd;
                        }
                        else
                        {
                            task.ProgressHistory.Add(new ProgressHistoryItem
                            {
                                Id = Guid.NewGuid().ToString(),
                                Date = entryDate.Date,
                                ActualWork = task.ActualWork ?? 0
                            });
                        }
                        result.MatchesFound++;
                    }

                    // --- PASS 3: RE-CALCULATE ENTIRE HISTORY PROGRESS ---
                    foreach (var task in affectedTasks)
                    {
                        if (task.ProgressHistory == null || !task.ProgressHistory.Any()) continue;

                        // Find the LATEST date ever recorded for this task (across all 3 CSVs)
                        DateTime anchorDate = task.ProgressHistory.Max(h => h.Date);

                        // Sort history by date to ensure ActualWork is cumulative correctly
                        var sortedHistory = task.ProgressHistory.OrderBy(h => h.Date).ToList();
                        double rollingWorkSum = 0;

                        foreach (var h in sortedHistory)
                        {
                            // If your CSV hours are daily, we need to ensure ActualWork in history 
                            // is a running total (cumulative).
                            // If you already have cumulative logic, this just reinforces it.

                            // RE-CALCULATE PROGRESS BASED ON THE NEW ANCHOR (Jan 31)
                            h.ActualProgress = CalculateInterpolatedProgress(task, h.Date, anchorDate);
                        }
                    }

                    // 4. Save to Database
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
                    // 1. READ CSV
                    var rawRows = new List<string[]>();
                    using (TextFieldParser parser = new TextFieldParser(filePath))
                    {
                        parser.TextFieldType = FieldType.Delimited;
                        parser.SetDelimiters(",");
                        if (!parser.EndOfData) parser.ReadFields(); // Skip Header row

                        while (!parser.EndOfData)
                        {
                            string[] fields = parser.ReadFields();

                            // FIX: Stricter validation for "Empty Comma Rows"
                            if (fields != null && fields.Length >= 2)
                            {
                                string taskHeading = fields[fields.Length - 1]; // Last Column
                                string taskName = fields[fields.Length - 2];    // 2nd to Last

                                // ONLY add the row if both Heading and Name have actual text
                                if (!string.IsNullOrWhiteSpace(taskHeading) && !string.IsNullOrWhiteSpace(taskName))
                                {
                                    rawRows.Add(fields);
                                }
                            }
                        }
                    }

                    // 2. IDENTIFY OR CREATE SYSTEM
                    // ... (Logic remains same as previous step) ...
                    SystemItem systemItem;
                    if (!string.IsNullOrEmpty(systemId))
                    {
                        systemItem = _dataService.GetSystemById(systemId);
                    }
                    else
                    {
                        // FIX: Calculate the next available numeric WBS value instead of using .Count
                        int nextWbsNumber = 1;
                        var existingSystems = _dataService.AllSystems;
                        if (existingSystems != null && existingSystems.Any())
                        {
                            var numericValues = existingSystems
                                .Select(s => int.TryParse(s.WbsValue, out int val) ? val : 0);

                            if (numericValues.Any())
                            {
                                nextWbsNumber = numericValues.Max() + 1;
                            }
                        }

                        systemItem = new SystemItem
                        {
                            Id = $"SYS-{Guid.NewGuid().ToString().Substring(0, 8)}",
                            Name = $"{newSystemNumber} {newSystemName}".Trim(),
                            WbsValue = nextWbsNumber.ToString(), // Root WBS Value is now safe
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

                    // 4. GROUP DATA
                    // The grouping now works on a cleaned list with no blank rows
                    var groupedData = rawRows
                        .GroupBy(r => r[r.Length - 1])
                        .ToList();

                    int subProjSeq = 0;

                    foreach (var group in groupedData)
                    {
                        string subProjString = group.Key;

                        // Create SubProject (Level 2)
                        var subProjectId = $"{projectId}|{subProjSeq}";
                        var subProjectItem = new WorkBreakdownItem
                        {
                            Id = subProjectId,
                            Name = subProjString.Trim(),
                            Level = 2,
                            ItemType = WorkItemType.Summary,
                            Status = WorkItemStatus.Active,
                            Sequence = subProjSeq
                        };
                        projectItem.Children.Add(subProjectItem);

                        // PROCESS LEAVES
                        var leafGroups = group.GroupBy(r => r[r.Length - 2]);
                        int leafSeq = 0;

                        foreach (var leafGroup in leafGroups)
                        {
                            string rawLeafName = leafGroup.Key ?? "";
                            string leafName = rawLeafName;

                            // NEW: Remove the first segment before the first space
                            // Input: "6.3.4.1 191-003 APY1/2 Project Support"
                            // Output: "191-003 APY1/2 Project Support"
                            int firstSpaceIndex = rawLeafName.IndexOf(' ');
                            if (firstSpaceIndex > 0 && firstSpaceIndex < rawLeafName.Length - 1)
                            {
                                leafName = rawLeafName.Substring(firstSpaceIndex + 1).Trim();
                            }
                            DateTime minDate = DateTime.MaxValue;
                            DateTime maxDate = DateTime.MinValue;
                            double totalAcwp = 0;
                            double totalActualHours = 0;

                            foreach (var row in leafGroup)
                            {
                                if (row.Length > 2 && DateTime.TryParse(row[2], out DateTime dt))
                                {
                                    if (dt < minDate) minDate = dt;
                                    if (dt > maxDate) maxDate = dt;
                                }

                                if (row.Length > 7 && double.TryParse(row[7], out double hours))
                                {
                                    totalActualHours += hours;
                                    string resource = row[0];
                                    var user = _dataService.AllUsers.FirstOrDefault(u => u.Name.Contains(resource.Split(',')[0]));
                                    decimal rate = user?.HourlyRate ?? 195m;
                                    totalAcwp += hours * (double)rate;
                                }
                            }

                            if (minDate == DateTime.MaxValue) minDate = DateTime.Today;
                            if (maxDate == DateTime.MinValue) maxDate = DateTime.Today;

                            var leafItem = new WorkBreakdownItem
                            {
                                Id = $"{subProjectId}|{leafSeq}",
                                Name = leafName,
                                Level = 3,
                                ItemType = WorkItemType.Leaf,
                                Status = WorkItemStatus.Active,
                                Sequence = leafSeq,
                                StartDate = minDate,
                                EndDate = maxDate,
                                Work = 1.0,
                                ActualWork = totalActualHours,
                                Acwp = totalAcwp,
                                LastAcwpImportDate = DateTime.Now,
                                LastAcwpImportSource = Path.GetFileName(filePath),
                                ScheduleMode = ScheduleMode.Manual
                            };

                            leafItem.DurationDays = WorkBreakdownItem.GetBusinessDaysSpan(leafItem.StartDate.Value, leafItem.EndDate.Value);

                            subProjectItem.Children.Add(leafItem);
                            leafSeq++;
                        }

                        subProjSeq++;
                    }

                    // 5. SAVE & APPLY STRUCTURAL WBS
                    systemItem.RecalculateRollup();
                    _dataService.RegenerateWbsValues(systemItem.Id);
                    await _dataService.SaveDataAsync();

                    result.RecordsProcessed = rawRows.Count;
                    result.MatchesFound = rawRows.Count;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(ex.Message);
                }
                return result;
            });
        }
    }
}
