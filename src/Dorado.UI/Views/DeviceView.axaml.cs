using System;
using Avalonia.Controls;
using Dorado.UI.ViewModels;

namespace Dorado.UI.Views;

public partial class DeviceView : UserControl
{
    public DeviceView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is DeviceViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(DeviceViewModel.GasGaugeColumns) || string.IsNullOrEmpty(args.PropertyName))
                {
                    UpdateGasGauge(vm);
                }
            };
            UpdateGasGauge(vm);
        }
    }

    private void UpdateGasGauge(DeviceViewModel vm)
    {
        var grid = this.FindControl<Grid>("GasGaugeGrid");
        if (grid != null && !string.IsNullOrWhiteSpace(vm.GasGaugeColumns))
        {
            try
            {
                grid.ColumnDefinitions = ColumnDefinitions.Parse(vm.GasGaugeColumns);
            }
            catch
            {
                grid.ColumnDefinitions = ColumnDefinitions.Parse("1*,1*,1*,1*,1*,5*");
            }
        }
    }
}
