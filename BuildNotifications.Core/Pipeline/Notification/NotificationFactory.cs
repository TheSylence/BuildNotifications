﻿using System.Collections.Generic;
using System.Linq;
using BuildNotifications.Core.Config;
using BuildNotifications.Core.Pipeline.Tree;
using BuildNotifications.PluginInterfaces;
using BuildNotifications.PluginInterfaces.Builds;

namespace BuildNotifications.Core.Pipeline.Notification
{
    internal class NotificationFactory
    {
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Factory to create Notifications from a BuildTreeDelta.
        /// </summary>
        /// <param name="configuration">Configuration needed to filter which notifications should be created.</param>
        public NotificationFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IEnumerable<INotification> ProduceNotifications(IBuildTreeBuildsDelta fromDelta)
        {
            if (NothingChanged(fromDelta))
                return Enumerable.Empty<INotification>();

            return CreateNotifications(fromDelta);
        }

        private IEnumerable<INotification> CreateNotifications(IBuildTreeBuildsDelta fromDelta)
        {
            var succeeded = FilterSucceeded(fromDelta).ToList();
            var failed = FilterFailed(fromDelta).ToList();
            var cancelled = FilterCancelled(fromDelta).ToList();

            var allGroups = new List<List<IBuildNode>> {succeeded, failed, cancelled};

            foreach (var buildNodes in allGroups.Where(x => x.Count > 0))
            {
                var status = buildNodes.Max(x => x.Status);

                // try to group by definition/branch etc.
                var groupedByDefinitionAndBranch = GroupByDefinitionAndBranch(buildNodes).ToList();
                var groupedByDefinition = GroupByDefinition(buildNodes).ToList();
                var groupedByBranch = GroupByBranch(buildNodes).ToList();

                // only display groups with a size of up to 3 elements
                var allGroupings = new List<IEnumerable<object>> {groupedByDefinitionAndBranch, groupedByDefinition, groupedByBranch};
                var smallestCount = allGroupings.Min(x => x.Count());

                // for a single build, also display the build notification
                if (smallestCount > 3 || allGroups.SelectMany(x => x).Count() == 1)
                {
                    yield return BuildsNotifications(buildNodes, status);
                    continue;
                }

                // only give this message if it's exactly the same definition and branch for every build
                // otherwise there would be two many messages
                if (groupedByDefinitionAndBranch.Count == 1)
                {
                    var tuple = groupedByDefinitionAndBranch.First().Key;
                    yield return DefinitionAndBranchNotification(buildNodes, status, tuple.definition, tuple.branch);
                    continue;
                }

                if (groupedByDefinition.Count == smallestCount)
                {
                    yield return DefinitionNotification(buildNodes, status, groupedByDefinition.Select(x => x.Key));
                    continue;
                }

                yield return BranchNotification(buildNodes, status, groupedByBranch.Select(x => x.Key));
            }
        }

        private INotification DefinitionNotification(IList<IBuildNode> buildNodes, BuildStatus status, IEnumerable<string> definitionNames)
            => new DefinitionNotification(buildNodes, status, definitionNames.ToList());

        private INotification BuildsNotifications(IList<IBuildNode> buildNodes, BuildStatus status)
            => new BuildNotification(buildNodes, status);

        private INotification BranchNotification(List<IBuildNode> buildNodes, BuildStatus status, IEnumerable<string> branchNames)
            => new BranchNotification(buildNodes, status, branchNames);

        private INotification DefinitionAndBranchNotification(List<IBuildNode> buildNodes, BuildStatus status, string tupleDefinition, string tupleBranch)
            => new DefinitionAndBranchNotification(buildNodes, status, tupleDefinition, tupleBranch);

        private IEnumerable<IGrouping<(string definition, string branch), IBuildNode>> GroupByDefinitionAndBranch(IEnumerable<IBuildNode> allBuilds)
            => allBuilds.GroupBy(x => (definition: x.Build.Definition.Name, branch: x.Build.BranchName));

        private IEnumerable<IGrouping<string, IBuildNode>> GroupByDefinition(IEnumerable<IBuildNode> allBuilds)
            => allBuilds.GroupBy(x => x.Build.Definition.Name);

        private IEnumerable<IGrouping<string, IBuildNode>> GroupByBranch(IEnumerable<IBuildNode> allBuilds)
            => allBuilds.GroupBy(x => x.Build.BranchName);

        private IEnumerable<IBuildNode> FilterSucceeded(IBuildTreeBuildsDelta fromDelta)
        {
            return fromDelta.Succeeded.Where(ShouldNotifyAboutBuild);
        }

        private IEnumerable<IBuildNode> FilterFailed(IBuildTreeBuildsDelta fromDelta)
        {
            var failed = fromDelta.Failed.Where(ShouldNotifyAboutBuild).ToList();
            var succeeded = fromDelta.Succeeded.Where(ShouldNotifyAboutBuild);
            var cancelled = fromDelta.Cancelled.Where(ShouldNotifyAboutBuild);

            if (failed.Any() && !succeeded.Any())
                return failed.Concat(cancelled);

            return failed;
        }

        private IEnumerable<IBuildNode> FilterCancelled(IBuildTreeBuildsDelta fromDelta)
        {
            var failed = fromDelta.Failed.Where(ShouldNotifyAboutBuild).ToList();
            var succeeded = fromDelta.Succeeded.Where(ShouldNotifyAboutBuild);
            var cancelled = fromDelta.Cancelled.Where(ShouldNotifyAboutBuild);

            if (failed.Any() && !succeeded.Any())
                return Enumerable.Empty<IBuildNode>();

            return cancelled;
        }

        private bool ShouldNotifyAboutBuild(IBuildNode buildNode)
        {
            var notifySetting = NotifySetting(buildNode);
            var currentUserIdentities = _configuration.IdentitiesOfCurrentUser;
            switch (notifySetting)
            {
                case BuildNotificationMode.None:
                    return false;
                case BuildNotificationMode.RequestedByMe:
                    return currentUserIdentities.Any(u => IsSameUser(u, buildNode.Build.RequestedBy));
                case BuildNotificationMode.RequestedForMe:
                    return currentUserIdentities.Any(u => IsSameUser(u, buildNode.Build.RequestedFor));
                case BuildNotificationMode.RequestedByOrForMe:
                    return currentUserIdentities.Any(u => IsSameUser(u, buildNode.Build.RequestedFor) || u.Equals(buildNode.Build.RequestedBy));
                default:
                    return true;
            }
        }

        private bool IsSameUser(IUser userA, IUser? userB) => userA.UniqueName == userB?.UniqueName;

        private BuildNotificationMode NotifySetting(IBuildNode buildNode)
        {
            switch (buildNode.Status)
            {
                case BuildStatus.Failed:
                    return _configuration.FailedBuildNotifyConfig;
                case BuildStatus.Succeeded:
                    return _configuration.SucceededBuildNotifyConfig;
                default:
                    return _configuration.CanceledBuildNotifyConfig;
            }
        }

        private bool NothingChanged(IBuildTreeBuildsDelta fromDelta)
        {
            return !fromDelta.Cancelled.Any()
                   && !fromDelta.Failed.Any()
                   && !fromDelta.Succeeded.Any();
        }
    }
}