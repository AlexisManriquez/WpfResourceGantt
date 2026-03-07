using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfResourceGantt.ProjectManagement.Adorners
{
    public class DragAdorner : Adorner
    {
        private readonly ContentPresenter _contentPresenter;
        private double _left;
        private double _top;
        private AdornerLayer _adornerLayer;
        public DragAdorner(UIElement adornedElement, object data) : base(adornedElement)
        {
            // --- FIX 1: Cast the UIElement to a FrameworkElement ---
            // This gives us access to FindResource.
            var frameworkElement = adornedElement as FrameworkElement;
            if (frameworkElement == null) return;

            // Store the layer for future invalidations.
            _adornerLayer = AdornerLayer.GetAdornerLayer(adornedElement);

            _contentPresenter = new ContentPresenter
            {
                Content = data,
                Opacity = 0.7,
                // Now this will compile correctly.
                ContentTemplate = frameworkElement.FindResource("DragAdornerTemplate") as DataTemplate
            };
        }

        public void SetPosition(double left, double top)
        {
            _left = left;
            _top = top;
            UpdatePosition();
        }

        // --- FIX 2: Implement the missing UpdatePosition method ---
        private void UpdatePosition()
        {
            // Invalidate the visual arrangement of the adorner layer.
            // This is a more robust way to force a redraw at the new position.
            _adornerLayer?.Update(AdornedElement);
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _contentPresenter;

        protected override Size MeasureOverride(Size constraint)
        {
            _contentPresenter.Measure(constraint);
            return _contentPresenter.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _contentPresenter.Arrange(new Rect(finalSize));
            return finalSize;
        }

        public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
        {
            var result = new GeneralTransformGroup();
            result.Children.Add(base.GetDesiredTransform(transform));
            result.Children.Add(new TranslateTransform(_left, _top));
            return result;
        }
    }
}
