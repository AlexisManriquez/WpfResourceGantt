using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using WpfResourceGantt.ProjectManagement.Data;
using WpfResourceGantt.ProjectManagement.Models;
using WpfResourceGantt.ProjectManagement.Services;

namespace WpfResourceGantt.ProjectManagement
{
    public class UserPreferences
    {
        public string LastView { get; set; } = "Dashboard";
    }

    public class DataService
    {
        private ProjectData? _projectData;
        private const decimal DEFAULT_HOURLY_RATE = 195.0m;
        private const string PREFERENCES_FILE = "user_preferences.json";
        private string PreferencesPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_preferences.json");
        private bool _isSaving = false;
        private bool _saveRequested = false;
        private bool _hasUnsavedChanges = false;
        private CancellationTokenSource? _saveDebounceCts;
        private Task _activeSaveTask = Task.CompletedTask;

        private readonly IEvmCalculationService _evmService;
        private readonly IScheduleCalculationService _scheduleService;
        private readonly IResourceAnalysisService _resourceService;

        public List<User> AllUsers => _projectData?.Users ?? new List<User>();
        public List<SystemItem> AllSystems => _projectData?.Systems ?? new List<SystemItem>();
        public bool IsEvmHoursBased => _projectData?.IsEvmHoursBased ?? false;
        public event EventHandler DataChanged;

        /// <summary>
        /// Constructor. Accepts IEvmCalculationService via manual injection.
        /// </summary>
        public DataService(IEvmCalculationService evmService, IScheduleCalculationService scheduleService, IResourceAnalysisService resourceService)
        {
            _evmService = evmService ?? throw new ArgumentNullException(nameof(evmService));
            _scheduleService = scheduleService ?? new ScheduleCalculationService();
            _resourceService = resourceService ?? new ResourceAnalysisService();
        }

        /// <summary>
        /// Parameterless constructor for contexts that do not require EVM calculations
        /// (e.g., StartupViewModel / login screen — only loads Users for authentication).
        /// Uses a default EvmCalculationService internally to satisfy the contract.
        /// </summary>
        public DataService() : this(new EvmCalculationService(), new ScheduleCalculationService(), new ResourceAnalysisService())
        {
        }

        public async Task ImportMppAndSaveAsync(string filePath, string targetSystemId = null, string newSystemName = null, string newSystemNumber = null)
        {
            string defaultPmId = _projectData?.Users.FirstOrDefault(u => u.Role == Role.ProjectManager)?.Id
                                 ?? _projectData?.Users.FirstOrDefault()?.Id;

            var importService = new MppImportService();
            // CHANGE: Service now returns List<WorkBreakdownItem>
            List<WorkBreakdownItem> importedProjects = await Task.Run(() => importService.ParseMppFile(filePath, defaultPmId));

            if (importedProjects == null || !importedProjects.Any()) return;

            if (_projectData == null) _projectData = new ProjectData();

            SystemItem targetSystem;

            if (!string.IsNullOrEmpty(newSystemName))
            {
                // Calculate WBS based on existing system count (1, 2, 3...)
                int nextSequence = 0;
                if (_projectData.Systems.Any())
                {
                    var numericValues = _projectData.Systems
                        .Select(s => int.TryParse(s.WbsValue, out int val) ? val : 0);

                    if (numericValues.Any())
                    {
                        nextSequence = numericValues.Max();
                    }
                }
                string structuralWbs = (nextSequence + 1).ToString();

                targetSystem = new SystemItem
                {
                    Id = $"SYS-{Guid.NewGuid().ToString().Substring(0, 8)}",
                    // APPLYING YOUR PATTERN: Number is a prefix for the Name property
                    Name = $"{newSystemNumber} {newSystemName}".Trim(),
                    WbsValue = structuralWbs,
                    Sequence = nextSequence
                };
                _projectData.Systems.Add(targetSystem);
            }
            else
            {
                targetSystem = _projectData.Systems.FirstOrDefault(s => s.Id == targetSystemId);
            }

            if (targetSystem != null)
            {
                targetSystem.Children.AddRange(importedProjects);

                // Ensure IDs follow the System path (e.g., SYS-ABC|0|1)
                SanitizeChildrenIds(targetSystem.Children, targetSystem.Id, 1);

                // Ensure display WBS values follow the structural root (e.g., 2.1, 2.2)
                RegenerateWbsValues(targetSystem.Id);

                targetSystem.RecalculateRollup();
            }

            MarkSystemDirty(targetSystem.Id);
            await SaveDataAsync();
        }
        public async Task LoadDataAsync(string filePath = "ProjectManagement/data.json")
        {
            using (var context = new AppDbContext())
            {
                var users = await context.Users.ToListAsync(); // Removed AsNoTracking
                var systems = await context.Systems.OrderBy(s => s.Sequence).ToListAsync();

                var allItems = await context.WorkItems
                    .Include(w => w.ProgressBlocks).ThenInclude(pb => pb.Items)
                    .Include(w => w.ProgressHistory)
                    .Include(w => w.Assignments)
                    .OrderBy(w => w.Sequence)
                    .ToListAsync();

                // ... [existing tree reconstruction logic] ...

                foreach (var sys in systems)
                {
                    sys.Children = sys.Children.OrderBy(c => c.Sequence).ToList();
                    SortChildrenRecursive(sys.Children);
                }

                // Single authoritative rollup: all EVM metrics (BAC, BCWS, BCWP, dates,
                // Progress) are calculated here and nowhere else in the application.
                // ACWP is NOT touched — it is owned exclusively by CsvImportService.
                _scheduleService.CalculateSchedule(systems);
                _resourceService.AnalyzeResources(systems, users);
                _evmService.RecalculateAll(systems);

                // Persist the authoritative BCWS/BCWP/Progress values back to the DB
                // so every screen reads from one consistent data store.
                await context.SaveChangesAsync();

                _projectData = new ProjectData { Users = users, Systems = systems };
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        private void SortChildrenRecursive(List<WorkBreakdownItem> items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                // 1. Sort the execution blocks by Sequence
                if (item.ProgressBlocks != null)
                {
                    item.ProgressBlocks = item.ProgressBlocks.OrderBy(b => b.Sequence).ToList();
                    foreach (var block in item.ProgressBlocks)
                    {
                        // 2. Sort the checklist items by Sequence
                        if (block.Items != null)
                        {
                            block.Items = block.Items.OrderBy(i => i.Sequence).ToList();
                        }
                    }
                }

                if (item.Children != null && item.Children.Any())
                {
                    item.Children = item.Children.OrderBy(c => c.Sequence).ToList();
                    SortChildrenRecursive(item.Children);
                }
            }
        }
        public async Task SeedDatabaseFromJson(AppDbContext context, string filePath)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show($"Backup file not found at: {filePath}", "Restore Error");
                return;
            }

            try
            {
                var jsonString = await File.ReadAllTextAsync(filePath);

                // Use options that handle the recursive WBS tree properly
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles
                };

                var data = JsonSerializer.Deserialize<ProjectData>(jsonString, options);

