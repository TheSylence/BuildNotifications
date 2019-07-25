﻿using BuildNotifications.PluginInterfaces.Builds;

namespace BuildNotifications.Core.Pipeline.Tree
{
    internal class DefinitionGroupNode : TreeNode, IDefinitionGroupNode
    {
        public DefinitionGroupNode(IBuildDefinition definition)
        {
            Definition = definition;
        }

        /// <inheritdoc />
        public IBuildDefinition Definition { get; }

        public override bool Equals(IBuildTreeNode other)
        {
            return Definition.Id.Equals((other as DefinitionGroupNode)?.Definition?.Id);
        }
    }
}