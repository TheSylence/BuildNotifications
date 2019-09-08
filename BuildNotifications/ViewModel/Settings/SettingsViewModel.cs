﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using BuildNotifications.Core.Config;
using BuildNotifications.Core.Plugin;
using ReflectSettings;
using ReflectSettings.EditableConfigs;
using DelegateCommand = BuildNotifications.ViewModel.Utils.DelegateCommand;

namespace BuildNotifications.ViewModel.Settings
{
// properties *are* initialized within the constructor. However by a method call, which is not correctly recognized by the code analyzer yet.
#pragma warning disable CS8618 // warning about uninitialized non-nullable properties

    public class SettingsViewModel
    {
        public SettingsViewModel(IConfiguration configuration, Action saveMethod, IPluginRepository pluginRepository)
        {
            Configuration = configuration;
            _saveMethod = saveMethod;
            _pluginRepository = pluginRepository;
            EditConnectionsCommand = new DelegateCommand(OnEditConnections);

            CreateEditables();
        }

        public ObservableCollection<IEditableConfig> Configs { get; } = new ObservableCollection<IEditableConfig>();

        public IConfiguration Configuration { get; }

        public SettingsSubSetViewModel ConnectionsSubSet { get; private set; }

        public ICommand EditConnectionsCommand { get; set; }

        public SettingsSubSetViewModel ProjectsSubSet { get; private set; }

        public event EventHandler EditConnectionsRequested;

        public event EventHandler SettingsChanged;

        private void CreateEditables()
        {
            var factory = new SettingsFactory();
            var editables = factory.Reflect(Configuration, out var changeTrackingManager).ToList();

            var projectsEditables = new List<IEditableConfig>();

            foreach (var config in editables)
            {
                if (config.PropertyInfo.Name == nameof(IConfiguration.Connections))
                {
                    // ignored, as connections are wrapped later
                    continue;
                }

                if (config.PropertyInfo.Name == nameof(IConfiguration.Projects))
                {
                    projectsEditables.Add(config);
                    continue;
                }

                Configs.Add(config);
            }

            ConnectionsWrapper = new ConnectionsWrapperViewModel(Configuration.Connections, Configuration, _pluginRepository);
            var connectionsEditable = factory.Reflect(ConnectionsWrapper, out var connectionChangeTrackingManager);

            ConnectionsSubSet = new SettingsSubSetViewModel(connectionsEditable);
            ProjectsSubSet = new SettingsSubSetViewModel(projectsEditables);

            changeTrackingManager.ConfigurationChanged += OnConfigurationChanged;
            connectionChangeTrackingManager.ConfigurationChanged += OnConfigurationChanged;
        }

        public ConnectionsWrapperViewModel ConnectionsWrapper { get; private set; }

        private void OnConfigurationChanged(object? sender, EventArgs args)
        {
            _saveMethod.Invoke();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnEditConnections(object parameter)
        {
            EditConnectionsRequested?.Invoke(this, EventArgs.Empty);
        }

        private readonly Action _saveMethod;
        private readonly IPluginRepository _pluginRepository;
    }
}
#pragma warning enable CS8618