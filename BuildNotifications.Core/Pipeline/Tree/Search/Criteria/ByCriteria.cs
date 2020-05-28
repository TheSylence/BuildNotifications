﻿using System;
using System.Collections.Generic;
using System.Linq;
using BuildNotifications.Core.Text;
using BuildNotifications.PluginInterfaces.Builds;

namespace BuildNotifications.Core.Pipeline.Tree.Search.Criteria
{
    internal class ByCriteria : BaseStringCriteria
    {
        public ByCriteria(IPipeline pipeline) : base(StringLocalizer.SearchCriteriaByKeyword, StringLocalizer.SearchCriteriaByDescription, pipeline)
        {
        }

        protected override IEnumerable<string> ResolveAllPossibleStringValues(IPipeline pipeline) => pipeline.CachedBuilds().Select(b => b.RequestedBy.DisplayName).Distinct();

        protected override bool IsBuildIncludedInternal(IBuild build, string input)
        {
            if (!StringMatcher.SearchPattern.Equals(input, StringComparison.InvariantCulture))
                StringMatcher.SearchPattern = input;

            return StringMatcher.IsMatch(StringValueOfBuild(build));
        }

        protected override string StringValueOfBuild(IBuild build) => build.RequestedBy.DisplayName;

        protected override IEnumerable<string> Examples()
        {
            yield return StringLocalizer.SearchCriteriaByMeExample;
            yield return string.Join("", StringLocalizer.SearchCriteriaBySomeoneExample.ToLower(CurrentCultureInfo).Take(3));
            yield return "=" + StringLocalizer.SearchCriteriaBySomeoneExample;
            yield return "*" + StringLocalizer.SearchCriteriaBySomeoneExample;
        }
    }
}