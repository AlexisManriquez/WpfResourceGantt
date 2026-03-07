using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfResourceGantt.ProjectManagement.Data;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.Models.Templates;

namespace WpfResourceGantt.ProjectManagement.data
{
    public static class TemplateSeeder
    {
        public static void SeedTemplates()
        {
            using (var context = new AppDbContext())
            {

                // 1.CLEAR EVERYTHING FIRST
                // Because of Cascade Delete, this one line clears all 4 template tables.
                if (context.ProjectTemplates.Any())
                {
                    context.ProjectTemplates.RemoveRange(context.ProjectTemplates);
                    context.SaveChanges();
                }
                var standardTemplate = new ProjectTemplate
                {
                    Name = "Default Template",
                    Description = "A standard 4-gate process (Design, Implementation, Acceptance Testing, and Delivery)",
                    Gates = new List<TemplateGate>
                    {
                        // GATE 1: Design
                        new TemplateGate
                        {
                            Name = "Design",
                            SortOrder = 0,
                            DurationDays = 0,
                            Predecessors = null, // First gate, no predecessors
                            Blocks = new List<TemplateProgressBlock>
                            {
                                new TemplateProgressBlock
                                {
                                    Name = "Develop TPSDD",
                                    SortOrder = 0,
                                    Items = new List<TemplateProgressItem>
                                    {
                                        new TemplateProgressItem { Description = "TPSDD Chapter 1", SortOrder = 0 },
                                        new TemplateProgressItem { Description = "TPSDD Chapter 2", SortOrder = 1 },
                                        new TemplateProgressItem { Description = "TPSDD Chapter 3", SortOrder = 2 },
                                        new TemplateProgressItem { Description = "TPSDD Chapter 4", SortOrder = 3 },
                                        new TemplateProgressItem { Description = "TPSDD Chapter 5", SortOrder = 4 },
                                        new TemplateProgressItem { Description = "TPSDD Chapter 6", SortOrder = 5 },
                                        new TemplateProgressItem { Description = "TPSDD Appendix A", SortOrder = 6 },
                                        new TemplateProgressItem { Description = "TPSDD Appendix B", SortOrder = 7 },
                                        new TemplateProgressItem { Description = "TPSDD Appendix C", SortOrder = 8 },
                                        new TemplateProgressItem { Description = "TPSDD Appendix D", SortOrder = 9 },
                                        new TemplateProgressItem { Description = "TPSDD Appendix E", SortOrder = 10 },
                                        new TemplateProgressItem { Description = "TPSDD Appendix F", SortOrder = 11 },
                                        new TemplateProgressItem { Description = "TPSDD Appendix G", SortOrder = 12 }
                                    }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "Develop HRM",
                                    SortOrder = 1,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "Review TPSDD",
                                    SortOrder = 2,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "Review HRM",
                                    SortOrder = 3,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "Internal Preliminary Design Review (PDR)",
                                    SortOrder = 4,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "External Preliminary Design Review (PDR)",
                                    SortOrder = 5,
                                    Items = new List<TemplateProgressItem>{ }
                                }
                            }
                        },
                        // GATE 2: Integration
                        new TemplateGate
                        {
                            Name = "Integration",
                            SortOrder = 1,
                            Blocks = new List<TemplateProgressBlock>
                            {
                                new TemplateProgressBlock
                                {
                                    Name = "Internal Critical Design Review (CDR)",
                                    SortOrder = 0,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "External Critical Design Review (CDR)",
                                    SortOrder = 1,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "Fault Insertion",
                                    SortOrder = 2,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "Prepare Final Peer Review (FPR)",
                                    SortOrder = 3,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "Preliminary Qualification Testing (PQT)",
                                    SortOrder = 4,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "Internal Physical Configuration Audit (PCA)",
                                    SortOrder = 5,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "Test Readiness Review (TRR)",
                                    SortOrder = 6,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "External Physical Configuration Audit (PCA)",
                                    SortOrder = 7,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "Final Qualification Testing (FQT)",
                                    SortOrder = 8,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "Functional Configuration Audit (FCA)",
                                    SortOrder = 9,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "CPIN Delivery",
                                    SortOrder = 10,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "Hardware Delivery",
                                    SortOrder = 11,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "TO Delivery",
                                    SortOrder = 12,
                                    Items = new List<TemplateProgressItem>{ }
                                },

                                new TemplateProgressBlock
                                {
                                    Name = "Drawings Delivery",
                                    SortOrder = 13,
                                    Items = new List<TemplateProgressItem>{ }
                                }
                            }
                        }

                    }
                };

                context.ProjectTemplates.Add(standardTemplate);
                context.SaveChanges();
                var goaTemplate = new ProjectTemplate
                {
                    Name = "GOA Compliant Template",
                    Description = "Example Template Demonstrating GOA Scheduling.",
                    Gates = new List<TemplateGate>
                    {
                        // ══════════════════════════════════════════════════════════════
                        // GATE 1: Design (Flat Leaf with Checklists)
                        // This is a single phase of work — one engineer writes the TPSDD.
                        // The checklist items are internal milestones, not schedule drivers.
                        // ══════════════════════════════════════════════════════════════
                        new TemplateGate
                        {
                            Name = "Design",
                            SortOrder = 0,
                            DurationDays = 30,
                            Predecessors = null, // First gate, no predecessors
                            Blocks = new List<TemplateProgressBlock>
                            {
                                new TemplateProgressBlock
                                {
                                    Name = "Develop TPSDD",
                                    SortOrder = 0,
                                    Items = new List<TemplateProgressItem>
                                    {
                                        new TemplateProgressItem { Description = "TPSDD Chapter 1", SortOrder = 0 },
                                        new TemplateProgressItem { Description = "TPSDD Chapter 2", SortOrder = 1 },
                                        new TemplateProgressItem { Description = "TPSDD Chapter 3", SortOrder = 2 },
                                        new TemplateProgressItem { Description = "TPSDD Chapter 4", SortOrder = 3 },
                                        new TemplateProgressItem { Description = "TPSDD Chapter 5", SortOrder = 4 },
                                        new TemplateProgressItem { Description = "TPSDD Chapter 6", SortOrder = 5 },
                                        new TemplateProgressItem { Description = "TPSDD Appendix A", SortOrder = 6 },
                                        new TemplateProgressItem { Description = "TPSDD Appendix B", SortOrder = 7 },
                                        new TemplateProgressItem { Description = "TPSDD Appendix C", SortOrder = 8 },
                                        new TemplateProgressItem { Description = "TPSDD Appendix D", SortOrder = 9 },
                                        new TemplateProgressItem { Description = "TPSDD Appendix E", SortOrder = 10 },
                                        new TemplateProgressItem { Description = "TPSDD Appendix F", SortOrder = 11 },
                                        new TemplateProgressItem { Description = "TPSDD Appendix G", SortOrder = 12 }
                                    }
                                },
                                new TemplateProgressBlock { Name = "Develop HRM", SortOrder = 1, Items = new List<TemplateProgressItem>() },
                                new TemplateProgressBlock { Name = "Review TPSDD", SortOrder = 2, Items = new List<TemplateProgressItem>() },
                                new TemplateProgressBlock { Name = "Review HRM", SortOrder = 3, Items = new List<TemplateProgressItem>() },
                                new TemplateProgressBlock { Name = "Internal Preliminary Design Review (PDR)", SortOrder = 4, Items = new List<TemplateProgressItem>() },
                                new TemplateProgressBlock { Name = "External Preliminary Design Review (PDR)", SortOrder = 5, Items = new List<TemplateProgressItem>() }
                            }
                        },

                        // ══════════════════════════════════════════════════════════════
                        // GATE 2: Integration (Summary with Logic-Driven Child Tasks)
                        //
                        // GAO Rule: "Does another task depend specifically on this item finishing?"
                        //   YES → Make it a TemplateTask (becomes a leaf WorkBreakdownItem).
                        //   NO  → Keep it as a TemplateProgressItem (checklist).
                        //
                        // This gate is broken into 4 distinct leaf nodes:
                        //   1. Software Development  — can start immediately after Design
                        //   2. Hardware Delivery      — external wait (Receipt task, 0 work hours)
                        //   3. UUT Delivery           — external wait (Receipt task, 0 work hours)
                        //   4. Station Integration    — the merge point (depends on ALL 3 above)
                        //
                        // Critical Path Example:
                        //   If Hardware takes 60 days and Code takes 45 days,
                        //   Station Integration waits for Hardware → Hardware is on the Critical Path.
                        //   Software Development has 15 days of float (slack).
                        // ══════════════════════════════════════════════════════════════
                        new TemplateGate
                        {
                            Name = "Integration",
                            SortOrder = 1,
                            DurationDays = 0, // Summary — duration rolled up from children
                            Predecessors = null, // Children manage their own predecessors
                            Tasks = new List<TemplateTask>
                            {
                                // Task 1: Software Development
                                // Can start immediately after Design finishes (FS)
                                new TemplateTask
                                {
                                    Name = "Software Development",
                                    SortOrder = 0,
                                    DurationDays = 45,
                                    WorkHours = 360, // 45 days × 8 hours
                                    ItemType = WorkItemType.Leaf,
                                    Predecessors = "0" // Gate 0 (Design)
                                },

                                // Task 2: Hardware Delivery (Receipt Task)
                                // External dependency — vendor lead time. 0 internal work.
                                new TemplateTask
                                {
                                    Name = "Hardware Delivery",
                                    SortOrder = 1,
                                    DurationDays = 60, // Vendor lead time
                                    WorkHours = 0,     // No internal effort — it's a wait
                                    ItemType = WorkItemType.Receipt,
                                    Predecessors = null // Starts at project start (or contract award)
                                },

                                // Task 3: UUT Delivery (Receipt Task)
                                // External dependency — customer-furnished equipment.
                                new TemplateTask
                                {
                                    Name = "UUT Delivery",
                                    SortOrder = 2,
                                    DurationDays = 30,
                                    WorkHours = 0,
                                    ItemType = WorkItemType.Receipt,
                                    Predecessors = null
                                },

                                // Task 4: Station Integration (The Merge Point)
                                // Cannot start until ALL three predecessors finish.
                                // This is where the Critical Path converges.
                                new TemplateTask
                                {
                                    Name = "Station Integration",
                                    SortOrder = 3,
                                    DurationDays = 20,
                                    WorkHours = 160, // 20 days × 8 hours
                                    ItemType = WorkItemType.Leaf,
                                    Predecessors = "1.0, 1.1, 1.2" // Software Dev, Hardware, UUT
                                }
                            },
                            // Internal process checklists (not schedule-driving)
                            Blocks = new List<TemplateProgressBlock>
                            {
                                new TemplateProgressBlock { Name = "Internal CDR", SortOrder = 0, Items = new List<TemplateProgressItem>() },
                                new TemplateProgressBlock { Name = "External CDR", SortOrder = 1, Items = new List<TemplateProgressItem>() },
                                new TemplateProgressBlock { Name = "Test Readiness Review (TRR)", SortOrder = 2, Items = new List<TemplateProgressItem>() }
                            }
                        }
                    }
                };

                context.ProjectTemplates.Add(goaTemplate);
                context.SaveChanges();
            }
        }
    }
}
