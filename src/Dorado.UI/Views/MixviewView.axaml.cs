using System;
using System.Collections.Specialized;
using Avalonia.Controls;
using Dorado.UI.ViewModels;

namespace Dorado.UI.Views;

public partial class MixviewView : UserControl
{
    public MixviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += (s, e) => RecalculateLayout();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MixviewViewModel vm)
        {
            vm.Satellites.CollectionChanged += OnSatellitesChanged;
            RecalculateLayout();
        }
    }

    private void OnSatellitesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RecalculateLayout();
    }

    private void RecalculateLayout()
    {
        double width = Bounds.Width > 0 ? Bounds.Width : 1100;
        double height = Bounds.Height > 0 ? Bounds.Height : 700;

        double centerX = width / 2.0;
        double centerY = height / 2.0;

        var centerBorder = this.FindControl<Border>("CenterNodeBorder");
        if (centerBorder != null)
        {
            Canvas.SetLeft(centerBorder, Math.Max(0, centerX - 90));
            Canvas.SetTop(centerBorder, Math.Max(0, centerY - 75));
        }

        if (DataContext is MixviewViewModel vm)
        {
            foreach (var sat in vm.Satellites)
            {
                sat.X = centerX + sat.RelativeX - 75;
                sat.Y = centerY + sat.RelativeY - 35;
            }
        }
    }
}
