using System.Text.Json;

namespace WpfResourceGantt.ProjectManagement.Features.Simulation
{
    public static class CloneHelper
    {
        /// <summary>
        /// Performs a deep clone of an object using JSON serialization.
        /// This creates a completely disconnected graph in memory for the sandbox.
        /// </summary>
        public static T DeepClone<T>(T source)
        {
            if (source == null) return default;
            var json = JsonSerializer.Serialize(source);
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}
