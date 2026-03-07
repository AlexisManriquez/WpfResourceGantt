using System.Collections.Generic;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Services
{
    public interface IResourceAnalysisService
    {
        /// <summary>
        /// Analyzes resource allocations across the project to detect over-allocations.
        /// </summary>
        /// <param name="systems">The item hierarchy.</param>
        /// <param name="users">The project users.</param>
        void AnalyzeResources(IEnumerable<SystemItem> systems, IEnumerable<User> users);
    }
}
