﻿using System;
using System.Collections.Generic;
using System.Windows.Input;
using BuildNotifications.Core.Config;
using BuildNotifications.Core.Text;
using BuildNotifications.ViewModel.Settings.Options;
using BuildNotifications.ViewModel.Utils;

namespace BuildNotifications.ViewModel.Settings
{
    public class SettingsViewModel : BaseViewModel
    {
        public SettingsViewModel(IConfiguration configuration, Action saveMethod)
        {
            _saveMethod = saveMethod;
            Configuration = configuration;
            EditConnectionsCommand = new DelegateCommand(EditConnections);

            Language = new LanguageOptionViewModel(configuration.Language);
            AnimationsMode = new AnimationsOptionViewModel(configuration.AnimationSpeed);
            AutoStartMode = new AutoStartModeViewModel(configuration.Autostart);
            CanceledBuildNotify = new BuildNotificationModeViewModel(configuration.CanceledBuildNotifyConfig, StringLocalizer.Keys.CanceledBuildNotifyConfig);
            FailedBuildNotify = new BuildNotificationModeViewModel(configuration.FailedBuildNotifyConfig, StringLocalizer.Keys.FailedBuildNotifyConfig);
            SucceededBuildNotify = new BuildNotificationModeViewModel(configuration.SucceededBuildNotifyConfig, StringLocalizer.Keys.SucceededBuildNotifyConfig);
            PartialSucceededTreatmentMode = new PartialSucceededTreatmentModeOptionViewModel(configuration.PartialSucceededTreatmentMode);
            BuildsPerGroup = new NumberOptionViewModel(configuration.BuildsToShow, StringLocalizer.Keys.BuildsToShow);
            ShowBusyIndicatorDuringUpdate = new BooleanOptionViewModel(configuration.ShowBusyIndicatorOnDeltaUpdates, StringLocalizer.Keys.ShowBusyIndicatorOnDeltaUpdates);
            UpdateInterval = new NumberOptionViewModel(configuration.UpdateInterval, StringLocalizer.Keys.UpdateInterval);
            UpdateToPreReleases = new BooleanOptionViewModel(configuration.UsePreReleases, StringLocalizer.Keys.UsePreReleases);

            foreach (var option in Options)
            {
                option.ValueChanged += Option_ValueChanged;
            }
        }

        // TODO: Why is this in the ViewModel?
        public IConfiguration Configuration { get; }
        public ICommand EditConnectionsCommand { get; }

        public IEnumerable<OptionViewModelBase> Options
        {
            get
            {
                yield return Language;
                yield return BuildsPerGroup;
                yield return UpdateInterval;
                yield return UpdateToPreReleases;
                yield return CanceledBuildNotify;
                yield return FailedBuildNotify;
                yield return SucceededBuildNotify;
                yield return PartialSucceededTreatmentMode;
                yield return AutoStartMode;
                yield return AnimationsMode;
                yield return ShowBusyIndicatorDuringUpdate;
            }
        }

        private AnimationsOptionViewModel AnimationsMode { get; }
        private AutoStartModeViewModel AutoStartMode { get; }
        private NumberOptionViewModel BuildsPerGroup { get; }
        private BuildNotificationModeViewModel CanceledBuildNotify { get; }
        private BuildNotificationModeViewModel FailedBuildNotify { get; }
        private LanguageOptionViewModel Language { get; }
        private PartialSucceededTreatmentModeOptionViewModel PartialSucceededTreatmentMode { get; }
        private BooleanOptionViewModel ShowBusyIndicatorDuringUpdate { get; }
        private BuildNotificationModeViewModel SucceededBuildNotify { get; }
        private NumberOptionViewModel UpdateInterval { get; }
        private BooleanOptionViewModel UpdateToPreReleases { get; }

        public event EventHandler? EditConnectionsRequested;

        public void UpdateUser()
        {
            // TODO: Implement
            // TODO: Why is this here?
        }

        private void EditConnections()
        {
            EditConnectionsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Option_ValueChanged(object? sender, EventArgs e)
        {
            Configuration.AnimationSpeed = AnimationsMode.Value;
            Configuration.Autostart = AutoStartMode.Value;
            Configuration.BuildsToShow = BuildsPerGroup.Value;
            Configuration.CanceledBuildNotifyConfig = CanceledBuildNotify.Value;
            Configuration.FailedBuildNotifyConfig = FailedBuildNotify.Value;
            Configuration.Language = Language.Value.IetfLanguageTag;
            Configuration.PartialSucceededTreatmentMode = PartialSucceededTreatmentMode.Value;
            Configuration.ShowBusyIndicatorOnDeltaUpdates = ShowBusyIndicatorDuringUpdate.Value;
            Configuration.SucceededBuildNotifyConfig = SucceededBuildNotify.Value;
            Configuration.UpdateInterval = UpdateInterval.Value;
            Configuration.UsePreReleases = UpdateToPreReleases.Value;

            _saveMethod.Invoke();
        }

        private readonly Action _saveMethod;
    }
}