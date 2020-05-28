﻿using System.Collections.Generic;
using System.Linq;
using BuildNotifications.Core.Text;
using BuildNotifications.PluginInterfaces.Builds;

namespace BuildNotifications.Core.Pipeline.Tree.Search.Criteria
{
    internal class DefinitionCriteria : BaseStringCriteria
    {
        public DefinitionCriteria(IPipeline pipeline) : base(StringLocalizer.SearchCriteriaDefinitionKeyword, StringLocalizer.SearchCriteriaDefinitionDescription, pipeline)
        {
        }

        protected override IEnumerable<string> ResolveAllPossibleStringValues(IPipeline pipeline) => pipeline.CachedDefinitions().Select(b => b.Name).Distinct();

        protected override string StringValueOfBuild(IBuild build) => build.Definition.Name;

        protected override IEnumerable<string> Examples()
        {
            yield return StringLocalizer.SearchCriteriaDefinitionNightlyExample;
            yield return string.Join("", StringLocalizer.SearchCriteriaDefinitionNightlyExample.ToLower(CurrentCultureInfo).Take(3));
            yield return "=" + StringLocalizer.SearchCriteriaDefinitionNightlyExample;
            yield return "*" + StringLocalizer.SearchCriteriaDefinitionNightlyExample;
        }
    }
}