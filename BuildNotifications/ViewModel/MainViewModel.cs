﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using BuildNotifications.Core;
using BuildNotifications.Core.Pipeline;
using BuildNotifications.ViewModel.GroupDefinitionSelection;
using BuildNotifications.ViewModel.Settings;
using BuildNotifications.ViewModel.Tree;
using BuildNotifications.ViewModel.Utils;

namespace BuildNotifications.ViewModel
{
    public class MainViewModel : BaseViewModel
    {
        public MainViewModel()
        {
            _coreSetup = new CoreSetup();

            SearchViewModel = new SearchViewModel();
            SettingsViewModel = new SettingsViewModel();

            GroupAndSortDefinitionsSelection = new GroupAndSortDefinitionsViewModel();
            GroupAndSortDefinitionsSelection.BuildTreeGroupDefinition = _coreSetup.Configuration.GroupDefinition;
            GroupAndSortDefinitionsSelection.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(GroupAndSortDefinitionsViewModel.BuildTreeGroupDefinition))
                {
                    Debug.WriteLine("Selected groups: " + string.Join(',', GroupAndSortDefinitionsSelection.BuildTreeGroupDefinition));

                    _coreSetup.Configuration.GroupDefinition = GroupAndSortDefinitionsSelection.BuildTreeGroupDefinition;
                }

                if (args.PropertyName == nameof(GroupAndSortDefinitionsViewModel.BuildTreeSortingDefinition))
                {
                    Debug.WriteLine("Selected sortings: " + string.Join(',', GroupAndSortDefinitionsSelection.BuildTreeSortingDefinition));
                    BuildTree.SortingDefinition = GroupAndSortDefinitionsSelection.BuildTreeSortingDefinition;
                }
            };

            ToggleGroupDefinitionSelectionCommand = new DelegateCommand(ToggleGroupDefinitionSelection);
            ToggleShowSettingsCommand = new DelegateCommand(ToggleShowSettings);

            var projectProvider = _coreSetup.ProjectProvider;
            foreach (var project in projectProvider.AllProjects())
            {
                _coreSetup.Pipeline.AddProject(project);
            }

            _coreSetup.PipelineUpdated += CoreSetup_PipelineUpdated;

            UpdateTimer().FireAndForget();
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

        private void CoreSetup_PipelineUpdated(object sender, PipelineUpdateEventArgs e)
        {
            var buildTreeViewModelFactory = new BuildTreeViewModelFactory();

            BuildTree = buildTreeViewModelFactory.Produce(e.Tree, BuildTree);
            BuildTree.SortingDefinition = GroupAndSortDefinitionsSelection.BuildTreeSortingDefinition;
        }

        // RemoveChildrenIfNotOfType ensures that only elements of the type are within the list, therefore the ReSharper warning is taken care of

        private void ToggleGroupDefinitionSelection(object obj)
        {
            ShowGroupDefinitionSelection = !ShowGroupDefinitionSelection;
        }

        private void ToggleShowSettings(object obj)
        {
            ShowSettings = !ShowSettings;
        }

        private async Task UpdateTimer()
        {
            while (true)
            {
                IsBusy = true;
                await _coreSetup.Update();
                IsBusy = false;

                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            // ReSharper disable once FunctionNeverReturns
        }

        private readonly CoreSetup _coreSetup;
        private BuildTreeViewModel _buildTree;
        private bool _showGroupDefinitionSelection;
        private bool _showSettings;
    }
}