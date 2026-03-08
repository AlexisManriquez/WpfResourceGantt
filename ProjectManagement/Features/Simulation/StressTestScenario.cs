namespace WpfResourceGantt.ProjectManagement.Features.Simulation
{
    /// <summary>
    /// Represents a pre-built stress test scenario that can be executed with one click.
    /// </summary>
    public class StressTestScenario
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }

        public override string ToString() => Name;
    }
}
