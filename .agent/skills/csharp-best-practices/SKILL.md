---
name: csharp-best-practices
description: Modern C# guidelines (C# 10+), LINQ optimization, async hygiene, and memory management. Use this to validate logic, services, and algorithm implementations.
---

# C# Best Practices & Standards

## 1. Async/Await Hygiene
Asynchronous programming is critical in WPF to keep the UI responsive.

### 🛑 The Golden Rule: No `async void`
`async void` methods cannot be awaited and crash the process if an exception occurs.
- **Exception:** Top-level Event Handlers (e.g., `Button_Click`) are the *only* place `async void` is permitted.
- **Solution:** Use `ICommand` / `RelayCommand` which handles async delegates safely.

### ⚠️ Deadlock Prevention
- **UI Context:** In ViewModels, you generally *want* to return to the UI thread, so `await task` is fine.
- **Library/Service Code:** In non-UI services (e.g., `FileService`), use `.ConfigureAwait(false)` to avoid capturing the synchronization context unnecessarily.

### ⏱️ Cancellation
Long-running operations must accept a `CancellationToken`.
```csharp
// ✅ Correct
public async Task<Data> FetchDataAsync(int id, CancellationToken token = default) {
    var response = await _client.GetAsync($"api/{id}", token); // Pass it down
    return ...
}
2. Modern Syntax (C# 10/11/12)
File-Scoped Namespaces

Reduce indentation levels.

// ❌ Old
namespace MyApp.Services
{
    public class MyService { ... }
}

// ✅ New
namespace MyApp.Services;

public class MyService { ... }
Records for DTOs

Use record types for immutable data transfer objects (View models, API responses).


// ✅ Immutable, equals-by-value, concise
public record CustomerDto(int Id, string Name, string Email);
Pattern Matching

Use modern is and switch expressions.

// ✅ Switch Expression
string statusMessage = status switch
{
    OrderStatus.Pending => "Please wait",
    OrderStatus.Shipped => "On the way",
    _ => "Unknown"
};

// ✅ Property Pattern
if (obj is Customer { IsActive: true, Balance: > 1000 }) { ... }
3. LINQ & Collections
Performance

Check Existence: Use .Any() instead of .Count() > 0. Any stops at the first match; Count iterates the whole list.

Materialization: Do not use .ToList() or .ToArray() unless you need a snapshot of the data or need to iterate multiple times. Return IEnumerable<T> or IQueryable<T> (for EF Core) to defer execution.

Arrays vs Lists

Use T[] (Array) or ImmutableArray<T> for fixed collections.

Use List<T> only when size changes.

Use ObservableCollection<T> only for UI binding (WPF).

4. Null Safety

The project should have <Nullable>enable</Nullable> in the .csproj.

Assumptions: Do not ignore CS8600 warnings. Handle nulls explicitly.

Operators:

?? (Null Coalescing): string name = inputName ?? "Default";

?. (Null Conditional): int? length = inputString?.Length;

! (Null Forgiving): Use sparingly, only when you are 100% sure (e.g., Entity Framework IDs after saving).

5. Resource Management
IDisposable

Use the modern using declaration (no braces) to ensure resources are cleaned up.


// ❌ Old
using (var stream = File.OpenRead("log.txt"))
{
    // code
}

// ✅ New
using var stream = File.OpenRead("log.txt");
// code...
// stream is disposed automatically at end of scope
6. String Manipulation

Interpolation: Use $"ID: {id}" instead of string.Format or +.

StringBuilder: If concatenating inside a loop, ALWAYS use StringBuilder. Regular string concatenation creates a new object in memory for every iteration (O(N^2) memory pressure).

// ✅ Correct loop concatenation
var sb = new StringBuilder();
foreach (var item in list) {
    sb.AppendLine(item.ToString());
}
return sb.ToString();
