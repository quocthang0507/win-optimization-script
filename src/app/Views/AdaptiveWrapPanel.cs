namespace WinOptimizationApp.Views;

/// <summary>Wraps toolbars using the available content width, including after a resize.</summary>
public sealed partial class AdaptiveWrapPanel : Panel
{
    public double Spacing { get; set; } = 10;

    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
    {
        foreach (var child in Children)
            child.Measure(new Windows.Foundation.Size(availableSize.Width, double.PositiveInfinity));
        return Layout(availableSize.Width, arrange: false);
    }

    protected override Windows.Foundation.Size ArrangeOverride(Windows.Foundation.Size finalSize)
    {
        Layout(finalSize.Width, arrange: true);
        return finalSize;
    }

    private Windows.Foundation.Size Layout(double width, bool arrange)
    {
        double x = 0, y = 0, rowHeight = 0, usedWidth = 0;
        var row = new List<(UIElement Child, double X, double Width, double Height)>();
        void ArrangeRow()
        {
            foreach (var item in row)
                item.Child.Arrange(new Windows.Foundation.Rect(item.X, y + rowHeight - item.Height, item.Width, item.Height));
            row.Clear();
        }
        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            var size = child.DesiredSize;
            var childWidth = Math.Min(size.Width, width);
            if (x > 0 && x + childWidth > width)
            {
                if (arrange) ArrangeRow();
                x = 0;
                y += rowHeight + Spacing;
                rowHeight = 0;
            }
            if (arrange)
                row.Add((child, x, childWidth, size.Height));
            usedWidth = Math.Max(usedWidth, x + childWidth);
            x += childWidth + Spacing;
            rowHeight = Math.Max(rowHeight, size.Height);
        }
        if (arrange) ArrangeRow();
        return new Windows.Foundation.Size(usedWidth, y + rowHeight);
    }
}
