using System.Windows;
using WpfResourceGantt.ProjectManagement;
namespace WpfResourceGantt.ProjectManagement.Models
{
    public enum ColumnType
    {
        Health,
        SV,
        CV,
        Predecessors,
        Float
    }

    public class GanttColumn : ViewModelBase // Inherit ViewModelBase to update UI on changes
    {
        public string Id { get; set; }

        private string _header;
        public string Header
        {
            get => _header;
            set { _header = value; OnPropertyChanged(); }
        }

        private double _width;
        public double Width
        {
            get => _width;
            set { _width = value; OnPropertyChanged(); }
        }

        private ColumnType _type;
        public ColumnType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        // NEW: Identifies the "Add New Column" slot
        public bool IsPlaceholder { get; set; }
    }
}
