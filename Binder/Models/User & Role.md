---
tags: [model]
purpose: AI reference for the User/Role model and section assignment system.
---

# User & Role

**File**: `ProjectManagement/Models/User.cs`

## User
| Property | Type | Notes |
|----------|------|-------|
| `Id` | `string` | GUID |
| `Name` | `string` | Display name |
| `Role` | `Role` | Enum (see below) |
| `Section` | `string` | Office symbol (e.g., "AFLCMC/EZJ") |
| `HourlyRate` | `decimal?` | Individual rate; defaults to `195.0m` if null |
| `WeeklyCapacity` | `double` | Default `40.0` hours |
| `ManagedProjectManagerIds` | `List<string>` | For SectionChief: which PMs they manage |
| `ManagedProjectIds` | `List<string>` | For ProjectManager: which Level 1 project IDs they manage |

## Role Enum
**File**: `ProjectManagement/Models/Role.cs`

| Value | Purpose |
|-------|---------|
| `Administrator` | Hidden superuser — full access, invisible in all UI lists |
| `FlightChief` | Top leadership — sees all systems and projects |
| `SectionChief` | Oversees PMs, used for project office derivation, sees all systems/projects |
| `TechnicalSpecialist` | Technical oversight — sees all systems and projects |
| `ProjectManager` | Manages specific projects (tracked via `ManagedProjectIds` + `ResourceAssignment`) |
| `Developer` | Assigned to tasks, sees [[Developer Portal]] |
| `ConfigurationManager` | Configuration management role |
| `Technician` | Technical support role |
| `ElectricalDesignEngineer` | EE design role |
| `MechanicalDesignEngineer` | ME design role |
| `TechWriter` | Technical documentation role |

## Role-Based Access Control

### System & Project Visibility
| Role | Systems | Projects |
|------|---------|----------|
| **Administrator** | All | All |
| **FlightChief** | All | All |
| **SectionChief** | All | All |
| **TechnicalSpecialist** | All | All |
| **ProjectManager** | All (as containers) | Only managed projects (`ManagedProjectIds` or `ResourceAssignment`) |
| **Developer** | Only relevant | Only branches with their assigned tasks |

> [!IMPORTANT]
> **Systems are pure containers.** They have no `ProjectManagerId`. Project assignment is tracked at the User level via `ManagedProjectIds` and `ResourceAssignment` on Level 1 items.

### PM Project Management
A Project Manager "manages" a project if **either**:
1. The project's ID is in their `ManagedProjectIds` list, **OR**
2. The PM has a `ResourceAssignment` on the project (Level 1 item)

When a PM is assigned to a project via the Assign Developer dialog, `ManagedProjectIds` is automatically synced.

### Administrator Role
- **Invisible** in: login combobox, User Management list, Create/Edit User role dropdown
- **Full access** to all systems, projects, and data (same as FlightChief)
- Created manually (database insert only — cannot be created through the UI)

## Role-Based Behavior
- **Developer** → auto-routed to [[Developer Portal]] on login
- **ProjectManager** → default [[Dashboard]] view; sees only managed projects in [[System Management]] and [[Gantt View]]
- **SectionChief** → used by [[Resource Gantt]] to derive project office symbols; sees all data
- **Administrator** → full access to everything; hidden from all user lists

## ResourceAssignment
**File**: `ProjectManagement/Models/ResourceAssignment.cs`

Maps a `User` to a leaf [[WorkBreakdownItem]].

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `string` | GUID |
| `WorkItemId` | `string` | FK to WorkBreakdownItem.Id |
| `DeveloperId` | `string` | FK to User.Id |
| `Role` | `AssignmentRole` | Primary, Support, Reviewer |

## Related Pages
- [[User Management]] — CRUD UI
- [[Resource Gantt]] — resource allocation view
- [[Developer Portal]] — role-filtered dashboard
- [[DataService]] — user persistence
- [[Authentication & Deployment]] — login flow and Windows auto-login
