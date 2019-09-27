﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Anotar.NLog;
using BuildNotifications.Core.Pipeline.Tree;
using BuildNotifications.Core.Pipeline.Tree.Arrangement;

namespace BuildNotifications.ViewModel.Tree
{
    internal class BuildTreeViewModelFactory
    {
        public async Task<BuildTreeViewModel> ProduceAsync(IBuildTree tree, BuildTreeViewModel? existingTree, IBuildTreeSortingDefinition buildTreeSortingDefinition)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            LogTo.Debug("Producing ViewModel for BuildTree.");
            var buildTreeResult = await Task.Run(() =>
            {
                var groupsAsList = tree.GroupDefinition.ToList();
                var sortingsAsList = buildTreeSortingDefinition.ToList();
                LogTo.Debug($"Grouping by {string.Join(",", tree.GroupDefinition)}.");
                var buildTree = new BuildTreeViewModel(tree);

                var firstLevelSorting = !buildTreeSortingDefinition.Any() ? SortingDefinition.AlphabeticalDescending :buildTreeSortingDefinition.First();
                buildTree.SetSorting(firstLevelSorting);

                var children = CreateChildren(tree.Children, groupsAsList, sortingsAsList, 0);
                foreach (var childVm in children)
                {
                    buildTree.Children.Add(childVm);
                }

                return buildTree;
            });

            if (existingTree != null)
            {
                LogTo.Debug("Merging with existing tree.");
                buildTreeResult = Merge(existingTree, buildTreeResult);
            }

            var treeDepth = GetMaxDepth(buildTreeResult);
            LogTo.Debug($"Setting max depths, which is {treeDepth}.");
            SetMaxDepths(buildTreeResult, treeDepth);

            stopWatch.Stop();
            LogTo.Info($"Produced ViewModels for BuildTree in {stopWatch.ElapsedMilliseconds} ms.");
            return buildTreeResult;
        }

        private static BuildTreeNodeViewModel AsViewModel(IBuildTreeNode node, GroupDefinition groupDefinition)
        {
            switch (groupDefinition)
            {
                case GroupDefinition.Branch:
                    return new BranchGroupNodeViewModel((IBranchGroupNode) node);
                case GroupDefinition.BuildDefinition:
                    return new DefinitionGroupNodeViewModel((IDefinitionGroupNode) node);
                case GroupDefinition.Source:
                    return new SourceGroupNodeViewModel((ISourceGroupNode) node);
                case GroupDefinition.Status:
                    return new StatusGroupNodeViewModel(node);
                default:
                    return new BuildNodeViewModel((IBuildNode) node) {IsLargeSize = false};
            }
        }

        private IEnumerable<BuildTreeNodeViewModel> CreateChildren(IEnumerable<IBuildTreeNode> children, IReadOnlyList<GroupDefinition> groups, List<SortingDefinition> sortingsAsList, int groupIndex)
        {
            if (children == null)
                yield break;

            foreach (var node in children.ToList())
            {
                var groupDefinition = groupIndex >= groups.Count ? GroupDefinition.None : groups[groupIndex];
                var nodeVm = AsViewModel(node, groupDefinition);

                // the sorting definition means the current level, therefore the parent of the current level has to sort its children (which are the current level)
                // therefore the sorting of this level, is how the children of the next level shall be sorted. This is why we use index + 1
                if (groupIndex + 1 < sortingsAsList.Count)
                    nodeVm.SetSorting(sortingsAsList[groupIndex + 1]);
                // the last level are always the builds which are always sorted by DateAscending, which is the default. So there is nothing to do in the else case
                
                var childrenVms = CreateChildren(node.Children, groups, sortingsAsList, groupIndex + 1);
                foreach (var childVm in childrenVms)
                {
                    nodeVm.Children.Add(childVm);
                }

                yield return nodeVm;
            }
        }

        private static int GetMaxDepth(BuildTreeNodeViewModel node, int currentDepth = 0)
        {
            var maxDepth = currentDepth;
            foreach (var child in node.Children)
            {
                var deepestChildDepth = GetMaxDepth(child, currentDepth + 1);
                maxDepth = deepestChildDepth;
            }

            return maxDepth;
        }

        private BuildTreeViewModel Merge(BuildTreeViewModel tree1, BuildTreeViewModel tree2)
        {
            var taggedNodes = new List<BuildTreeNodeViewModel>();
            TagAllNodesForDeletion(tree1, taggedNodes);

            foreach (var child in tree2.Children)
            {
                MergeInternal(tree1, child, taggedNodes);
            }

            RemoveTaggedNodes(tree1, taggedNodes);

            return tree1;
        }

        private void MergeInternal(BuildTreeNodeViewModel tree1, BuildTreeNodeViewModel tree2, List<BuildTreeNodeViewModel> taggedNodes)
        {
            var insertTarget = tree1;
            var nodeToInsert = tree2;

            var subTree = insertTarget.Children.FirstOrDefault(node => node.NodeSource.Equals(nodeToInsert.NodeSource));
            if (subTree != null)
            {
                foreach (var child in nodeToInsert.Children)
                {
                    MergeInternal(subTree, child, taggedNodes);
                }

                taggedNodes.Remove(subTree);
                subTree.BackendPropertiesChanged();
            }
            else
            {
                tree1.Children.Add(nodeToInsert);
                taggedNodes.Remove(tree1);
            }
        }

        private void RemoveTaggedNodes(BuildTreeNodeViewModel tree, List<BuildTreeNodeViewModel> taggedNodes)
        {
            foreach (var node in tree.Children.ToList())
            {
                if (taggedNodes.Contains(node))
                    tree.Children.Remove(node);
                else
                    RemoveTaggedNodes(node, taggedNodes);
            }
        }

        private static void SetMaxDepths(BuildTreeNodeViewModel node, in int maxDepth)
        {
            node.MaxTreeDepth = maxDepth;
            foreach (var child in node.Children)
            {
                SetMaxDepths(child, maxDepth);
            }
        }

        private void TagAllNodesForDeletion(BuildTreeNodeViewModel tree, List<BuildTreeNodeViewModel> taggedNodes)
        {
            foreach (var node in tree.Children)
            {
                taggedNodes.Add(node);
                TagAllNodesForDeletion(node, taggedNodes);
            }
        }
    }
}