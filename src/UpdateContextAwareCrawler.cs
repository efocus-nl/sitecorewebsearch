using System;
using NCrawler;
using NCrawler.Interfaces;
using Sitecore.Data;
using Sitecore.Search;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class UpdateContextAwareCrawler: Crawler
    {
        public ShortID RunningContextId { get; set; }
        private readonly IndexUpdateContext _updateContext;

        public UpdateContextAwareCrawler(IndexUpdateContext updateContext, ShortID runningContextId, Uri crawlStart, params IPipelineStep[] pipeline): base(crawlStart, pipeline)
        {
            RunningContextId = runningContextId;
            _updateContext = updateContext;
        }

        public IndexUpdateContext UpdateContext
        {
            get { return _updateContext; }
        }

    }
}