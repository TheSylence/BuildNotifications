﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Anotar.NLog;
using BuildNotifications.Core.Config;
using Microsoft.Win32;

namespace BuildNotifications.ViewModel.Utils.Configuration
{
    public class AutostartHelper
    {
        public AutostartHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void UpdateRegistrationForAutostart()
        {
            LogTo.Info($"Updating autostart registration. Autostart mode: {_configuration.Autostart}");

            if (InDebug())
            {
                LogTo.Info("App was started in Debug. Not registering autostart");
                return;
            }

            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly.GetName().Name ?? "";

            if (ShouldAutostart())
            {
                LogTo.Info("Registering Autostart.");
                RegisterForAutostart(name);
            }
            else
            {
                LogTo.Info("Deregistering Autostart.");
                DeregisterForAutostart(name);
            }
        }

        private string AutostartCommand()
        {
            return $"\"{AutostartLocation()}\" {StartupMode()}";
        }

        private string AutostartLocation()
        {
            var fileName = Path.GetFileName(Assembly.GetExecutingAssembly().Location) ?? "";
            fileName = Path.ChangeExtension(fileName, "exe");

            var location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            location = Directory.GetParent(location).FullName;

            return Path.Combine(location, fileName);
        }

        private void DeregisterForAutostart(string name)
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (key == null)
                    return;

                if (key.GetValueNames().Contains(name))
                    key.DeleteValue(name);
            }
            catch (Exception e)
            {
                LogTo.ErrorException("Could not remove autostart from registry", e);
            }
        }

        private bool InDebug()
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }

        private void RegisterForAutostart(string name)
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                var cmd = AutostartCommand();

                key?.SetValue(name, cmd);
            }
            catch (Exception e)
            {
                LogTo.ErrorException("Could not write autostart to registry", e);
            }
        }

        private bool ShouldAutostart()
        {
            return _configuration.Autostart == AutostartMode.StartWithWindows || _configuration.Autostart == AutostartMode.StartWithWindowsMinimized;
        }

        private string StartupMode()
        {
            return _configuration.Autostart == AutostartMode.StartWithWindowsMinimized ? MinimizeArgument : "";
        }

        private readonly IConfiguration _configuration;

        public const string MinimizeArgument = "--minimize";
        private const string RunKeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
    }
}