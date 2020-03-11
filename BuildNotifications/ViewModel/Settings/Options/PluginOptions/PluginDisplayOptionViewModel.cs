﻿using BuildNotifications.PluginInterfaces.Configuration.Options;

namespace BuildNotifications.ViewModel.Settings.Options.PluginOptions
{
    internal class PluginDisplayOptionViewModel : OptionViewModelBase, IPluginOptionViewModel
    {
        public PluginDisplayOptionViewModel(IOption option, ILocalizationProvider localizationProvider)
            : base(option.NameTextId, option.DescriptionTextId)
        {
            _pluginOptionViewModelImplementation = new PluginOptionViewModelImplementation(option, localizationProvider, this);
            Value = option.ToString() ?? string.Empty;
        }

        public override string Description => _pluginOptionViewModelImplementation.Description;
        public override string DisplayName => _pluginOptionViewModelImplementation.DisplayName;
        public override bool IsEnabled => _pluginOptionViewModelImplementation.IsEnabled;
        public override bool IsLoading => _pluginOptionViewModelImplementation.IsLoading;
        public override bool IsVisible => _pluginOptionViewModelImplementation.IsVisible;

        public string Value { get; }
        private readonly PluginOptionViewModelImplementation _pluginOptionViewModelImplementation;
    }
}