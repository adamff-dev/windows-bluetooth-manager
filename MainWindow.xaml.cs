using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BluetoothManager.Helpers;
using BluetoothManager.Models;
using BluetoothManager.Services;

namespace BluetoothManager
{
    public partial class MainWindow : Window
    {
        private readonly BluetoothService _service;
        private Dictionary<string, System.Timers.Timer> _saveTimers = new Dictionary<string, System.Timers.Timer>();
        private const int DEBOUNCE_MILLISECONDS = 1000; // 1 segundo de debounce
        public ObservableCollection<Device> Devices { get; } = new ObservableCollection<Device>();

        public MainWindow()
        {
            InitializeComponent();
            _service = new BluetoothService();
            DevicesGrid.ItemsSource = Devices;
            RefreshButton.Click += async (_, __) => await RefreshAsync();
            ClearSavedButton.Click += async (_, __) => await ClearSavedNames();
            SearchBox.TextChanged += (_, __) => ApplyFilter();
            Loaded += async (_, __) => await RefreshAsync();
        }

        private string GetString(string key) => StringResources.GetString(key);

        private void ApplyFilter()
        {
            var q = SearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(q))
            {
                DevicesGrid.ItemsSource = Devices;
            }
            else
            {
                DevicesGrid.ItemsSource = Devices.Where(d =>
                    (d.DisplayName ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (d.MAC ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                ).ToList();
            }
        }

        private async Task RefreshAsync()
        {
            SetStatus(GetString("StatusListingDevices"));
            Devices.Clear();
            var res = await _service.ListDevicesAsync();
            if (!res.Ok)
            {
                SetStatus($"Error: {res.Error}");
                return;
            }
            foreach (var d in res.Devices)
            {
                Devices.Add(d);
            }
            SetStatus($"{Devices.Count} {GetString("StatusDevicesListed")}");
        }

        private void SetStatus(string text)
        {
            StatusText.Text = text;
        }

        private async void SaveName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is Device device)
            {
                var name = device.DisplayName;
                var ok = await _service.SaveNameAsync(device.MAC, name);
                SetStatus(ok ? GetString("StatusNameSaved") : GetString("StatusErrorSavingName"));
            }
        }

        private void DevicesGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditingElement is TextBox textBox && e.Row.Item is Device device && e.Column.Header.ToString() == GetString("ColumnName"))
            {
                device.DisplayName = textBox.Text;
                ScheduleSaveName(device);
            }
        }

        private void ScheduleSaveName(Device device)
        {
            // Cancelar timer anterior si existe
            if (_saveTimers.ContainsKey(device.MAC))
            {
                _saveTimers[device.MAC].Stop();
                _saveTimers[device.MAC].Dispose();
                _saveTimers.Remove(device.MAC);
            }

            // Crear nuevo timer con debounce
            var timer = new System.Timers.Timer(DEBOUNCE_MILLISECONDS);
            timer.Elapsed += async (s, e) => await OnSaveNameTimer(device, s);
            timer.AutoReset = false;
            _saveTimers[device.MAC] = timer;
            timer.Start();
        }

        private async Task OnSaveNameTimer(Device device, object timerSender)
        {
            var ok = await _service.SaveNameAsync(device.MAC, device.DisplayName);
            Dispatcher.Invoke(() =>
            {
                SetStatus(ok ? GetString("StatusNameSaved") : GetString("StatusErrorSavingName"));
            });
            
            // Limpiar timer
            if (timerSender is System.Timers.Timer t)
            {
                t.Stop();
                t.Dispose();
            }
            _saveTimers.Remove(device.MAC);
        }

        private async void Pair_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is Device device)
            {
                SetStatus(string.Format(GetString("StatusPairing"), device.MAC));
                var res = await _service.PairByMacAsync(device.MAC);
                if (res.Ok)
                {
                    SetStatus(GetString("StatusPaired"));
                }
                else
                {
                    SetStatus(string.Format(GetString("StatusErrorPairing"), res.Error));
                    System.Windows.MessageBox.Show(string.Format(GetString("ErrorPairingMessage"), device.MAC, res.Error), GetString("ErrorPairingTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
                await RefreshAsync();
            }
        }

        private async void Unpair_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is Device device)
            {
                SetStatus(string.Format(GetString("StatusUnpairing"), device.MAC));
                var res = await _service.UnpairByMacAsync(device.MAC);
                if (res.Ok)
                {
                    SetStatus(GetString("StatusUnpaired"));
                }
                else
                {
                    SetStatus(string.Format(GetString("StatusErrorUnpairing"), res.Error));
                    System.Windows.MessageBox.Show(string.Format(GetString("ErrorUnpairingMessage"), device.MAC, res.Error), GetString("ErrorPairingTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
                await RefreshAsync();
            }
        }

        private async Task ClearSavedNames()
        {
            var ok = await _service.ClearAllNamesAsync();
            SetStatus(ok ? GetString("StatusNamesCleared") : GetString("StatusNamesClearedError"));
            await RefreshAsync();
        }
    }
}