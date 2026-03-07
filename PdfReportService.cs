using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WpfResourceGantt.ProjectManagement.Models;
using TaskStatus = WpfResourceGantt.ProjectManagement.Models.TaskStatus;

namespace WpfResourceGantt
{
    public static class PdfReportService
    {
        public static void GenerateReport(string filePath, IEnumerable<ResourcePerson> resources, IEnumerable<ResourceTask> unassignedTasks)
        {
            var today = DateTime.Today;
            var dateIn90Days = today.AddDays(90);
            var totalStaff = resources.Count();

            // 1. FORECAST NUMBERS
            var availNow = CountAvailable(resources, today);
            var avail30 = CountAvailable(resources, today.AddDays(30));
            var avail60 = CountAvailable(resources, today.AddDays(60));
            var avail90 = CountAvailable(resources, today.AddDays(90));
            var avail180 = CountAvailable(resources, today.AddDays(180));

            // 2. DETAILED AVAILABILITY LISTS
            var availablePeopleDetails = resources
                .Select(r => new AvailabilityRow { Person = r, StatusDetail = GetAvailabilityDetail(r, today) })
                .Where(x => x.StatusDetail.Type != AvailabilityType.Busy)
                .OrderBy(x => x.StatusDetail.Type).ThenBy(x => x.Person.Section).ToList();

            var availablePeople90Details = resources
                .Select(r => new AvailabilityRow { Person = r, StatusDetail = GetAvailabilityDetail(r, dateIn90Days) })
                .Where(x => x.StatusDetail.Type != AvailabilityType.Busy)
                .OrderBy(x => x.StatusDetail.Type).ThenBy(x => x.Person.Section).ToList();

            // 3. UNASSIGNED ANALYSIS

            // A. KPI Count (Active & Today)
            var activeUnassignedCount = unassignedTasks.Count(t =>
                t.Status == TaskStatus.InWork &&
                today >= t.StartDate && today <= t.EndDate);

            // B. Future Pipeline (Starts > Today OR Status = Future)
            var futureTasks = unassignedTasks
                .Where(t => t.Status == TaskStatus.Future || t.StartDate > today)
                .OrderBy(t => t.StartDate)
                .ToList();

            // C. Current Backlog (InWork/OnHold AND Started <= Today)
            // This is the new list you requested
            var currentBacklogTasks = unassignedTasks
                .Where(t => (t.Status == TaskStatus.InWork || t.Status == TaskStatus.OnHold) && t.StartDate <= today)
                .OrderBy(t => t.Status) // Active first, then OnHold (Assuming enum order InWork=0, OnHold=1)
                .ThenBy(t => t.StartDate)
                .ToList();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    // HEADER
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Resource Allocation Report").FontSize(18).SemiBold().FontColor(Colors.Blue.Medium);
                            col.Item().Text($"Generated: {DateTime.Now:f}").FontSize(9).FontColor(Colors.Grey.Medium);
                        });
                    });

                    // CONTENT
                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        // 1. KPI CARDS
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Component(new KpiCard("Total Staff", totalStaff.ToString(), Colors.Blue.Lighten4));
                            row.Spacing(10);
                            row.RelativeItem().Component(new KpiCard("Available Now", availNow.ToString(), Colors.Green.Lighten4));
                            row.Spacing(10);
                            row.RelativeItem().Component(new KpiCard("Active Backlog", activeUnassignedCount.ToString(), Colors.Red.Lighten4));
                            row.Spacing(10);
                            row.RelativeItem().Component(new KpiCard("Future Backlog", futureTasks.Count.ToString(), Colors.Orange.Lighten4));
                        });

                        col.Item().PaddingVertical(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        // 2. FORECAST TABLE
                        col.Item().PaddingBottom(5).Text("Capacity Forecast").FontSize(12).SemiBold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                            table.Header(h =>
                            {
                                h.Cell().Element(CellStyle).Text("Current");
                                h.Cell().Element(CellStyle).Text("+30 Days");
                                h.Cell().Element(CellStyle).Text("+60 Days");
                                h.Cell().Element(CellStyle).Text("+90 Days");
                                h.Cell().Element(CellStyle).Text("+180 Days");
                            });
                            table.Cell().Element(ValueStyle).Text($"{availNow} / {totalStaff}");
                            table.Cell().Element(ValueStyle).Text($"{avail30} / {totalStaff}");
                            table.Cell().Element(ValueStyle).Text($"{avail60} / {totalStaff}");
                            table.Cell().Element(ValueStyle).Text($"{avail90} / {totalStaff}");
                            table.Cell().Element(ValueStyle).Text($"{avail180} / {totalStaff}");
                        });

                        col.Item().PaddingVertical(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        // 3. AVAILABLE ROSTERS
                        col.Item().PaddingBottom(2).Text("Current Available Personnel").FontSize(12).SemiBold();
                        if (availablePeopleDetails.Any()) ComposeRosterTable(col, availablePeopleDetails);
                        else col.Item().Text("No personnel currently available.").Italic().FontColor(Colors.Grey.Medium);

                        col.Item().PaddingVertical(10);

                        col.Item().PaddingBottom(2).Text($"Personnel Available in 90 Days ({dateIn90Days:MMM dd})").FontSize(12).SemiBold();
                        if (availablePeople90Details.Any()) ComposeRosterTable(col, availablePeople90Details);
                        else col.Item().Text("No personnel available in 90 days.").Italic().FontColor(Colors.Grey.Medium);

                        col.Item().PaddingVertical(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        // 4. UNASSIGNED PROJECTS (NEW SECTION)
                        // --- 4. UNASSIGNED PROJECTS (UPDATED COLUMNS) ---
                        col.Item().PaddingBottom(5).Text("Current Unassigned Backlog (Active & On Hold)").FontSize(12).SemiBold();

                        if (currentBacklogTasks.Any())
                        {
                            col.Item().Table(table =>
                            {
                                // 1. UPDATE COLUMNS: Added one more column for the split date
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(2); // Task Name (Wider)
                                    c.ConstantColumn(80); // Status
                                    c.RelativeColumn();  // Start Date
                                    c.RelativeColumn();  // End Date
                                    c.RelativeColumn();  // Office
                                });

                                // 2. UPDATE HEADER: Split Timeline into Start/End
                                table.Header(h =>
                                {
                                    h.Cell().Text("Task Name").SemiBold().FontColor(Colors.Grey.Darken2);
                                    h.Cell().Text("Status").SemiBold().FontColor(Colors.Grey.Darken2);
                                    h.Cell().Text("Start").SemiBold().FontColor(Colors.Grey.Darken2); // NEW
                                    h.Cell().Text("End").SemiBold().FontColor(Colors.Grey.Darken2);   // NEW
                                    h.Cell().Text("Office").SemiBold().FontColor(Colors.Grey.Darken2);
                                });

                                foreach (var task in currentBacklogTasks)
                                {
                                    // Name
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3).Text(task.Name).FontSize(9);

                                    // Status
                                    var statusColor = task.Status == TaskStatus.InWork ? Colors.Red.Darken1 : Colors.Grey.Darken1;
                                    var statusText = task.Status == TaskStatus.InWork ? "Active" : "On Hold";
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3).Text(statusText).FontColor(statusColor).FontSize(9).SemiBold();

                                    // 3. UPDATE ROWS: Separate Cells for Dates
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3).Text($"{task.StartDate:MM/dd/yyyy}").FontSize(9);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3).Text($"{task.EndDate:MM/dd/yyyy}").FontSize(9);

                                    // Office
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3).Text(task.ProjectOfficeSymbol ?? "-").FontSize(9);
                                }
                            });
                        }
                        else
                        {
                            col.Item().Text("No unassigned projects in current backlog.").Italic().FontColor(Colors.Grey.Medium);
                        }

                        col.Item().PaddingVertical(15);

                        // 5. FUTURE PIPELINE
                        col.Item().PaddingBottom(5).Text("Future Pipeline (Starts > Today)").FontSize(12).SemiBold();

                        if (futureTasks.Any())
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); });
                                table.Header(h =>
                                {
                                    h.Cell().Text("Task Name").SemiBold().FontColor(Colors.Grey.Darken2);
                                    h.Cell().Text("Planned Start").SemiBold().FontColor(Colors.Grey.Darken2);
                                    h.Cell().Text("Office").SemiBold().FontColor(Colors.Grey.Darken2);
                                });
                                foreach (var task in futureTasks.Take(15))
                                {
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3).Text(task.Name).FontSize(9);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3).Text($"{task.StartDate:MMM dd, yyyy}").FontSize(9);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3).Text(task.ProjectOfficeSymbol ?? "-").FontSize(9);
                                }
                            });
                            if (futureTasks.Count > 15) col.Item().PaddingTop(5).Text($"... and {futureTasks.Count - 15} more.").Italic().FontSize(9).FontColor(Colors.Grey.Medium);
                        }
                        else
                        {
                            col.Item().Text("No future pipeline items found.").Italic().FontColor(Colors.Grey.Medium);
                        }
                    });

                    page.Footer().AlignCenter().Text(x => { x.Span("Page "); x.CurrentPageNumber(); });
                });
            })
            .GeneratePdf(filePath);

            try { new Process { StartInfo = new ProcessStartInfo(filePath) { UseShellExecute = true } }.Start(); } catch { }
        }

        // --- KEEP EXISTING HELPERS & CLASSES (ComposeRosterTable, GetAvailabilityDetail, KpiCard, etc) ---
        // (Copy from previous correct response)

        // Ensure you include the helper classes from the previous solution
        private static void ComposeRosterTable(ColumnDescriptor col, IEnumerable<AvailabilityRow> peopleList)
        {
            // ... (Keep existing implementation) ...
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.ConstantColumn(70); c.ConstantColumn(70); c.RelativeColumn(); c.ConstantColumn(100); });
                table.Header(h =>
                {
                    h.Cell().Text("Section").SemiBold().FontColor(Colors.Grey.Darken2);
                    h.Cell().Text("Role").SemiBold().FontColor(Colors.Grey.Darken2);
                    h.Cell().Text("Name").SemiBold().FontColor(Colors.Grey.Darken2);
                    h.Cell().Text("Status").SemiBold().FontColor(Colors.Grey.Darken2);
                });
                foreach (var item in peopleList)
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3).Text(item.Person.Section ?? "-");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3).Text(item.Person.Role ?? "-");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3).Text(item.Person.Name);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3).Text(item.StatusDetail.Label).FontColor(item.StatusDetail.Color).SemiBold().FontSize(9);
                }
            });
        }

        private static IContainer CellStyle(IContainer container) => container.Background(Colors.Grey.Lighten3).Padding(5).AlignCenter();
        private static IContainer ValueStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8).AlignCenter();

        private enum AvailabilityType { Busy, Unassigned, NonCritical, OnHold }

        private class AvailabilityRow
        {
            public ResourcePerson Person { get; set; }
            public (AvailabilityType Type, string Label, string Color) StatusDetail { get; set; }
        }

        private static (AvailabilityType Type, string Label, string Color) GetAvailabilityDetail(ResourcePerson p, DateTime date)
        {
            var activeTasks = p.Tasks.Where(t => t.StartDate <= date && t.EndDate >= date).ToList();

            if (!activeTasks.Any()) return (AvailabilityType.Unassigned, "Unassigned", Colors.Green.Darken2);
            if (activeTasks.Any(t => t.Status == TaskStatus.InWork && t.AssignmentRole == AssignmentRole.Primary)) return (AvailabilityType.Busy, "Busy", Colors.Blue.Medium);
            if (activeTasks.Any(t => t.Status == TaskStatus.InWork && t.AssignmentRole == AssignmentRole.Secondary)) return (AvailabilityType.NonCritical, "Non-Critical Task", Colors.Purple.Darken2);
            return (AvailabilityType.OnHold, "Project On Hold", Colors.Grey.Darken1);
        }

        private static int CountAvailable(IEnumerable<ResourcePerson> resources, DateTime date)
        {
            return resources.Count(r => GetAvailabilityDetail(r, date).Type != AvailabilityType.Busy);
        }

        private class KpiCard : IComponent
        {
            private string Title { get; }
            private string Value { get; }
            private string ColorHex { get; }
            public KpiCard(string title, string value, string colorHex) { Title = title; Value = value; ColorHex = colorHex; }
            public void Compose(IContainer container)
            {
                container.Background(ColorHex).Padding(10).Column(column =>
                {
                    column.Item().Text(Value).FontSize(24).Bold().AlignRight();
                    column.Item().Text(Title).FontSize(10).FontColor(Colors.Grey.Darken3).AlignRight();
                });
            }
        }
    }
}
