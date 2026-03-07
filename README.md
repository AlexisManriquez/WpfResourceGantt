# WpfResourceGantt

A WPF-based Project Management and Resource Allocation application with Gantt chart visualization.

## Features

- **Resource Allocation**: Manage and assign developers to tasks.
- **Gantt Chart**: Visualize project schedules and resource utilization.
- **Reporting**: Generate PDF reports using QuestPDF.
- **Data Integration**: Supports Excel and MS Project interoperability.

## Tech Stack

- **Framework**: .NET 8.0 (WPF)
- **Database**: SQL Server (Entity Framework Core)
- **Charts**: LiveCharts.Wpf
- **Reports**: QuestPDF
- **Configuration**: Microsoft.Extensions.Configuration

## Prerequisites

- .NET 8.0 SDK
- SQL Server (or LocalDB)
- Microsoft Office (for Excel/MS Project interop features)

## Getting Started

1. Clone the repository.
2. Update `appsettings.json` with your database connection string.
3. Run `dotnet restore` to install dependencies.
4. Run `dotnet build` to compile the project.
5. Launch the application from Visual Studio or using `dotnet run`.
