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
| `ManagedProjectManagerIds` | `List<string>` | For SectionChief: which PMs they manage |

## Role Enum
**File**: `ProjectManagement/Models/Role.cs`

| Value | Purpose |
|-------|---------|
| `Developer` | Assigned to tasks, sees [[Developer Portal]] |
| `ProjectManager` | Full access, manages projects |
| `SectionChief` | Oversees PMs, used for project office derivation |

## Role-Based Behavior
- **Developer** → auto-routed to [[Developer Portal]] on login
- **ProjectManager** → default [[Dashboard]] view
- **SectionChief** → used by [[Resource Gantt]] to derive project office symbols

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
