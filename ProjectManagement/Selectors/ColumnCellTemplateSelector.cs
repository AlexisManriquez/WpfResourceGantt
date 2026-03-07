using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Selectors
{
    public class ColumnCellTemplateSelector : DataTemplateSelector
    {
        public DataTemplate HealthTemplate { get; set; }
        public DataTemplate SVTemplate { get; set; }
        public DataTemplate CVTemplate { get; set; }
        public DataTemplate PlaceholderTemplate { get; set; }
        public DataTemplate PredecessorsTemplate { get; set; }
        public DataTemplate FloatTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is GanttColumn col)
            {
                if (col.IsPlaceholder) return PlaceholderTemplate;

                switch (col.Type)
                {
                    case ColumnType.Health: return HealthTemplate;
                    case ColumnType.SV: return SVTemplate;
                    case ColumnType.CV: return CVTemplate;
                    case ColumnType.Predecessors: return PredecessorsTemplate;
                    case ColumnType.Float: return FloatTemplate;
                }
            }
            return base.SelectTemplate(item, container);
        }
    }
}
