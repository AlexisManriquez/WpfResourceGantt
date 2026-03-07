using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.Models.Templates;

namespace WpfResourceGantt.ProjectManagement.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<SystemItem> Systems { get; set; }
        public DbSet<WorkBreakdownItem> WorkItems { get; set; }
        public DbSet<ProgressBlock> ProgressBlocks { get; set; }
        public DbSet<ProgressHistoryItem> ProgressHistory { get; set; }
        public DbSet<ProgressItem> ProgressItems { get; set; }
        public DbSet<ResourceAssignment> ResourceAssignments { get; set; }
        public DbSet<AdminTask> AdminTasks { get; set; }
        public DbSet<ProjectTemplate> ProjectTemplates { get; set; }
        public DbSet<TemplateGate> TemplateGates { get; set; }
        public DbSet<TemplateProgressBlock> TemplateProgressBlocks { get; set; }
        public DbSet<TemplateTask> TemplateTasks { get; set; }

        // Phase 3: Weekly EVM Snapshots — the authoritative time-series for S-Curves
        public DbSet<EvmWeeklySnapshot> EvmWeeklySnapshots { get; set; }
        public DbSet<TemplateProgressItem> TemplateProgressItems { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Build configuration to read from appsettings.json
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json")
                    .Build();

                // Get the connection string
                string connectionString = configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                    throw new Exception("The connection string is empty! Check your appsettings.json path.");
                // Use it
                optionsBuilder.UseSqlServer(connectionString, sqlServerOptionsAction: sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                });
            }
            // Connect to local SQL Express. 
            // "TrustServerCertificate=True" is often needed for local dev certificates.
            //string connectionString = @"Server=.\SQLEXPRESS;Database=ProjectManagementDb;User Id=ProjectUser;Password=ProjectPass123!;TrustServerCertificate=True;";
            //optionsBuilder.UseSqlServer(@"Server=.\SQLEXPRESS;Database=ProjectManagementDb;Trusted_Connection=True;TrustServerCertificate=True;");
            //optionsBuilder.UseSqlServer(@"Server=192.168.0.202;Database=ProjectManagementDb;User Id=ProjectUser;Password=ProjectPass123!;TrustServerCertificate=True;");
            //optionsBuilder.UseSqlServer(@"Server=134.166.152.130;Database=ProjectManagementDb;User Id=ProjectUser;Password=ProjectPass123!;TrustServerCertificate=True;");


        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure Primary Keys
            modelBuilder.Entity<User>().HasKey(u => u.Id);
            modelBuilder.Entity<SystemItem>().HasKey(s => s.Id);
            modelBuilder.Entity<WorkBreakdownItem>().HasKey(w => w.Id);
            modelBuilder.Entity<ProgressBlock>().HasKey(p => p.Id); // Ensure ProgressBlock has an Id
            modelBuilder.Entity<ProgressHistoryItem>().HasKey(p => p.Id);
            modelBuilder.Entity<ProgressItem>().HasKey(pi => pi.Id);
            modelBuilder.Entity<ResourceAssignment>().HasKey(ra => ra.Id);
            modelBuilder.Entity<AdminTask>().HasKey(a => a.Id);
            modelBuilder.Entity<ProjectTemplate>().HasKey(pt => pt.Id);
            modelBuilder.Entity<TemplateGate>().HasKey(tg => tg.Id);
            modelBuilder.Entity<TemplateProgressBlock>().HasKey(tpb => tpb.Id);
            modelBuilder.Entity<TemplateTask>().HasKey(tt => tt.Id);



            modelBuilder.Entity<User>()
                .Property(u => u.HourlyRate)
                .HasPrecision(18, 2);

            modelBuilder.Entity<WorkBreakdownItem>()
                .Property(w => w.BAC)
                .HasPrecision(18, 2);

            modelBuilder.Entity<WorkBreakdownItem>()
               .Property(w => w.AssignedDeveloperId)
               .IsRequired(false); // Allow NULLs
                                   // Configure Relationships

            // System -> Top Level WorkItems
            modelBuilder.Entity<SystemItem>()
                .HasMany(s => s.Children)
                .WithOne()
                .HasForeignKey("SystemId") // Shadow Foreign Key
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // WorkItem -> Child WorkItems (Recursive)
            modelBuilder.Entity<WorkBreakdownItem>()
                .HasMany(w => w.Children)
                .WithOne()
                .HasForeignKey("ParentId") // Shadow Foreign Key
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // WorkItem -> ProgressBlocks
            modelBuilder.Entity<WorkBreakdownItem>()
                .HasMany(w => w.ProgressBlocks)
                .WithOne()
                .HasForeignKey("WorkItemId"); // Shadow Foreign Key

            modelBuilder.Entity<WorkBreakdownItem>()
                .HasMany(w => w.ProgressHistory)
                .WithOne()
                .HasForeignKey("WorkItemId"); // Shadow Foreign Key

            // WorkItem -> Assignments
            modelBuilder.Entity<WorkBreakdownItem>()
                .HasMany(w => w.Assignments)
                .WithOne()
                .HasForeignKey("WorkItemId")
                .OnDelete(DeleteBehavior.Cascade);


            // Configure ProgressBlock -> ProgressItems
            modelBuilder.Entity<ProgressBlock>()
                .HasMany(p => p.Items)
                .WithOne()
                .HasForeignKey("ProgressBlockId"); // Shadow FK

            modelBuilder.Entity<ProgressBlock>()
        .HasMany(p => p.Items)
        .WithOne()
        .HasForeignKey("ProgressBlockId") // This creates the column in the ProgressItems table
        .OnDelete(DeleteBehavior.Cascade); // If block is deleted, items are deleted

            // Template Configurations
            modelBuilder.Entity<ProjectTemplate>()
            .HasMany(t => t.Gates)
            .WithOne(g => g.ProjectTemplate)
            .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TemplateGate>()
            .HasMany(g => g.Blocks)
            .WithOne(b => b.TemplateGate)
            .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TemplateGate>()
            .HasMany(g => g.Tasks)
            .WithOne(t => t.TemplateGate)
            .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TemplateProgressBlock>()
            .HasMany(b => b.Items)
            .WithOne(i => i.TemplateProgressBlock)
            .OnDelete(DeleteBehavior.Cascade);

            // ── Phase 3: EvmWeeklySnapshot ───────────────────────────────────────
            modelBuilder.Entity<EvmWeeklySnapshot>().HasKey(s => s.Id);

            // Enforce one snapshot per SubProject per week.
            // A second "Close Week" on the same week overwrites (unless IsLocked = true,
            // which is enforced in DataService, not at the DB constraint level).
            modelBuilder.Entity<EvmWeeklySnapshot>()
                .HasIndex(s => new { s.SubProjectId, s.WeekEndingDate })
                .IsUnique();

            modelBuilder.Entity<EvmWeeklySnapshot>()
                .Property(s => s.BAC)
                .HasPrecision(18, 2);
        }
    }
}
