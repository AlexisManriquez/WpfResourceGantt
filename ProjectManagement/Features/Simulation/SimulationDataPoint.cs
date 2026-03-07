using System;
using WpfResourceGantt.ProjectManagement.ViewModels;

namespace WpfResourceGantt.ProjectManagement.Features.Simulation
{
    public class SimulationDataPoint : ViewModelBase
    {
        public int WeekNumber { get; set; }
        public DateTime Date { get; set; }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set
            {
                // Rounding to 3 decimal places (0.1%) prevents math jitter during drags
                _progress = Math.Round(value, 3);
                OnPropertyChanged();
            }
        }

        private double _actualHours;
        public double ActualHours
        {
            get => _actualHours;
            set
            {
                _actualHours = Math.Round(value, 1);
                OnPropertyChanged();
            }
        }
    }

    public enum GraphEditMode
    {
        Progress,
        ActualHours
    }
}
