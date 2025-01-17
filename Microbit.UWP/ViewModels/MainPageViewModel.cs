﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Devices.Enumeration;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Views;
using GalaSoft.MvvmLight.Command;

using Microbit.UWP.Models;
using Microbit.UWP.Services;
using GalaSoft.MvvmLight.Threading;
using GalaSoft.MvvmLight.Messaging;
using Windows.UI.Xaml.Controls;
using Microsoft.Toolkit.Uwp;

namespace Microbit.UWP.ViewModels
{
    public class MainPageViewModel : ViewModelBase
    {
        private DeviceWatcher deviceWatcher;

        private IDialogService _dialogService;
        private INavigationService _navigate;

        public MainPageViewModel(IDialogService dialogService, INavigationService navigation)
        {
            _dialogService = dialogService;
            _navigate = navigation;

            StartBleDeviceWatcher();
        }
        #region Properties

        private string _statusContent = default(string);
        public string StatusContent
        {
            get { return _statusContent; }
            set
            {
                Set(ref _statusContent, value);
                RaisePropertyChanged(nameof(StatusContent));
            }
        }
        private ObservableCollection<ObservableBluetoothLEDevice> _resultCollection = new ObservableCollection<ObservableBluetoothLEDevice>();
        public ObservableCollection<ObservableBluetoothLEDevice> ResultCollection
        {
            get { return _resultCollection; }
            set
            {
                _resultCollection = value;
                RaisePropertyChanged(nameof(ResultCollection));
            }
        }

        #endregion


        #region Discovery Bluetooth LE Devices
        private void StartBleDeviceWatcher()
        {
            StatusContent = "开始搜索周边蓝牙设备...";

            // Additional properties we would like about the device.
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };

            // BT_Code: Currently Bluetooth APIs don't provide a selector to get ALL devices that are both paired and non-paired.
            deviceWatcher =
                    DeviceInformation.CreateWatcher(
                        "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")",
                        requestedProperties,
                        DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.Removed += DeviceWatcher_Removed;
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher.Stopped += DeviceWatcher_Stopped;

            // Start over with an empty collection.
            ResultCollection.Clear();

            // Start the watcher.
            deviceWatcher.Start();
        }

        private void StopBleDeviceWatcher()
        {
            if (deviceWatcher != null)
            {
                // Unregister the event handlers.
                deviceWatcher.Added -= DeviceWatcher_Added;
                deviceWatcher.Updated -= DeviceWatcher_Updated;
                deviceWatcher.Removed -= DeviceWatcher_Removed;
                deviceWatcher.EnumerationCompleted -= DeviceWatcher_EnumerationCompleted;
                deviceWatcher.Stopped -= DeviceWatcher_Stopped;

                // Stop the watcher.
                deviceWatcher.Stop();
                deviceWatcher = null;
            }


        }

        private async void DeviceWatcher_Stopped(DeviceWatcher sender, object args)
        {
            await GalaSoft.MvvmLight.Threading.DispatcherHelper.RunAsync(() =>
            {
                // We must update the collection on the UI thread because the collection is databound to a UI element.
                if (sender == deviceWatcher)
                {
                    StatusContent = $"No longer watching for devices." + sender.Status;
                }
            });
        }

        private async void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            await GalaSoft.MvvmLight.Threading.DispatcherHelper.RunAsync(() =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    StatusContent = ResultCollection.Count() + "devices found. Enumeration completed.";
                }
            });
        }

        private async void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await GalaSoft.MvvmLight.Threading.DispatcherHelper.RunAsync(() =>
            {
                // Find the corresponding DeviceInformation in the collection and remove it.
                ObservableBluetoothLEDevice bleDeviceDisplay = FindBluetoothLEDeviceDisplay(args.Id);
                if (bleDeviceDisplay != null)
                {
                    ResultCollection.Remove(bleDeviceDisplay);
                }
            });
        }
        private ObservableBluetoothLEDevice FindBluetoothLEDeviceDisplay(string id)
        {
            foreach (ObservableBluetoothLEDevice bleDeviceDisplay in ResultCollection)
            {
                if (bleDeviceDisplay.DeviceInfo.Id == id)
                {
                    return bleDeviceDisplay;
                }
            }
            return null;
        }

        private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await GalaSoft.MvvmLight.Threading.DispatcherHelper.RunAsync(() =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    ObservableBluetoothLEDevice bleDeviceDisplay = FindBluetoothLEDeviceDisplay(args.Id);
                    if (bleDeviceDisplay != null)
                    {
                        bleDeviceDisplay.UpdateAsync(args);
                    }
                }
            });
        }

        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            await GalaSoft.MvvmLight.Threading.DispatcherHelper.RunAsync(() =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    // Make sure device name isn't blank or already present in the list.
                    if (deviceInfo.Name != string.Empty && FindBluetoothLEDeviceDisplay(deviceInfo.Id) == null)
                    {
                        var bleDevice = new ObservableBluetoothLEDevice(deviceInfo);
                        _resultCollection.Add(bleDevice);
                    }
                }
            });
        }
        #endregion

        #region Pairing Devices
        private bool isBusy = false;
        private RelayCommand<object> _getDeviceInfoCommmand;
        public RelayCommand<object> GetDeviceInfoCommmand => _getDeviceInfoCommmand ?? (
            _getDeviceInfoCommmand = new RelayCommand<object>((s) =>
            {
                if (null != s)
                {
                    GetDeviceInfo(s);
                }
            }));

        private RelayCommand<object> _readBLEDeviceCommand;
        public RelayCommand<object> ReadBLEDeviceCommand => _readBLEDeviceCommand ?? (
            _readBLEDeviceCommand = new RelayCommand<object>((s) =>
            {
                if (!string.IsNullOrEmpty(s.ToString()))
                {
                    var address = s.ToString();

                }
            }));

        private async void GetDeviceInfo(object obj)
        {
            // Do not allow a new Pair operation to start if an existing one is in progress.
            if (isBusy)
            {
                return;
            }
            isBusy = true;
            StatusContent = "正在配对中,请稍等...";
            // Capture the current selected item in case the user changes it while we are pairing.
            var selectItem = (obj as ItemClickEventArgs) == null ? obj : (obj as ItemClickEventArgs).ClickedItem;
            if (selectItem == null)
            {
                return;
            }
            var bleDeviceDisplay = selectItem as Microsoft.Toolkit.Uwp.ObservableBluetoothLEDevice;

            if (null != bleDeviceDisplay && !bleDeviceDisplay.IsPaired)
            {
                // BT_Code: Pair the currently selected device.
                DevicePairingResult result = await bleDeviceDisplay.DeviceInfo.Pairing.PairAsync();
                StatusContent = $"Pairing Result = {result.Status}";

                if (result.Status == DevicePairingResultStatus.Paired ||
                    result.Status == DevicePairingResultStatus.AlreadyPaired)
                {

                    StopBleDeviceWatcher();

                    await bleDeviceDisplay.ConnectAsync();
                    var services = bleDeviceDisplay.Services;

                    StatusContent = $"find  {services.Count}  services...";

                    //Messenger.Default.Send<ObservableBluetoothLEDevice>(bleDeviceDisplay);
                    //_navigate.NavigateTo("DevicePage");
                }
            }
            else
            {
                DeviceUnpairingResult dpResult = await bleDeviceDisplay.DeviceInfo.Pairing.UnpairAsync();
                if (dpResult.Status == DeviceUnpairingResultStatus.Unpaired)
                {
                    StatusContent = "解除匹配成功";
                }
            }
            isBusy = false;
        }
        #endregion 

    }
}
