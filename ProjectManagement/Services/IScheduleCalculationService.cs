using System.Collections.Generic;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Services
{
    public interface IScheduleCalculationService
    {
        void CalculateSchedule(IEnumerable<SystemItem> systems);
    }
}