                if (data != null)
                {
                    // 1. Clear any existing data just in case context isn't fresh
                    // (Optional, but safe for a 'Nuclear' reset)
                    context.Users.RemoveRange(context.Users);
                    context.AdminTasks.RemoveRange(context.AdminTasks);
                    context.Systems.RemoveRange(context.Systems);
                    await context.SaveChangesAsync();

                    // 2. Restore Users (Important to do this first for FKs)
                    if (data.Users != null && data.Users.Any())
                    {
                        context.Users.AddRange(data.Users);
                    }

                    // 3. Restore AdminTasks
                    if (data.AdminTasks != null && data.AdminTasks.Any())
                    {
                        context.AdminTasks.AddRange(data.AdminTasks);
                    }

                    // 4. Restore Systems and the entire WBS Graph
                    if (data.Systems != null && data.Systems.Any())
                    {
                        // NOTE: We do NOT call SanitizeImportedIds here because 
                        // we want to preserve the exact IDs and relationships 
                        // from the backup, not generate new ones.
                        context.Systems.AddRange(data.Systems);
                    }

                    // 5. Final Commit to Database
                    await context.SaveChangesAsync();

                    // 6. Update local memory cache so the UI shows the data immediately
                    _projectData = data;

                    MessageBox.Show("Database successfully restored from backup!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                // Log deep details for debugging
                var inner = ex.InnerException != null ? $"\nDetails: {ex.InnerException.Message}" : "";
                MessageBox.Show($"Error restoring database: {ex.Message}{inner}", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public async Task ExportFullDatabaseToBackup(string fileName = "full_db_backup.json")
        {
            using (var context = new AppDbContext())
            {
                // 1. Fetch the 3 "Root" or Independent tables
                var users = await context.Users.AsNoTracking().ToListAsync();
                var adminTasks = await context.AdminTasks.AsNoTracking().ToListAsync();
                var systems = await context.Systems.AsNoTracking().ToListAsync();

                // 2. Fetch the WorkItems and their 4 related child tables
                // (WorkItems, ProgressBlocks, ProgressItems, ProgressHistory, ResourceAssignments)
                var allWorkItems = await context.WorkItems
                    .Include(w => w.ProgressBlocks)
                        .ThenInclude(pb => pb.Items)
                    .Include(w => w.ProgressHistory)
                    .Include(w => w.Assignments)
                    .AsNoTracking()
                    .ToListAsync();

                // 3. Reconstruct the Tree Hierarchy for the JSON structure
                var itemMap = allWorkItems.ToDictionary(i => i.Id);
                foreach (var s in systems) s.Children = new List<WorkBreakdownItem>();

                foreach (var item in allWorkItems)
                {
                    item.Children = new List<WorkBreakdownItem>();

                    // Determine Parentage based on your ID format (SYS|1|2)
                    int lastPipe = item.Id.LastIndexOf('|');
                    if (lastPipe != -1)
                    {
                        string parentId = item.Id.Substring(0, lastPipe);

                        if (itemMap.TryGetValue(parentId, out var parentItem))
                            parentItem.Children.Add(item);
                        else
                        {
                            var sys = systems.FirstOrDefault(s => s.Id == parentId);
                            sys?.Children.Add(item);
                        }
                    }
                }

                // 4. Wrap everything into a single object that matches your ProjectData model
                var backupObject = new
                {
                    Users = users,
                    Systems = systems,
                    AdminTasks = adminTasks // We explicitly add this now
                };

                // 5. Save to File
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles // Prevents errors with recursive trees
                };

                string jsonString = JsonSerializer.Serialize(backupObject, options);
                await File.WriteAllTextAsync(fileName, jsonString);

                MessageBox.Show($"Full backup of all 8 tables saved to {fileName}", "Backup Success");
            }
        }
        public async Task SaveDataAsync()
        {
            _hasUnsavedChanges = true;

            // Cancel any pending save timer
            _saveDebounceCts?.Cancel();
            _saveDebounceCts = new CancellationTokenSource();
            var token = _saveDebounceCts.Token;

            try
            {
                // Wait 300ms. If another save is requested before 300ms is up, this gets canceled.
                await Task.Delay(300, token);
            }
            catch (TaskCanceledException)
            {
                // A new click happened! Let the new click handle the save.
                return;
            }

            // If we survived the 300ms delay without being canceled, execute the save.
            // We wait for any currently running DB operation to finish first to prevent thread crashes.
            await _activeSaveTask;

            _activeSaveTask = ExecuteDbSaveAsync();
            await _activeSaveTask;
        }

        // Forces an immediate save (Used when the app is closing)
        // Only saves if there are actual unsaved changes to prevent stale data overwrites.
        public async Task EnsureSavedAsync()
        {
            // Stop the 300ms timer
            _saveDebounceCts?.Cancel();

            // Wait for any DB operation currently running
            await _activeSaveTask;

            // Only save if the user actually made changes in this session
            if (_hasUnsavedChanges)
            {
                await ExecuteDbSaveAsync();
            }
        }
        /// <summary>
        /// Marks all entities in the given system's subtree as dirty so they are
        /// included in the next save. Call this from any ViewModel that modifies
        /// data within a specific system before calling SaveDataAsync().
        /// </summary>
        public void MarkSystemDirty(string systemId)
        {
            var system = _projectData?.Systems?.FirstOrDefault(s => s.Id == systemId);
            if (system == null) return;
            system.IsDirty = true;
            foreach (var child in system.Children)
                MarkWorkBreakdownItemDirty(child);
        }

        /// <summary>
        /// Marks a single user as dirty so their record is included in the next save.
        /// Call from UserManagementViewModel when editing an existing user.
        /// </summary>
        public void MarkUserDirty(string userId)
        {
            var user = _projectData?.Users?.FirstOrDefault(u => u.Id == userId);
            if (user != null) user.IsDirty = true;
        }

        // Single recursive helper — both mark and clear share the same traversal logic.
        private void SetDirtyFlagsRecursive(WorkBreakdownItem item, bool dirty)
        {
            item.IsDirty = dirty;
            foreach (var block in item.ProgressBlocks)
            {
                block.IsDirty = dirty;
                foreach (var pi in block.Items) pi.IsDirty = dirty;
            }
            foreach (var hist in item.ProgressHistory)
                hist.IsDirty = dirty;
            foreach (var assignment in item.Assignments)
                assignment.IsDirty = dirty;
            foreach (var child in item.Children)
                SetDirtyFlagsRecursive(child, dirty);
        }

        private void MarkWorkBreakdownItemDirty(WorkBreakdownItem item)
            => SetDirtyFlagsRecursive(item, dirty: true);

        private void ClearAllDirtyFlags()
        {
            if (_projectData == null) return;
            foreach (var system in _projectData.Systems)
            {
                system.IsDirty = false;
                foreach (var child in system.Children)
                    SetDirtyFlagsRecursive(child, dirty: false);
            }
            foreach (var user in _projectData.Users)
                user.IsDirty = false;
        }

        private void NotifySaveSuccess()
        {
            _hasUnsavedChanges = false;
            ClearAllDirtyFlags();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task ExecuteDbSaveAsync()
        {
            if (_isSaving) return;
            _isSaving = true;


            try
            {
                // FORCE authoratative rollup before any persistence.
                // This ensures Logic-Driven dates and EVM metrics are in sync.
                if (_projectData?.Systems != null)
                {
                    _scheduleService.CalculateSchedule(_projectData.Systems);
                    _resourceService.AnalyzeResources(_projectData.Systems, _projectData.Users);
                    _evmService.RecalculateAll(_projectData.Systems);
                }

                using (var context = new AppDbContext())
                {
                    // 1. COLLECT ALL CURRENT IDs (from memory)
                    var currentSystemIds = _projectData.Systems.Select(s => s.Id).ToHashSet();
                    var currentWorkItemIds = new HashSet<string>();
                    var currentBlockIds = new HashSet<string>();
                    var currentChecklistIds = new HashSet<string>();
                    var currentUserIds = _projectData.Users.Select(u => u.Id).ToHashSet();
                    var currentHistoryIds = new HashSet<string>();
                    var currentAssignmentIds = new HashSet<string>();

                    // Local function to recursively collect IDs and set Sequences
                    void CollectIds(List<WorkBreakdownItem> items)
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            var item = items[i];
                            item.Sequence = i; // Save Order

                            currentWorkItemIds.Add(item.Id);

                            bool isSummary = item.Children != null && item.Children.Count > 0;

                            // A. Progress Blocks (Only valid for Leaves)
                            if (!isSummary && item.ProgressBlocks != null)
                            {
                                // FIX: Sort the blocks before re-assigning sequences
                                var sortedBlocks = item.ProgressBlocks.OrderBy(b => b.Sequence).ToList();
                                item.ProgressBlocks.Clear();
                                foreach (var b in sortedBlocks) item.ProgressBlocks.Add(b);

                                for (int j = 0; j < item.ProgressBlocks.Count; j++)
                                {
                                    var block = item.ProgressBlocks[j];
                                    block.Sequence = j;
                                    currentBlockIds.Add(block.Id);

                                    if (block.Items != null)
                                    {
                                        // FIX: Sort the items before re-assigning sequences
                                        var sortedItems = block.Items.OrderBy(i => i.Sequence).ToList();
                                        block.Items.Clear();
                                        foreach (var itm in sortedItems) block.Items.Add(itm);

                                        for (int k = 0; k < block.Items.Count; k++)
                                        {
                                            var checklistItem = block.Items[k];
                                            checklistItem.Sequence = k;
                                            currentChecklistIds.Add(checklistItem.Id);
                                        }
                                    }
                                }
                            }

                            // B. History (Valid for all)
                            if (item.ProgressHistory != null)
                            {
                                foreach (var hist in item.ProgressHistory)
                                {
                                    currentHistoryIds.Add(hist.Id);
                                }
                            }

                            // C. Assignments (Only valid for Leaves)
                            // If it's a summary, we skip collecting assignments, which means
                            // they will be detected as "orphans" below and deleted from DB.
                            if (!isSummary && item.Assignments != null)
                            {
                                foreach (var assignment in item.Assignments)
                                {
                                    currentAssignmentIds.Add(assignment.Id);
                                }
                            }

                            // D. Recurse
                            if (isSummary)
                            {
                                CollectIds(item.Children);
                            }
                        }
                    }

                    // Iterate through Systems using a FOR loop to set System Sequence
                    for (int i = 0; i < _projectData.Systems.Count; i++)
                    {
                        var system = _projectData.Systems[i];
                        system.Sequence = i; // Save System Order

                        if (system.Children != null)
                            CollectIds(system.Children);
                    }

                    // 2. FETCH ALL IDs FROM DB
                    var dbWorkItemIds = await context.WorkItems.Select(w => w.Id).ToListAsync();
                    var dbBlockIds = await context.ProgressBlocks.Select(p => p.Id).ToListAsync();
                    var dbChecklistIds = await context.Set<ProgressItem>().Select(pi => pi.Id).ToListAsync();
                    var dbSystemIds = await context.Systems.Select(s => s.Id).ToListAsync();
                    var dbUserIds = await context.Users.Select(u => u.Id).ToListAsync();
                    var dbHistoryIds = await context.ProgressHistory.Select(ph => ph.Id).ToListAsync();
                    var dbAssignmentIds = await context.ResourceAssignments.Select(ra => ra.Id).ToListAsync();

                    // 3. PERFORM DELETIONS (Remove items in DB that are no longer in memory)

                    var checklistToDelete = dbChecklistIds.Except(currentChecklistIds).ToList();
                    if (checklistToDelete.Any())
                    {
                        var toRemove = await context.Set<ProgressItem>().Where(pi => checklistToDelete.Contains(pi.Id)).ToListAsync();
                        context.Set<ProgressItem>().RemoveRange(toRemove);
                    }

                    var blocksToDelete = dbBlockIds.Except(currentBlockIds).ToList();
                    if (blocksToDelete.Any())
                    {
                        var toRemove = await context.ProgressBlocks.Where(p => blocksToDelete.Contains(p.Id)).ToListAsync();
                        context.ProgressBlocks.RemoveRange(toRemove);
                    }

                    var historyToDelete = dbHistoryIds.Except(currentHistoryIds).ToList();
                    if (historyToDelete.Any())
                    {
                        var toRemove = await context.ProgressHistory.Where(ph => historyToDelete.Contains(ph.Id)).ToListAsync();
                        context.ProgressHistory.RemoveRange(toRemove);
                    }

                    // Delete orphaned assignments (e.g. from items that became summaries)
                    var assignmentsToDelete = dbAssignmentIds.Except(currentAssignmentIds).ToList();
                    if (assignmentsToDelete.Any())
                    {
                        var toRemove = await context.ResourceAssignments.Where(ra => assignmentsToDelete.Contains(ra.Id)).ToListAsync();
                        context.ResourceAssignments.RemoveRange(toRemove);
                    }

                    var workItemsToDelete = dbWorkItemIds.Except(currentWorkItemIds).ToList();
                    if (workItemsToDelete.Any())
                    {
                        var toRemove = await context.WorkItems.Where(w => workItemsToDelete.Contains(w.Id)).ToListAsync();
                        context.WorkItems.RemoveRange(toRemove);
                    }

                    var systemsToDelete = dbSystemIds.Except(currentSystemIds).ToList();
                    if (systemsToDelete.Any())
                    {
                        var toRemove = await context.Systems.Where(s => systemsToDelete.Contains(s.Id)).ToListAsync();
                        context.Systems.RemoveRange(toRemove);
                    }

                    var usersToDelete = dbUserIds.Except(currentUserIds).ToList();
                    if (usersToDelete.Any())
                    {
                        var toRemove = await context.Users.Where(u => usersToDelete.Contains(u.Id)).ToListAsync();
                        context.Users.RemoveRange(toRemove);
                    }

                    // 4. SMART SAVE (Delta-Only Graph)
                    // Only entities with IsDirty = true are marked Modified.
                    // Existing entities that were not touched by this user session are
                    // Detached so EF Core generates no UPDATE and no RowVersion check for them.
                    // This eliminates false concurrency conflicts between users on different projects.
                    var trackedIdsInThisSession = new HashSet<string>();

                    foreach (var system in _projectData.Systems)
                    {
                        context.ChangeTracker.TrackGraph(system, node =>
                        {
                            string id = null;
                            bool isDirty = false;

                            if (node.Entry.Entity is SystemItem s)        { id = s.Id; isDirty = s.IsDirty; }
                            else if (node.Entry.Entity is WorkBreakdownItem w) { id = w.Id; isDirty = w.IsDirty; }
                            else if (node.Entry.Entity is ProgressBlock b)     { id = b.Id; isDirty = b.IsDirty; }
                            else if (node.Entry.Entity is ProgressItem pi)     { id = pi.Id; isDirty = pi.IsDirty; }
                            else if (node.Entry.Entity is ProgressHistoryItem ph) { id = ph.Id; isDirty = ph.IsDirty; }
                            else if (node.Entry.Entity is ResourceAssignment ra)  { id = ra.Id; isDirty = ra.IsDirty; }

                            if (id == null || trackedIdsInThisSession.Contains(id))
                            {
                                node.Entry.State = EntityState.Detached;
                                return;
                            }

                            bool existsInDb = dbSystemIds.Contains(id) || dbWorkItemIds.Contains(id) ||
                                             dbBlockIds.Contains(id) || dbChecklistIds.Contains(id) ||
                                             dbHistoryIds.Contains(id) || dbAssignmentIds.Contains(id);

                            if (!existsInDb)
                                node.Entry.State = EntityState.Added;
                            else if (isDirty)
                                node.Entry.State = EntityState.Modified;
                            else
                                node.Entry.State = EntityState.Detached; // Not changed — skip entirely

                            trackedIdsInThisSession.Add(id);
                        });
                    }

                    // Handle Users separately (only save dirty or new users)
                    foreach (var user in _projectData.Users)
                    {
                        bool userExistsInDb = dbUserIds.Contains(user.Id);
                        if (!userExistsInDb)
                            context.Entry(user).State = EntityState.Added;
                        else if (user.IsDirty)
                            context.Entry(user).State = EntityState.Modified;
                        // else: skip — not modified in this session
                    }

                    // Handle Admin Tasks separately (since they are roots in the DB)
                    var dbAdminTaskIds = await context.AdminTasks.Select(a => a.Id).ToListAsync();
                    // Assuming _projectData has AdminTasks list (if you added it)
                    // If not, this part is skipped, but AdminTasks won't be deleted implicitly.

                    // 5. SAVE with silent-merge retry for concurrent conflicts
                    try
                    {
                        await context.SaveChangesAsync();
                        NotifySaveSuccess();
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        // Phase 3: Silent Merge — reload the fresh RowVersions for conflicting
                        // entries and retry. This handles the rare case where two users
                        // edit the SAME row at nearly the same instant.
                        foreach (var entry in ex.Entries)
                        {
                            var dbValues = await entry.GetDatabaseValuesAsync();
                            if (dbValues != null)
                            {
                                // Refresh the RowVersion token so the retry UPDATE matches the DB
                                entry.OriginalValues.SetValues(dbValues);
                            }
                            else
                            {
                                // The row was deleted by another user — detach it to allow partial save
                                entry.State = EntityState.Detached;
                            }
                        }

                        try
                        {
                            await context.SaveChangesAsync();
                            NotifySaveSuccess();
                        }
                        catch (DbUpdateConcurrencyException)
                        {
                            // True simultaneous conflict: two users edited the exact same row
                            // at the exact same instant even after retry.
                            // Leave _hasUnsavedChanges = true so EnsureSavedAsync retries on close.
                            MessageBox.Show(
                                "Your changes could not be saved because another user is editing the same record simultaneously.\n\n" +
                                "Please close and reopen the application to load the latest data, then re-apply your edits.",
                                "Concurrent Edit Conflict",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var detailedError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                MessageBox.Show($"Error saving to DB: {detailedError}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isSaving = false;
            }
        }
        public async Task<ObservableCollection<ResourcePerson>> GetResourceGanttDataAsync()
        {
            using (var context = new AppDbContext())
            {
                // 1. Fetch Lookups (Users, Systems, and Admin Tasks)
                var users = await context.Users.AsNoTracking().ToListAsync();
                var systems = await context.Systems.AsNoTracking().ToListAsync();
                var adminTasks = await context.AdminTasks.AsNoTracking().ToListAsync();

                // 2. Fetch ALL Assignments
                var assignments = await context.ResourceAssignments.AsNoTracking().ToListAsync();

                // 3. IDENTIFY PARENTS: Fetch IDs of all items that act as a parent to others
                // In your AppDbContext, the FK is defined as "ParentId"
                var parentIds = await context.WorkItems
                    .Select(w => EF.Property<string>(w, "ParentId"))
                    .Where(pid => pid != null)
                    .Distinct()
                    .ToListAsync();
                var parentIdSet = new HashSet<string>(parentIds);

                // 4. Fetch Work Items that have assignments
                var assignedWorkItemIds = assignments.Select(a => a.WorkItemId).Distinct().ToList();
                var allocatedItems = await context.WorkItems
                    .Where(w => assignedWorkItemIds.Contains(w.Id))
                    .AsNoTracking()
                    .ToListAsync();

                // 5. Construct the Result Collection
                var result = new ObservableCollection<ResourcePerson>();

                foreach (var user in users)
                {
                    // A. Create the Person
                    var person = new ResourcePerson
                    {
                        Name = user.Name,
                        Section = user.Section,
                        Role = user.Role.ToString().Replace("ProjectManager", "PM"),
                        Tasks = new ObservableCollection<ResourceTask>()
                    };

                    // B. Find Assignments for THIS user
                    var userAssignments = assignments.Where(a => a.DeveloperId == user.Id).ToList();

                    foreach (var assignment in userAssignments)
                    {
                        // Find the corresponding work item
                        var wi = allocatedItems.FirstOrDefault(w => w.Id == assignment.WorkItemId);

                        // CRITICAL FILTER: 
                        // 1. Ensure WorkItem exists.
                        // 2. Ensure WorkItem is NOT in the parentIdSet (it must be a leaf).
                        if (wi == null || parentIdSet.Contains(wi.Id))
                        {
                            continue;
                        }

                        // C. Derive Project Context
                        var systemId = wi.Id.Contains("|") ? wi.Id.Split('|')[0] : "";
                        var system = systems.FirstOrDefault(s => s.Id == systemId);

                        // D. Derive Project Office Symbol
                        // Find the PM who manages this project (Level 1) by looking at User.ManagedProjectIds
                        string projectSymbol = "N/A";
                        // Derive the Level 1 project ID from the work item's ID (e.g., "SYS-123|0|2" -> "SYS-123|0")
                        string projectIdForSymbol = wi.Id;
                        var idParts = wi.Id.Split('|');
                        if (idParts.Length >= 2)
                            projectIdForSymbol = $"{idParts[0]}|{idParts[1]}";

                        var managingPm = users.FirstOrDefault(u =>
                            u.Role == Role.ProjectManager &&
                            u.ManagedProjectIds != null &&
                            u.ManagedProjectIds.Contains(projectIdForSymbol));

                        if (managingPm != null)
                        {
                            var managingChief = users.FirstOrDefault(u =>
                                u.Role == Role.SectionChief &&
                                u.ManagedProjectManagerIds != null &&
                                u.ManagedProjectManagerIds.Contains(managingPm.Id));

                            if (managingChief != null)
                                projectSymbol = managingChief.Section;
                        }

                        // E. Map Status
                        WpfResourceGantt.ProjectManagement.Models.TaskStatus status = WpfResourceGantt.ProjectManagement.Models.TaskStatus.InWork;
                        if (wi.Status == Models.WorkItemStatus.OnHold) status = WpfResourceGantt.ProjectManagement.Models.TaskStatus.OnHold;
                        if (wi.Status == Models.WorkItemStatus.Future) status = WpfResourceGantt.ProjectManagement.Models.TaskStatus.Future;

                        // F. Add Task to Person
                        var task = new ResourceTask
                        {
                            Name = wi.Name,
                            StartDate = wi.StartDate?.Date ?? DateTime.Today,
                            EndDate = wi.EndDate?.Date ?? DateTime.Today.AddDays(1),
                            Status = status,
                            ProjectName = system?.Name ?? "Unknown System",
                            ProjectOfficeSymbol = projectSymbol,
                            ResourceOfficeSymbol = user.Section,
                            AssignmentRole = assignment.Role,
                            IsVisibleInView = true
                        };

                        person.Tasks.Add(task);
                    }

                    // --- PROCESS ADMIN TASKS ---
                    var myAdminTasks = adminTasks.Where(t => t.AssignedUserId == user.Id).ToList();
                    foreach (var adminTask in myAdminTasks)
                    {
                        person.Tasks.Add(new ResourceTask
                        {
                            Name = adminTask.Name,
                            StartDate = adminTask.StartDate.Date,
                            EndDate = adminTask.EndDate.Date,
                            Status = adminTask.Status,
                            ProjectName = "Administrative",
                            ProjectOfficeSymbol = "ADM",
                            ResourceOfficeSymbol = user.Section,
                            AssignmentRole = AssignmentRole.Primary,
                            IsVisibleInView = true
                        });
                    }

                    // G. Sort tasks by Start Date
                    var sortedTasks = person.Tasks.OrderBy(t => t.StartDate).ToList();
                    person.Tasks = new ObservableCollection<ResourceTask>(sortedTasks);

                    result.Add(person);
                }

                return result;
            }
        }
        public async Task<string?> GetUserIdByNameAsync(string name)
        {
            using (var context = new AppDbContext())
            {
                // Find the user case-insensitively to be safe
                var user = await context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Name == name);

                return user?.Id;
            }
        }
        // Inside DataService.cs

        public async Task<ObservableCollection<ResourceTask>> GetUnassignedGanttTasksAsync()
        {
            using (var context = new AppDbContext())
            {
                // 1. Identify all items that are parents (have children)
                // We look for any ID that is referenced in the "ParentId" shadow property
                var parentIds = await context.WorkItems
                    .Select(w => EF.Property<string>(w, "ParentId"))
                    .Where(pid => pid != null)
                    .Distinct()
                    .ToListAsync();

                var parentIdSet = new HashSet<string>(parentIds);

                // 2. Fetch WorkItems that:
                //    a) Have no assignments
                //    b) Are NOT in the parentIdSet (they must be leaves)
                var unassignedWorkItems = await context.WorkItems
                    .Where(w => !w.Assignments.Any())
                    .AsNoTracking()
                    .ToListAsync();

                // 3. Filter manually for leaves and map to ResourceTask
                var list = new ObservableCollection<ResourceTask>();

                // Fetch systems for context (Project Names)
                var systems = await context.Systems.AsNoTracking().ToListAsync();

                foreach (var wi in unassignedWorkItems)
                {
                    // SKIP if this item is a parent
                    if (parentIdSet.Contains(wi.Id)) continue;

                    // Map Status
                    WpfResourceGantt.ProjectManagement.Models.TaskStatus status = WpfResourceGantt.ProjectManagement.Models.TaskStatus.InWork;
                    if (wi.Status == Models.WorkItemStatus.OnHold) status = WpfResourceGantt.ProjectManagement.Models.TaskStatus.OnHold;
                    if (wi.Status == Models.WorkItemStatus.Future) status = WpfResourceGantt.ProjectManagement.Models.TaskStatus.Future;

                    // Find parent system name
                    var systemId = wi.Id.Contains("|") ? wi.Id.Split('|')[0] : "";
                    var system = systems.FirstOrDefault(s => s.Id == systemId);

                    list.Add(new ResourceTask
                    {
                        Name = wi.Name,
                        StartDate = wi.StartDate ?? DateTime.Today,
                        EndDate = wi.EndDate ?? DateTime.Today.AddDays(1),
                        Status = status,
                        ProjectName = system?.Name ?? "Unassigned",
                        IsVisibleInView = true
                    });
                }
                return list;
            }
        }

        public async Task DeleteSystemAsync(string systemId)
        {
            if (_projectData?.Systems == null) return;

            var systemToRemove = _projectData.Systems.FirstOrDefault(s => s.Id == systemId);
            if (systemToRemove != null)
            {
                // 1. CASCADE CLEANUP: Remove all project IDs under this system from PM ManagedProjectIds
                var projectIds = new List<string>();
                CollectAllItemIdsRecursive(systemToRemove.Children, projectIds);
                CleanupManagedProjectIds(projectIds);

                // 2. Remove from local memory list
                _projectData.Systems.Remove(systemToRemove);

                // 3. The existing SaveDataAsync will detect the ID is missing from _projectData.Systems
                // and execute context.Systems.RemoveRange(...) based on your 'systemsToDelete' logic.
                await SaveDataAsync();
            }
        }
        private void CollectMetadataRecursive(
    IEnumerable<WorkBreakdownItem> items,
    HashSet<string> itemIds,
    HashSet<string> blockIds,
    HashSet<string> itemChecklistIds,
            HashSet<string> historyIds)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                itemIds.Add(item.Id);

                if (item.ProgressBlocks != null)
                {
                    foreach (var block in item.ProgressBlocks)
                    {
                        // Ensure ID exists (WPF might have created it empty)
                        if (string.IsNullOrEmpty(block.Id) || block.Id.Length < 10)
                        {
                            block.Id = "PB-" + Guid.NewGuid().ToString("N").Substring(0, 12);
                        }
                        blockIds.Add(block.Id);

                        if (block.Items != null)
                        {
                            foreach (var checkItem in block.Items)
                            {
                                if (string.IsNullOrEmpty(checkItem.Id)) checkItem.Id = Guid.NewGuid().ToString();
                                itemChecklistIds.Add(checkItem.Id);
                            }
                        }
                    }
                }
                if (item.ProgressHistory != null)
                {
                    foreach (var history in item.ProgressHistory)
                    {
                        if (string.IsNullOrEmpty(history.Id)) history.Id = Guid.NewGuid().ToString();
                        historyIds.Add(history.Id);
                    }
                }
                CollectMetadataRecursive(item.Children, itemIds, blockIds, itemChecklistIds, historyIds);
            }
        }

        /// <summary>
        /// Ensures that every System and its Children have globally unique IDs.
        /// It handles cases where WBS numbers (1.1, 1.2) are repeated across different projects.
        /// </summary>
        private void SanitizeImportedIds(List<SystemItem> systems)
        {
            var existingSystemIds = new HashSet<string>();

            foreach (var system in systems)
            {
                if (string.IsNullOrEmpty(system.WbsValue)) system.WbsValue = system.Id;

                // Ensure System ID is unique and doesn't conflict with numeric WBS
                if (string.IsNullOrWhiteSpace(system.Id) || !system.Id.StartsWith("SYS-"))
                {
                    system.Id = $"SYS-{Guid.NewGuid().ToString().Substring(0, 8)}";
                }

                existingSystemIds.Add(system.Id);

                // Process children with Level 1 (Projects)
                SanitizeChildrenIds(system.Children, system.Id, 1);
            }
        }

        // 2. Update SanitizeChildrenIds (For Tasks)
        // Inside DataService.cs

        public void SanitizeChildrenIds(List<WorkBreakdownItem> items, string parentIdPrefix, int level)
        {
            if (items == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                // 1. Ensure Sequence is correct
                item.Sequence = i;

                // 2. Set Level
                item.Level = level;
                // ONLY generate a new ID if it's empty or doesn't follow the parent path
                // This prevents existing items from "losing" their ID (and expansion state)
                if (string.IsNullOrEmpty(item.Id) || !item.Id.StartsWith(parentIdPrefix))
                {
                    item.Id = $"{parentIdPrefix}|{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                }
                // 3. GENERATE ID BASED ON SEQUENCE
                // This creates a solid chain like: "SYS-A|0|2" (System A -> 1st Child -> 3rd Grandchild)
                // This is 100% unique and reproducible on reload.
                //item.Id = $"{parentIdPrefix}|{item.Sequence}";

                // 4. Initialize History if needed
                if (item.Children == null || !item.Children.Any())
                {
                    if (item.ProgressHistory == null) item.ProgressHistory = new List<ProgressHistoryItem>();
                    if (!item.ProgressHistory.Any())
                    {
                        item.ProgressHistory.Add(new ProgressHistoryItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            Date = item.StartDate ?? DateTime.Now.Date,
                            ActualProgress = 0,
                            ActualWork = 0,
                            ExpectedProgress = 0
                        });
                    }
                }

                // 5. Recurse using the NEW ID
                SanitizeChildrenIds(item.Children, item.Id, level + 1);
            }
        }

        public async Task CreateAdminTaskAsync(string name, string userId, DateTime start, DateTime end, WpfResourceGantt.ProjectManagement.Models.TaskStatus status, string description = "")
        {
            using (var context = new AppDbContext())
            {
                var task = new AdminTask
                {
                    Name = name,
                    Description = description,
                    AssignedUserId = userId,
                    StartDate = start,
                    EndDate = end,
                    Status = status
                };

                context.AdminTasks.Add(task);
                await context.SaveChangesAsync();

                // Notify subscribers
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task<List<AdminTask>> GetAdminTasksForUserAsync(string userId)
        {
            using (var context = new AppDbContext())
            {
                return await context.AdminTasks
                    .Where(t => t.AssignedUserId == userId)
                    .AsNoTracking()
                    .ToListAsync();
            }
        }

        public async Task<List<AdminTask>> GetAllAdminTasksAsync()
        {
            using (var context = new AppDbContext())
            {
                return await context.AdminTasks
                    .AsNoTracking()
                    .ToListAsync();
            }
        }

        public async Task UpdateAdminTaskAsync(AdminTask task)
        {
            using (var context = new AppDbContext())
            {
                context.AdminTasks.Update(task);
                await context.SaveChangesAsync();
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task DeleteAdminTaskAsync(string taskId)
        {
            using (var context = new AppDbContext())
            {
                var task = await context.AdminTasks.FindAsync(taskId);
                if (task != null)
                {
                    context.AdminTasks.Remove(task);
                    await context.SaveChangesAsync();
                    DataChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void SavePreferences(string currentView)
        {
            try
            {
                var prefs = new UserPreferences { LastView = currentView };
                var json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(PreferencesPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save preferences: {ex.Message}");
            }
        }

        public async Task<string> LoadPreferencesAsync()
        {
            try
            {
                if (!File.Exists(PreferencesPath)) return null;
                var json = await File.ReadAllTextAsync(PreferencesPath);
                var prefs = JsonSerializer.Deserialize<UserPreferences>(json);
                return prefs?.LastView;
            }
            catch { return null; }
        }

        public async Task ReplicateSubProjectStructureAsync(string sourceSubProjectId)
        {
            var sourceSubProject = GetWorkBreakdownItemById(sourceSubProjectId);
            if (sourceSubProject == null || sourceSubProject.Level != 2) return;

            string sourcePrefix = sourceSubProject.Name.Split(' ').FirstOrDefault() ?? "";
            string systemId = sourceSubProjectId.Split('|')[0];
            var system = GetSystemById(systemId);

            var allSubProjectsInSystem = new List<WorkBreakdownItem>();
            CollectByLevel(system.Children, level: 2, allSubProjectsInSystem);
            var targetSubProjects = allSubProjectsInSystem.Where(c => c.Id != sourceSubProjectId).ToList();

            int subProjectCount = 0;
            int itemsInjectedCount = 0;

            foreach (var targetSub in targetSubProjects)
            {
                string targetPrefix = targetSub.Name.Split(' ').FirstOrDefault() ?? "";
                bool modified = false;

                // 1. Match Gates (Level 3)
                foreach (var sourceGate in sourceSubProject.Children)
                {
                    string sourceGateCore = GetCoreName(sourceGate.Name);
                    var targetGate = targetSub.Children.FirstOrDefault(tg => GetCoreName(tg.Name) == sourceGateCore);

                    if (targetGate != null)
                    {
                        // 2. Match Tasks (Level 4) inside the Gate
                        foreach (var sourceTask in sourceGate.Children)
                        {
                            string sourceTaskCore = GetCoreName(sourceTask.Name);
                            var targetTask = targetGate.Children.FirstOrDefault(tt => GetCoreName(tt.Name) == sourceTaskCore);

                            if (targetTask != null)
                            {
                                // --- INJECTION LOGIC ---
                                // We do NOT change targetTask.StartDate or targetTask.EndDate.

                                // A. Inject Progress Blocks (Checklists)
                                targetTask.ProgressBlocks = CloneBlocks(sourceTask.ProgressBlocks);

                                // B. Inject Level 5 Sub-tasks (if they exist in source)
                                if (sourceTask.Children != null && sourceTask.Children.Any())
                                {
                                    targetTask.Children.Clear();
                                    foreach (var sourceSubTask in sourceTask.Children)
                                    {
                                        var clonedSubTask = DeepCloneForReplication(sourceSubTask, sourcePrefix, targetPrefix);

                                        // MANDATORY: To keep Level 4 dates from shifting, the new Level 5 
                                        // sub-tasks must inherit the target parent's current dates.
                                        clonedSubTask.StartDate = targetTask.StartDate;
                                        clonedSubTask.EndDate = targetTask.EndDate;

                                        targetTask.Children.Add(clonedSubTask);
                                        itemsInjectedCount++;
                                    }
                                }
                                modified = true;
                            }
                        }
                    }
                }

                if (modified)
                {
                    // Fix IDs for the new nested items
                    SanitizeChildrenIds(targetSub.Children, targetSub.Id, 3);
                    subProjectCount++;
                }
            }

            MarkSystemDirty(systemId);
            await SaveDataAsync();
            MessageBox.Show($"Injection Success!\n\nSynced {subProjectCount} SubProjects.\nInjected {itemsInjectedCount} sub-tasks and their checklists.\n\nNote: Original dates for existing Gates and Tasks were preserved.");
        }

        private List<ProgressBlock> CloneBlocks(List<ProgressBlock> sourceBlocks)
        {
            if (sourceBlocks == null) return new List<ProgressBlock>();
            return sourceBlocks.Select(pb => new ProgressBlock
            {
                Id = "PB-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                Name = pb.Name,
                Sequence = pb.Sequence,
                Items = pb.Items?.Select(pi => new ProgressItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = pi.Name,
                    Sequence = pi.Sequence,
                    IsCompleted = false
                }).ToList() ?? new List<ProgressItem>()
            }).ToList();
        }

        // Helper to strip prefixes like "339-025-1825"
        private string GetCoreName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "";
            int firstSpace = fullName.IndexOf(' ');
            if (firstSpace == -1) return fullName.Trim();
            return fullName.Substring(firstSpace).Trim();
        }

        private WorkBreakdownItem DeepCloneForReplication(WorkBreakdownItem source, string oldPrefix, string newPrefix)
        {
            var clone = new WorkBreakdownItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = source.Name?.Replace(oldPrefix, newPrefix),
                ItemType = source.ItemType,
                DurationDays = source.DurationDays,
                Work = source.Work,
                ScheduleMode = source.ScheduleMode,
                Status = WorkItemStatus.Active,
                Level = source.Level,

                // Reset assignment/execution data as requested
                AssignedDeveloperId = null,
                Assignments = new List<ResourceAssignment>(),
                Predecessors = null,
                Progress = 0,
                ActualWork = 0,
                Bcws = 0,
                Bcwp = 0,
                Acwp = 0,

                // Deep Clone Checklist Blocks
                ProgressBlocks = source.ProgressBlocks?.Select(pb => new ProgressBlock
                {
                    Id = "PB-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                    Name = pb.Name,
                    Sequence = pb.Sequence,
                    Items = pb.Items?.Select(pi => new ProgressItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = pi.Name,
                        Sequence = pi.Sequence,
                        IsCompleted = false
                    }).ToList() ?? new List<ProgressItem>()
                }).ToList() ?? new List<ProgressBlock>(),

                Children = new List<WorkBreakdownItem>()
            };

            // Recursively clone Level 4 tasks AND their Level 5 sub-tasks
            if (source.Children != null)
            {
                foreach (var child in source.Children)
                {
                    clone.Children.Add(DeepCloneForReplication(child, oldPrefix, newPrefix));
                }
            }

            return clone;
        }


        #region System & User Management (Largely Unchanged)

        public List<SystemItem> GetSystemsForUser(User loggedInUser)
        {
            if (loggedInUser == null || _projectData?.Systems == null) return new List<SystemItem>();

            switch (loggedInUser.Role)
            {
                // Leadership and Administrator roles see ALL systems and ALL projects
                case Role.Administrator:
                case Role.FlightChief:
                case Role.SectionChief:
                case Role.TechnicalSpecialist:
                    return _projectData.Systems;

                // Project Managers see ALL systems, but only their managed projects
                case Role.ProjectManager:
                    return FilterProjectsForPM(loggedInUser);

                // Developers see only systems/branches containing their assigned tasks
                case Role.Developer:
                    return FilterSystemsForDeveloper(loggedInUser.Id);

                default:
                    return new List<SystemItem>();
            }
        }

        /// <summary>
        /// Returns ALL systems but filters Level 1 children to only those the PM manages.
        /// A PM "manages" a project if:
        ///   1. The project ID is in their ManagedProjectIds list, OR
        ///   2. The PM has a ResourceAssignment on the project (Level 1 item)
        /// Systems that have no managed projects for this PM will still appear (as empty containers).
        /// </summary>
        private List<SystemItem> FilterProjectsForPM(User pmUser)
        {
            var managedIds = pmUser.ManagedProjectIds ?? new List<string>();
            var result = new List<SystemItem>();

            foreach (var system in _projectData.Systems)
            {
                // Filter Level 1 children (Projects) to only those the PM manages
                // Check BOTH the explicit ManagedProjectIds list AND ResourceAssignments
                var filteredChildren = system.Children
                    .Where(c => managedIds.Contains(c.Id) ||
                                (c.Assignments != null && c.Assignments.Any(a => a.DeveloperId == pmUser.Id)))
                    .ToList();

                // Always show the system container, even if empty for this PM
                result.Add(new SystemItem
                {
                    Id = system.Id,
                    Name = system.Name,
                    WbsValue = system.WbsValue,
                    Status = system.Status,
                    Children = filteredChildren
                });
            }
            return result;
        }

        public SystemItem GetSystemById(string systemId)
        {
            return _projectData?.Systems.FirstOrDefault(s => s.Id == systemId);
        }

        public int GetSystemCount()
        {
            return _projectData?.Systems?.Count ?? 0;
        }

        public void AddSystem(SystemItem newSystem)
        {
            _projectData?.Systems?.Add(newSystem);
        }

        public void UpdateSystem(SystemItem updatedSystem)
        {
            if (_projectData?.Systems == null) return;
            int index = _projectData.Systems.FindIndex(s => s.Id == updatedSystem.Id);
            if (index != -1)
            {
                _projectData.Systems[index] = updatedSystem;
            }
        }

        public void AddUser(User newUser)
        {
            _projectData?.Users?.Add(newUser);
        }

        public async Task DeleteUserAsync(string userId)
        {
            using (var context = new AppDbContext())
            {
                var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user != null)
                {
                    // 1. Database Cleanup

                    // Remove individual assignments
                    var assignments = await context.ResourceAssignments.Where(a => a.DeveloperId == userId).ToListAsync();
                    context.ResourceAssignments.RemoveRange(assignments);

                    // Clear legacy fields
                    var workItems = await context.WorkItems.Where(w => w.AssignedDeveloperId == userId).ToListAsync();
                    foreach (var wi in workItems) wi.AssignedDeveloperId = null;

                    // Remove the user
                    context.Users.Remove(user);

                    await context.SaveChangesAsync();

                    // 2. Local Cache Cleanup

                    // Remove from users list
                    var localUser = _projectData?.Users.FirstOrDefault(u => u.Id == userId);
                    if (localUser != null) _projectData.Users.Remove(localUser);

                    // Recursive cleanup in the systems tree
                    if (_projectData?.Systems != null)
                    {
                        foreach (var system in _projectData.Systems)
                        {
                            RemoveUserAssignmentsRecursive(system.Children, userId);
                        }
                    }

                    // Notify subscribers
                    DataChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public async Task SaveUserAsync(User user)
        {
            using (var context = new AppDbContext())
            {
                // Check if exists
                var exists = await context.Users.AnyAsync(u => u.Id == user.Id);

                if (exists)
                {
                    context.Users.Update(user);

                    // Update local cache
                    var localIndex = _projectData?.Users.FindIndex(u => u.Id == user.Id) ?? -1;
                    if (localIndex != -1) _projectData.Users[localIndex] = user;
                }
                else
                {
                    context.Users.Add(user);
                    _projectData?.Users.Add(user);
                }

                await context.SaveChangesAsync();

                // Notify subscribers
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion

        #region Recursive WorkBreakdownItem Methods

        /// <summary>
        /// Gets a single WorkBreakdownItem from anywhere in the hierarchy by its ID.
        /// </summary>
        public WorkBreakdownItem GetWorkBreakdownItemById(string id)
        {
            if (_projectData?.Systems == null) return null;

            foreach (var system in _projectData.Systems)
            {
                var found = FindItemByIdRecursive(system.Children, id);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Reorders two sibling items within the data model.
        /// </summary>
        public bool ReorderWorkItem(string draggedId, string targetId, string position)
        {
            if (_projectData?.Systems == null) return false;

            // 1. Find the list containing both items
            var (list, dragged, target) = FindSharedParentList(_projectData.Systems, draggedId, targetId);
            if (list == null || dragged == null || target == null) return false;

            int draggedIndex = list.IndexOf(dragged);
            int targetIndex = list.IndexOf(target);

            // 2. Calculate Insertion Index logic
            int adjustment = (draggedIndex < targetIndex) ? -1 : 0;
            int rawInsertionIndex = targetIndex;

            if (position == "DropAfter") rawInsertionIndex++;

            int finalInsertionIndex = rawInsertionIndex + adjustment;

            if (finalInsertionIndex == draggedIndex) return false;

            // 3. Perform Move
            list.RemoveAt(draggedIndex);

            if (finalInsertionIndex < 0) finalInsertionIndex = 0;
            if (finalInsertionIndex > list.Count) finalInsertionIndex = list.Count;

            list.Insert(finalInsertionIndex, dragged);

            // 4. Regenerate WBS values using the System ID found via the internal ID format
            // Assuming ID format is "SYS-ID|..." or similar
            string systemId = draggedId.Contains("|") ? draggedId.Split('|')[0] : draggedId.Split('.')[0];
            RegenerateWbsValues(systemId);

            return true;
        }

        public void RegenerateWbsValues(string systemId)
        {
            var system = _projectData.Systems.FirstOrDefault(s => s.Id == systemId);
            if (system == null) return;

            // Ensure the root system has a valid WbsValue to start the chain
            string rootWbs = string.IsNullOrEmpty(system.WbsValue) ? "1" : system.WbsValue;

            RegenerateWbsValuesRecursive(system.Children, rootWbs);
        }

        private void RegenerateWbsValuesRecursive(List<WorkBreakdownItem> items, string parentWbs)
        {
            if (items == null) return;

            // Use a for loop to ensure we have the index for the Sequence and WBS
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                // 1. Sync the database sequence with the current list order
                item.Sequence = i;

                // 2. Generate the WBS (e.g., "1.1", "1.1.2")
                // We use i + 1 because WBS usually starts at 1, not 0.
                item.WbsValue = $"{parentWbs}.{i + 1}";

                // 3. Recurse to children
                RegenerateWbsValuesRecursive(item.Children, item.WbsValue);
            }
        }
        public async Task BaselineSystemAsync(string systemId, bool isRebaseline = false)
        {
            var system = GetSystemById(systemId);
            if (system == null) return;

            string title = isRebaseline ? "Confirm Rebaseline" : "Confirm Baseline";
            string msg = isRebaseline
                ? $"Are you sure you want to REBASELINE '{system.Name}'?\n\nThis will overwrite the current budget (BAC) with the newly planned work hours. All current Cost Variances will be reset."
                : $"Are you sure you want to baseline '{system.Name}'?\n\nThis will freeze the budget (BAC) based on the currently planned work hours.";

            var result = MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            BaselineItemsRecursive(system.Children);
            system.RecalculateRollup();

            MarkSystemDirty(systemId);
            await SaveDataAsync();
        }

        private void BaselineItemsRecursive(List<WorkBreakdownItem> items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                item.IsBaselined = true;
                if (item.Children == null || !item.Children.Any())
                {
                    // Leaf Node: Lock in the BAC based on EVM Mode
                    item.BAC = (decimal)(item.Work ?? 0) * WorkBreakdownItem.HourlyRate;
                }
                else
                {
                    // Summary Node: Recurse
                    BaselineItemsRecursive(item.Children);
                }
            }
        }
        /// <summary>
        /// Calculates and sets the Budget at Completion (BAC) for an entire system and its children.
        /// </summary>
        public void CalculateAndSetBAC(SystemItem system)
        {
            if (system == null) return;
            CalculateBACRecursive(system.Children);
        }

        public async Task ToggleEvmModeAsync(bool toHours)
        {
            if (_projectData == null || _projectData.IsEvmHoursBased == toHours) return;

            // FIX: Only toggle the visual preference flag. 
            // Do NOT call ConvertEvmRecursive or modify the underlying numbers.
            _projectData.IsEvmHoursBased = toHours;

            DataChanged?.Invoke(this, EventArgs.Empty);
            await SaveDataAsync();
        }



        /// <summary>
        /// Recursively updates the IDs in the data model to match the new hierarchy from the ViewModel.
        /// </summary>
        public void UpdateModelIdsFromWorkItems(IEnumerable<WorkItem> workItems)
        {
            foreach (var workItem in workItems)
            {
                // Find the backend item by its (now outdated) ID and update it.
                var itemToUpdate = GetWorkBreakdownItemById(workItem.Id); // This uses the ID *before* it's changed.
                if (itemToUpdate != null)
                {
                    // This logic assumes you are re-generating IDs in the ViewModel.
                    // A better approach would be to find by a permanent GUID.
                    // But based on the request, we find and update.
                }

                if (workItem.Children.Any())
                {
                    UpdateModelIdsFromWorkItems(workItem.Children);
                }
            }
        }

        // ProjectManagement/DataService.cs

        public WorkBreakdownItem CloneWorkItem(WorkBreakdownItem original, string newParentId, int newSequence)
        {
            // 1. Generate a unique ID based on the new parent's path + a random suffix to prevent PK collisions
            string uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 4);
            string newId = $"{newParentId}|{newSequence}_{uniqueSuffix}";

            // 2. Map basic properties and reset execution data
            var clone = new WorkBreakdownItem
            {
                Id = newId,
                WbsValue = original.WbsValue, // This will be recalculated by RegenerateWbsValues later
                Name = original.Name + " (Copy)",
                StartDate = original.StartDate,
                EndDate = original.EndDate,
                DurationDays = original.DurationDays,
                Predecessors = original.Predecessors,
                StartNoEarlierThan = original.StartNoEarlierThan,
                ScheduleMode = original.ScheduleMode,
                Work = original.Work,
                BAC = original.BAC,
                Status = WorkItemStatus.Active,
                Level = original.Level,
                ItemType = original.ItemType, // Preserve milestone / receipt / leaf type

                // Explicitly reset all progress/execution metrics for the new copy
                Progress = 0,
                ActualWork = 0,
                ActualFinishDate = null, // Milestones clone in uncompleted state
                Bcws = 0,
                Bcwp = 0,
                Acwp = 0,
                AssignedDeveloperId = null, // Reset assignment on copy

                // Initialize Collections
                Assignments = new List<ResourceAssignment>(),
                ProgressHistory = new List<ProgressHistoryItem>
        {
            new ProgressHistoryItem { Id = Guid.NewGuid().ToString(), Date = DateTime.Today, ActualProgress = 0 }
        }
            };

            // 3. Deep Clone Progress Blocks and Checklist Items
            if (original.ProgressBlocks != null)
            {
                clone.ProgressBlocks = original.ProgressBlocks.Select(pb => new ProgressBlock
                {
                    // Blocks need new IDs to avoid DB conflicts
                    Id = "PB-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                    Name = pb.Name,
                    Sequence = pb.Sequence,
                    Items = pb.Items?.Select(pi => new ProgressItem
                    {
                        // Items need new IDs
                        Id = Guid.NewGuid().ToString(),
                        Name = pi.Name,
                        Sequence = pi.Sequence,
                        IsCompleted = false // Reset checkbox
                    }).ToList() ?? new List<ProgressItem>()
                }).ToList();
            }

            // 4. Recursive Deep Clone of Children
            if (original.Children != null && original.Children.Any())
            {
                clone.Children = new List<WorkBreakdownItem>();
                for (int i = 0; i < original.Children.Count; i++)
                {
                    // Recurse: children use the clone's new ID as their parent prefix
                    var childClone = CloneWorkItem(original.Children[i], clone.Id, i);
                    childClone.Level = clone.Level + 1;
                    clone.Children.Add(childClone);
                }
            }

            return clone;
        }

        public SystemItem CloneSystem(SystemItem original)
        {
            var clone = new SystemItem
            {
                Id = $"SYS-{Guid.NewGuid().ToString().Substring(0, 8)}",
                WbsValue = original.WbsValue,
                Name = original.Name + " (Copy)",
                Status = WorkItemStatus.Active
            };

            // Fix: Use the overload of Select that provides the index
            if (original.Children != null)
            {
                clone.Children = original.Children
                    .Select((child, index) => CloneWorkItem(child, clone.Id, index))
                    .ToList();
            }
            else
            {
                clone.Children = new List<WorkBreakdownItem>();
            }

            return clone;
        }

        #endregion

        #region Private Recursive Helpers

        /// <summary>
        /// Recursively finds a WorkBreakdownItem by its ID within a given collection.
        /// </summary>
        private WorkBreakdownItem FindItemByIdRecursive(IEnumerable<WorkBreakdownItem> items, string id)
        {
            if (items == null) return null;
            foreach (var item in items)
            {
                if (item.Id == id) return item;
                var found = FindItemByIdRecursive(item.Children, id);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Recursively finds the shared parent list of two items.
        /// </summary>
        private (List<WorkBreakdownItem> list, WorkBreakdownItem item1, WorkBreakdownItem item2) FindSharedParentList(IEnumerable<SystemItem> systems, string id1, string id2)
        {
            foreach (var system in systems)
            {
                var result = FindSharedParentRecursive(system.Children, id1, id2);
                if (result.list != null) return result;
            }
            return (null, null, null);
        }

        private (List<WorkBreakdownItem> list, WorkBreakdownItem item1, WorkBreakdownItem item2) FindSharedParentRecursive(List<WorkBreakdownItem> collection, string id1, string id2)
        {
            var item1 = collection.FirstOrDefault(i => i.Id == id1);
            var item2 = collection.FirstOrDefault(i => i.Id == id2);

            if (item1 != null && item2 != null)
            {
                return (collection, item1, item2);
            }

            foreach (var item in collection)
            {
                var result = FindSharedParentRecursive(item.Children, id1, id2);
                if (result.list != null) return result;
            }
            return (null, null, null);
        }

        /// <summary>
        /// Recursively calculates the BAC for a collection of items.
        /// </summary>
        private decimal CalculateBACRecursive(IEnumerable<WorkBreakdownItem> items)
        {
            decimal totalBac = 0;
            if (items == null) return 0;

            foreach (var item in items)
            {
                if (item.Children.Any())
                {
                    // If it's a summary item, its BAC is the sum of its children's BACs.
                    item.BAC = CalculateBACRecursive(item.Children);
                    totalBac += item.BAC ?? 0;
                }
                else
                {
                    // If it's a leaf item (a task), calculate its cost directly.
                    var developer = AllUsers.FirstOrDefault(u => u.Id == item.AssignedDeveloperId);

                    // Respect the EVM calculation mode
                    decimal rate = IsEvmHoursBased ? 1.0m : (developer?.HourlyRate ?? DEFAULT_HOURLY_RATE);
                    totalBac += (decimal)(item.Work ?? 0) * rate;
                }
            }
            return totalBac;
        }

        /// <summary>
        /// Creates a filtered copy of the project hierarchy containing only items relevant to a developer.
        /// </summary>
        private List<SystemItem> FilterSystemsForDeveloper(string developerId)
        {
            var developerSystems = new List<SystemItem>();
            if (_projectData?.Systems == null) return developerSystems;

            foreach (var system in _projectData.Systems)
            {
                var relevantChildren = FilterChildrenForDeveloper(system.Children, developerId);
                if (relevantChildren.Any())
                {
                    // If relevant children were found, create a new SystemItem clone.
                    developerSystems.Add(new SystemItem
                    {
                        // --- EXPLICITLY COPY ALL SYSTEM PROPERTIES ---
                        Id = system.Id,
                        Name = system.Name,
                        Status = system.Status,

                        // And use the filtered list of children.
                        Children = relevantChildren
                    });
                }
            }
            return developerSystems;
        }

        private List<WorkBreakdownItem> FilterChildrenForDeveloper(List<WorkBreakdownItem> items, string developerId)
        {
            var relevantItems = new List<WorkBreakdownItem>();
            if (items == null) return relevantItems;

            foreach (var item in items)
            {
                if (!item.Children.Any() && item.AssignedDeveloperId == developerId)
                {
                    // Clone for leaf
                    relevantItems.Add(new WorkBreakdownItem
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Level = item.Level,
                        StartDate = item.StartDate,
                        EndDate = item.EndDate,
                        Work = item.Work,
                        ActualWork = item.ActualWork,
                        Progress = item.Progress,
                        AssignedDeveloperId = item.AssignedDeveloperId,
                        ProgressBlocks = item.ProgressBlocks,
                        ProgressHistory = item.ProgressHistory, // NEW: preserve history for individual tasks
                        Status = item.Status
                    });
                }
                else if (item.Children.Any())
                {
                    var relevantGrandchildren = FilterChildrenForDeveloper(item.Children, developerId);
                    if (relevantGrandchildren.Any())
                    {
                        // Clone for summary
                        relevantItems.Add(new WorkBreakdownItem
                        {
                            Id = item.Id,
                            Name = item.Name,
                            Level = item.Level,
                            StartDate = item.StartDate,
                            EndDate = item.EndDate,
                            Work = item.Work,
                            ActualWork = item.ActualWork,
                            Progress = item.Progress,
                            Children = relevantGrandchildren,
                            ProgressBlocks = item.ProgressBlocks, // Preserve blocks
                            ProgressHistory = item.ProgressHistory, // Preserve history
                            Status = item.Status
                        });
                    }
                }
            }
            return relevantItems;
        }

        private void RemoveUserAssignmentsRecursive(List<WorkBreakdownItem> items, string userId)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item.AssignedDeveloperId == userId) item.AssignedDeveloperId = null;
                if (item.Assignments != null)
                {
                    item.Assignments.RemoveAll(a => a.DeveloperId == userId);
                }
                RemoveUserAssignmentsRecursive(item.Children, userId);
            }
        }

        /// <summary>
        /// Removes the specified item IDs from all PM users' ManagedProjectIds lists.
        /// Call this when a project or its children are deleted.
        /// </summary>
        public void CleanupManagedProjectIds(List<string> deletedItemIds)
        {
            if (_projectData?.Users == null || deletedItemIds == null || !deletedItemIds.Any()) return;

            foreach (var user in _projectData.Users.Where(u => u.Role == Role.ProjectManager))
            {
                if (user.ManagedProjectIds != null)
                {
                    user.ManagedProjectIds.RemoveAll(id => deletedItemIds.Contains(id));
                }
            }
        }

        /// <summary>
        /// Recursively collects all WorkBreakdownItem IDs under the given collection.
        /// Used for cascade cleanup when deleting branches.
        /// </summary>
        public void CollectAllItemIdsRecursive(List<WorkBreakdownItem> items, List<string> ids)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                ids.Add(item.Id);
                CollectAllItemIdsRecursive(item.Children, ids);
            }
        }

        /// <summary>
        /// Recursively cleans up all resource assignments for items being deleted.
        /// Removes assignments from the data model and clears AssignedDeveloperId.
        /// </summary>
        public void CleanupAssignmentsOnDelete(List<WorkBreakdownItem> items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                item.Assignments?.Clear();
                item.AssignedDeveloperId = null;
                CleanupAssignmentsOnDelete(item.Children);
            }
        }

        #endregion

        // ════════════════════════════════════════════════════════════════════
        // PHASE 3: WEEKLY EVM SNAPSHOTS
        // ════════════════════════════════════════════════════════════════════
        #region Weekly Snapshot Service

        /// <summary>
        /// Calculates the WeekEndingDate for a given date.
        /// DoD standard: week closes on Sunday (end of business week).
        /// If today is already Sunday, use today; otherwise walk back to the prior Sunday.
        /// </summary>
        public static DateTime GetWeekEndingDate(DateTime forDate)
        {
            int daysToSunday = (int)forDate.DayOfWeek; // Sunday = 0
            return forDate.AddDays(-daysToSunday).Date;
        }

        /// <summary>
        /// Takes a weekly EVM snapshot for every SubProject (Level 2) across all
        /// systems in scope. Called by the PM when "Close Week" is triggered.
        ///
        /// BUSINESS RULES:
        ///   - WeekEndingDate = prior Sunday
        ///   - If a snapshot already exists for that (SubProject, Week) AND IsLocked = true,
        ///     it is skipped (immutable).
        ///   - If unlocked, the existing snapshot is overwritten (supports corrections
        ///     before PM locks it).
        ///   - ACWP is read directly from the model (already set by CsvImportService).
        ///   - Values are CUMULATIVE, not period-only.
        /// </summary>
        /// <param name="createdByUserId">User ID of the PM triggering the snapshot.</param>
        /// <returns>Number of SubProject snapshots written.</returns>
        public async Task<int> TakeWeeklySnapshotsAsync(string createdByUserId)
        {
            DateTime weekEnding = GetWeekEndingDate(DateTime.Today);
            int count = 0;

            using var context = new AppDbContext();

            // Collect all SubProject-level WorkItems (Level == 2) from loaded data
            var subProjects = new List<WorkBreakdownItem>();
            foreach (var sys in AllSystems)
            {
                CollectByLevel(sys.Children, level: 2, subProjects);
            }

            foreach (var sp in subProjects)
            {
                // Calculate this SubProject's cumulative EVM metrics
                double bac = (double)(GetLeafSum(sp, w => (double)(w.BAC ?? 0)));
                double bcws = GetLeafSum(sp, w => w.Bcws ?? 0);
                double bcwp = GetLeafSum(sp, w => w.Bcwp ?? 0);
                double acwp = GetLeafSum(sp, w => w.Acwp ?? 0);

                double spi = bcws > 0 ? Math.Round(bcwp / bcws, 3) : 1.0;
                double cpi = acwp > 0 ? Math.Round(bcwp / acwp, 3) : 1.0;
                double progress = bac > 0 ? Math.Round(bcwp / bac, 4) : 0.0;

                // Check for existing snapshot this week
                var existing = await context.EvmWeeklySnapshots
                    .FirstOrDefaultAsync(s => s.SubProjectId == sp.Id
                                           && s.WeekEndingDate == weekEnding);

                if (existing != null && existing.IsLocked)
                {
                    // PM locked this snapshot — do not overwrite. Skip.
                    continue;
                }

                if (existing != null)
                {
                    // Unlocked snapshot: update in place (supports corrections before lock)
                    existing.BAC = (decimal)bac;
                    existing.BCWS = bcws;
                    existing.BCWP = bcwp;
                    existing.ACWP = acwp;
                    existing.SPI = spi;
                    existing.CPI = cpi;
                    existing.Progress = progress;
                    existing.CreatedAt = DateTime.UtcNow;
                    existing.CreatedByUserId = createdByUserId;
                }
                else
                {
                    // New snapshot for this week
                    context.EvmWeeklySnapshots.Add(new EvmWeeklySnapshot
                    {
                        Id = Guid.NewGuid().ToString(),
                        SubProjectId = sp.Id,
                        WeekEndingDate = weekEnding,
                        BAC = (decimal)bac,
                        BCWS = bcws,
                        BCWP = bcwp,
                        ACWP = acwp,
                        SPI = spi,
                        CPI = cpi,
                        Progress = progress,
                        CreatedAt = DateTime.UtcNow,
                        CreatedByUserId = createdByUserId,
                        IsLocked = false
                    });
                }
                count++;
            }

            await context.SaveChangesAsync();
            return count;
        }

        /// <summary>
        /// Retrieves all historical snapshots for a specific SubProject,
        /// ordered chronologically. Used by the EVM S-Curve.
        /// </summary>
        public async Task<List<EvmWeeklySnapshot>> GetSnapshotsForSubProjectAsync(string subProjectId)
        {
            using var context = new AppDbContext();
            return await context.EvmWeeklySnapshots
                .Where(s => s.SubProjectId == subProjectId)
                .OrderBy(s => s.WeekEndingDate)
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves snapshots for all SubProjects within a given System or Project.
        /// </summary>
        public async Task<List<EvmWeeklySnapshot>> GetSnapshotsForSystemAsync(string systemId)
        {
            // Collect all SubProject IDs under this system
            var subProjectIds = new List<string>();
            var sys = AllSystems.FirstOrDefault(s => s.Id == systemId);
            if (sys != null)
                CollectByLevel(sys.Children, level: 2, new List<WorkBreakdownItem>());

            // Rebuild correctly with ID capture
            var spItems = new List<WorkBreakdownItem>();
            if (sys != null) CollectByLevel(sys.Children, level: 2, spItems);
            subProjectIds.AddRange(spItems.Select(sp => sp.Id));

            if (!subProjectIds.Any()) return new List<EvmWeeklySnapshot>();

            using var context = new AppDbContext();
            return await context.EvmWeeklySnapshots
                .Where(s => subProjectIds.Contains(s.SubProjectId))
                .OrderBy(s => s.WeekEndingDate)
                .ToListAsync();
        }

        /// <summary>
        /// Recursively collects WorkBreakdownItems at a specified hierarchy level.
        /// </summary>
        private void CollectByLevel(IEnumerable<WorkBreakdownItem> items, int level, List<WorkBreakdownItem> result)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item.Level == level)
                    result.Add(item);
                else
                    CollectByLevel(item.Children, level, result);
            }
        }

        /// <summary>
        /// Recursively sums a numeric property from all LEAF nodes under the given item.
        /// Used for computing cumulative metrics when rolling up from leaves.
        /// </summary>
        private double GetLeafSum(WorkBreakdownItem item, Func<WorkBreakdownItem, double> selector)
        {
            if (item.Children == null || !item.Children.Any())
                return selector(item);

            return item.Children.Sum(c => GetLeafSum(c, selector));
        }

        #endregion

    }
}
