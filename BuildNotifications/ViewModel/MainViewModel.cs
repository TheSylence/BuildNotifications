﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BuildNotifications.Core;
using BuildNotifications.Core.Pipeline;
using BuildNotifications.ViewModel.GroupDefinitionSelection;
using BuildNotifications.ViewModel.Overlays;
using BuildNotifications.ViewModel.Settings;
using BuildNotifications.ViewModel.Tree;
using BuildNotifications.ViewModel.Utils;
using TweenSharp.Animation;
using TweenSharp.Factory;

namespace BuildNotifications.ViewModel
{
    public class MainViewModel : BaseViewModel
    {
        private const string ConfigFileName = "config.json";
#if DEBUG
        private string ConfigFilePath => ConfigFileName;
#else
        private string ConfigFilePath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), $"BuildNotifications{System.IO.Path.DirectorySeparatorChar}{ConfigFileName}");
#endif

        public MainViewModel()
        {
            _coreSetup = new CoreSetup(ConfigFilePath);
            Initialize();
        }

        public BuildTreeViewModel BuildTree
        {
            get => _buildTree;
            set
            {
                _buildTree = value;
                OnPropertyChanged();
            }
        }

        public BaseViewModel Overlay
        {
            get => _overlay;
            set
            {
                _overlay = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TitleBarToolsVisibility));
            }
        }

        public StatusIndicatorViewModel StatusIndicator { get; set; }

        public Visibility TitleBarToolsVisibility => Overlay == null ? Visibility.Visible : Visibility.Collapsed;

        public GroupAndSortDefinitionsViewModel GroupAndSortDefinitionsSelection { get; set; }

        public SearchViewModel SearchViewModel { get; set; }

        public SettingsViewModel SettingsViewModel { get; set; }

        public bool ShowGroupDefinitionSelection
        {
            get => _showGroupDefinitionSelection;
            set
            {
                _showGroupDefinitionSelection = value;
                OnPropertyChanged();
            }
        }

        public bool ShowSettings
        {
            get => _showSettings;
            set
            {
                _showSettings = value;
                OnPropertyChanged();
            }
        }

        public ICommand ToggleGroupDefinitionSelectionCommand { get; set; }
        public ICommand ToggleShowSettingsCommand { get; set; }

        private void Initialize()
        {
            SetupViewModel();
            LoadProjects();
            _coreSetup.PipelineUpdated += CoreSetup_PipelineUpdated;
            _coreSetup.ErrorOccurred += CoreSetupErrorOccurred;
            ShowOverlay();
            if (Overlay == null)
                StartUpdating();
        }

        private void ShowOverlay()
        {
            var nothingConfigured = !_coreSetup.Configuration.Projects.Any() || !_coreSetup.Configuration.Connections.Any();
            if (!nothingConfigured)
                return;

            ShowInitialSetupOverlayViewModel();
        }

        private void LoadProjects()
        {
            _coreSetup.Pipeline.ClearProjects();
            var projectProvider = _coreSetup.ProjectProvider;
            foreach (var project in projectProvider.AllProjects())
            {
                _coreSetup.Pipeline.AddProject(project);
            }
        }

        private void SetupViewModel()
        {
            SearchViewModel = new SearchViewModel();
            StatusIndicator = new StatusIndicatorViewModel();
            SettingsViewModel = new SettingsViewModel(_coreSetup.Configuration, () => _coreSetup.PersistConfigurationChanges());
            SettingsViewModel.EditConnectionsRequested += SettingsViewModelOnEditConnectionsRequested;

            GroupAndSortDefinitionsSelection = new GroupAndSortDefinitionsViewModel();
            GroupAndSortDefinitionsSelection.BuildTreeGroupDefinition = _coreSetup.Configuration.GroupDefinition;
            GroupAndSortDefinitionsSelection.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(GroupAndSortDefinitionsViewModel.BuildTreeGroupDefinition))
                {
                    _coreSetup.Configuration.GroupDefinition = GroupAndSortDefinitionsSelection.BuildTreeGroupDefinition;
                    _coreSetup.PersistConfigurationChanges();
                    UpdateNow();
                }

                if (args.PropertyName == nameof(GroupAndSortDefinitionsViewModel.BuildTreeSortingDefinition))
                    BuildTree.SortingDefinition = GroupAndSortDefinitionsSelection.BuildTreeSortingDefinition;
            };

            ToggleGroupDefinitionSelectionCommand = new DelegateCommand(ToggleGroupDefinitionSelection);
            ToggleShowSettingsCommand = new DelegateCommand(ToggleShowSettings);
        }

        private void SettingsViewModelOnEditConnectionsRequested(object sender, EventArgs e)
        {
            ToggleShowSettingsCommand.Execute(null);
            ShowInitialSetupOverlayViewModel();
        }

        private void ShowInitialSetupOverlayViewModel()
        {
            if (Overlay != null)
                return;
            var vm = new InitialSetupOverlayViewModel(SettingsViewModel, _coreSetup.PluginRepository);
            vm.CloseRequested += InitialSetup_CloseRequested;

            Overlay = vm;
        }

        private void InitialSetup_CloseRequested(object sender, InitialSetupEventArgs e)
        {
            if (!(sender is InitialSetupOverlayViewModel vm))
                return;

            vm.CloseRequested -= InitialSetup_CloseRequested;
            var tween = vm.Tween(x => x.Opacity).To(0).In(0.5).Ease(Easing.ExpoEaseOut).OnComplete((timeline, parameter) =>
            {
                Overlay = null;

                // when connections or projects changed or the update is stopped. Now is the time to reload and restart the pipeline
                // as the user either changed or checked the critical settings
                if (e.ProjectOrConnectionsChanged || !_keepUpdating)
                {
                    ResetError();
                    StopUpdating();
                    LoadProjects();
                }

                StartUpdating();
            });

            App.GlobalTweenHandler.Add(tween);
        }

        private async void CoreSetup_PipelineUpdated(object sender, PipelineUpdateEventArgs e)
        {
            var buildTreeViewModelFactory = new BuildTreeViewModelFactory();

            var buildTreeViewModel = await buildTreeViewModelFactory.ProduceAsync(e.Tree, BuildTree);
            if (buildTreeViewModel != BuildTree)
                BuildTree = buildTreeViewModel;

            BuildTree.SortingDefinition = GroupAndSortDefinitionsSelection.BuildTreeSortingDefinition;
        }

        private void CoreSetupErrorOccurred(object sender, PipelineErrorEventArgs e)
        {
            StopUpdating();
            StatusIndicator.UpdateStatus = UpdateStatus.Error;
        }

        private void ToggleGroupDefinitionSelection(object obj)
        {
            ShowGroupDefinitionSelection = !ShowGroupDefinitionSelection;
        }

        private void ToggleShowSettings(object obj)
        {
            ShowSettings = !ShowSettings;
        }

        private void ResetError()
        {
            if (StatusIndicator.UpdateStatus != UpdateStatus.Error)
                return;

            StatusIndicator.UpdateStatus = UpdateStatus.None;

        }

        private async Task UpdateTimer()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            while (_keepUpdating)
            {
                if (StatusIndicator.UpdateStatus == UpdateStatus.None)
                    StatusIndicator.UpdateStatus = UpdateStatus.Busy;

                await _coreSetup.Update();

                if (StatusIndicator.UpdateStatus == UpdateStatus.Busy)
                    StatusIndicator.UpdateStatus = UpdateStatus.None;
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), _cancellationTokenSource.Token);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        private void StartUpdating()
        {
            if (_keepUpdating)
                return;
            _keepUpdating = true;
            UpdateTimer().FireAndForget();
        }

        private void StopUpdating()
        {
            _keepUpdating = false;
            UpdateNow(); // cancels the wait timer
        }

        private void UpdateNow()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        private CancellationTokenSource _cancellationTokenSource;
        private bool _keepUpdating;
        private readonly CoreSetup _coreSetup;
        private BuildTreeViewModel _buildTree;
        private bool _showGroupDefinitionSelection;
        private bool _showSettings;
        private BaseViewModel _overlay;
    }
}