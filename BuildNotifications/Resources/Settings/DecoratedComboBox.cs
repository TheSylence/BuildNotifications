﻿using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using BuildNotifications.Resources.Icons;
using JetBrains.Annotations;

namespace BuildNotifications.Resources.Settings
{
    internal class DecoratedComboBox : ComboBox, INotifyPropertyChanged
    {
        public DecoratedComboBox()
        {
            GotFocus += OnGotFocus;
        }

        private bool _toggleButtonActive = true;

        public bool ToggleButtonActive
        {
            get => _toggleButtonActive;
            set
            {
                _toggleButtonActive = value;
                OnPropertyChanged();
            }
        }

        private bool _itemsSourceCountIsSet;

        public bool IsEmpty => _itemsSourceCountIsSet && ItemsSourceCount == 0;

        public IconType Icon
        {
            get => (IconType) GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public string Label
        {
            get => (string) GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (!Equals(e.Source, this))
                return;
            IsDropDownOpen = true;
        }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            "Icon", typeof(IconType), typeof(DecoratedComboBox), new PropertyMetadata(IconType.DownArrow));

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            "Label", typeof(string), typeof(DecoratedComboBox), new PropertyMetadata(default(string)));

        public static readonly DependencyProperty ItemsSourceCountProperty = DependencyProperty.Register(
            "ItemsSourceCount", typeof(int), typeof(DecoratedComboBox), new PropertyMetadata(default(int), PropertyChangedCallback));

        private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DecoratedComboBox decoratedComboBox)
                decoratedComboBox.UpdateBorderVisibility();
        }

        private void UpdateBorderVisibility()
        {
            ToggleButtonActive = ItemsSourceCount > 1;
            _itemsSourceCountIsSet = true;
            OnPropertyChanged(nameof(IsEmpty));
            if (ItemsSourceCount == 1)
                SelectedIndex = 0;
        }

        public int ItemsSourceCount
        {
            get => (int) GetValue(ItemsSourceCountProperty);
            set => SetValue(ItemsSourceCountProperty, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}