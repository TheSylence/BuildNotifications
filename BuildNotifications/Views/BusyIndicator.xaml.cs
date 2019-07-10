﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BuildNotifications.ViewModel;
using BuildNotifications.ViewModel.Utils;

namespace BuildNotifications.Views
{
    public partial class BusyIndicator : UserControl
    {
        public BusyIndicator()
        {
            InitializeComponent();
            DummyItems = new RemoveTrackingObservableCollection<DummyItem>(TimeSpan.FromSeconds(1), new[]
            {
                new DummyItem()
            });
            _tokenSource = new CancellationTokenSource();
        }

        public RemoveTrackingObservableCollection<DummyItem> DummyItems { get; set; }

        public bool IsBusy
        {
            get => (bool) GetValue(IsBusyProperty);
            set => SetValue(IsBusyProperty, value);
        }

        private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.Property.Equals(IsBusyProperty) && d is BusyIndicator busyIndicator)
            {
                if ((bool) e.NewValue)
                    busyIndicator.StartTask();
                else
                    busyIndicator.StopTask();
            }
        }

        private void StartTask()
        {
            if (_removeAndAddingTask != null)
                return;

            _removeAndAddingTask = Task.Run(async () =>
            {
                while (!_tokenSource.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), _tokenSource.Token);
                    }
                    catch (Exception)
                    {
                        break;
                    }

                    var firstItem = DummyItems.FirstOrDefault(x => !x.IsRemoving);
                    if (!_tokenSource.IsCancellationRequested)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            DummyItems.Remove(firstItem);
                            DummyItems.Add(new DummyItem());
                        });
                    }
                }
            }, _tokenSource.Token);
        }

        private void StopTask()
        {
            if (_removeAndAddingTask == null || _removeAndAddingTask.Status == TaskStatus.WaitingForActivation)
                return;

            _tokenSource.Cancel();
            _removeAndAddingTask.Wait();
            _removeAndAddingTask = null;
            _tokenSource = new CancellationTokenSource();
        }

        private Task _removeAndAddingTask;
        private CancellationTokenSource _tokenSource;

        public static readonly DependencyProperty IsBusyProperty = DependencyProperty.Register(
            "IsBusy", typeof(bool), typeof(BusyIndicator), new PropertyMetadata(default(bool), PropertyChangedCallback));

        public class DummyItem : BaseViewModel
        {
        }
    }
}