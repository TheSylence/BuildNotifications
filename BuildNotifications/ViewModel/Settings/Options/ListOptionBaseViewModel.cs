﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace BuildNotifications.ViewModel.Settings.Options
{
    public abstract class ListOptionBaseViewModel<TItem> : OptionViewModelBase<TItem>
    {
        protected ListOptionBaseViewModel(string displayName, TItem value = default)
            : base(value, displayName)
        {
            _initialValue = value;
        }

        public IEnumerable<ListOptionItemViewModel<TItem>> AvailableValues
        {
            get
            {
                Init();
                return _availableValues;
            }
        }

        public ListOptionItemViewModel<TItem>? SelectedValue
        {
            get => _selectedValue;
            set
            {
                if (Equals(_selectedValue, value))
                    return;

                _selectedValue = value;
                if (_selectedValue != null)
                    Value = _selectedValue.Value;
            }
        }

        protected abstract IEnumerable<TItem> ModelValues { get; }

        protected virtual string DisplayNameFor(TItem item) => item?.ToString() ?? string.Empty;

        protected void InvalidateAvailableValues()
        {
            _shouldFetchValues = true;
            OnPropertyChanged(nameof(AvailableValues));
        }

        private void Init()
        {
            if (!_shouldFetchValues)
                return;

            _availableValues.Clear();
            foreach (var option in ModelValues.Select(v => new ListOptionItemViewModel<TItem>(v, DisplayNameFor(v))))
            {
                _availableValues.Add(option);
            }

            _shouldFetchValues = true;

            if (!_initialized)
            {
                _initialized = true;
                SelectedValue = AvailableValues.FirstOrDefault(v => Equals(v.Value, _initialValue));
            }
        }

        private readonly TItem _initialValue;
        private readonly ObservableCollection<ListOptionItemViewModel<TItem>> _availableValues = new ObservableCollection<ListOptionItemViewModel<TItem>>();
        private bool _shouldFetchValues = true;
        private ListOptionItemViewModel<TItem>? _selectedValue;
        private bool _initialized;
    }
}