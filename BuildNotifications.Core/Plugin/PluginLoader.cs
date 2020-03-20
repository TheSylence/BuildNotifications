﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Anotar.NLog;
using BuildNotifications.Core.Utilities;
using BuildNotifications.PluginInterfaces.Builds;
using BuildNotifications.PluginInterfaces.Host;
using BuildNotifications.PluginInterfaces.SourceControl;
using BuildNotifications.PluginInterfacesLegacy.Notification;

namespace BuildNotifications.Core.Plugin
{
    internal class PluginLoader : IPluginLoader
    {
        public PluginLoader(IPluginHost pluginHost)
        {
            _pluginHost = pluginHost;
        }

        private static IEnumerable<string> PluginsToIgnore { get; } = new List<string>
        {
            Path.GetFileName(typeof(IBuildPlugin).Assembly.Location)!,
            Path.GetFileName(typeof(ISourceControlPlugin).Assembly.Location)!,
            Path.GetFileName(typeof(INotificationProcessor).Assembly.Location)!
        }.Distinct();

        private IEnumerable<T> ConstructPluginsOfType<T>(IEnumerable<Type> types)
        {
            LogTo.Debug($"Parsing plugins to type {typeof(T).Name}.");
            var baseType = typeof(T);

            foreach (var type in types)
            {
                if (baseType.IsAssignableFrom(type))
                {
                    T value;
                    try
                    {
                        value = (T) Activator.CreateInstance(type)!;
                    }
                    catch (Exception ex)
                    {
                        var typeName = type.AssemblyQualifiedName;
                        LogTo.ErrorException($"Exception while trying to construct {typeName}", ex);
                        continue;
                    }

                    LogTo.Debug($"Successfully constructed instance of type {typeof(T).FullName}");
                    yield return value;
                }
                else
                    LogTo.Debug($"Type {baseType.FullName} is not assignable from {type.FullName}");
            }
        }

        private IEnumerable<Assembly> LoadPluginAssemblies(string folder)
        {
            LogTo.Info($"Loading plugin assemblies in folder \"{folder}\".");
            var fullPath = Path.GetFullPath(folder);
            if (!Directory.Exists(fullPath))
            {
                LogTo.Warn("Plugin directory does not exist.");
                yield break;
            }

            var pluginFolders = Directory.GetDirectories(fullPath);

            foreach (var pluginFolder in pluginFolders)
            {
                var assemblyLoadContext = new PluginAssemblyLoadContext(pluginFolder);

                var files = Directory.EnumerateFiles(pluginFolder, "*.dll");

                foreach (var dll in files.Where(NotIgnored))
                {
                    if (Path.GetFileName(dll)?.Contains("plugin", StringComparison.OrdinalIgnoreCase) != true)
                    {
                        LogTo.Debug($"Found file \"{dll}\" but is skipped, as it does not contain string \"plugin\".");
                        continue;
                    }

                    LogTo.Debug($"Loading plugin \"{dll}\".");
                    Assembly assembly;
                    try
                    {
                        assembly = assemblyLoadContext.LoadFromAssemblyPath(dll);

                        foreach (var referencedAssembly in assembly.GetReferencedAssemblies())
                        {
                            if (referencedAssembly.ContentType == AssemblyContentType.WindowsRuntime)
                            {
                                LogTo.Debug($"Skip loading referenced assembly {referencedAssembly.Name} because it contains WinRT code");
                                continue;
                            }

                            assemblyLoadContext.LoadFromAssemblyName(referencedAssembly);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTo.WarnException($"Exception while trying to load {dll}", ex);
                        continue;
                    }

                    LogTo.Debug($"Successfully loaded plugin \"{dll}\".");
                    yield return assembly;
                }
            }
        }

        private IEnumerable<T> LoadPlugins<T>(IEnumerable<T> plugins)
            where T : IPlugin
        {
            foreach (var plugin in plugins)
            {
                try
                {
                    plugin.OnPluginLoaded(_pluginHost);
                }
                catch (Exception ex)
                {
                    var typeName = plugin.GetType().AssemblyQualifiedName;
                    LogTo.ErrorException($"Exception during OnPluginLoaded for {typeName}", ex);
                    continue;
                }

                yield return plugin;
            }
        }

        private bool NotIgnored(string file)
        {
            var fileName = Path.GetFileName(file);
            var isIgnored = PluginsToIgnore.Any(p => p == fileName);
            return !isIgnored;
        }

        public IPluginRepository LoadPlugins(IEnumerable<string> folders)
        {
            var folderList = folders.ToList();

            var assemblies = folderList.SelectMany(LoadPluginAssemblies);
            var exportedTypes = assemblies.SelectMany(a => a.GetExportedTypes())
                .Where(t => !t.IsAbstract)
                .ToList();

            var buildPlugins = LoadPlugins(ConstructPluginsOfType<IBuildPlugin>(exportedTypes)).ToList();
            var sourceControlPlugins = LoadPlugins(ConstructPluginsOfType<ISourceControlPlugin>(exportedTypes)).ToList();
            var notificationProcessors = ConstructPluginsOfType<INotificationProcessor>(exportedTypes).ToList();

            LogTo.Info($"Loaded {buildPlugins.Count} build plugins.");
            LogTo.Info($"Loaded {sourceControlPlugins.Count} source control plugins.");
            LogTo.Info($"Loaded {notificationProcessors.Count} notification processor plugins.");
            return new PluginRepository(buildPlugins, sourceControlPlugins, notificationProcessors, new TypeMatcher());
        }

        private readonly IPluginHost _pluginHost;
    }
}