using System;
using System.Collections.Generic;
using NCrawler;
using NCrawler.Interfaces;
using Sitecore.ContentSearch.LuceneProvider;
using Sitecore.Data;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class UpdateContextAwareCrawler : Crawler
    {
        public ShortID RunningContextId { get; set; }
        private readonly LuceneUpdateContext _updateContext;

        public UpdateContextAwareCrawler(LuceneUpdateContext updateContext, ShortID runningContextId, IEnumerable<Uri> urlsToCrawl, ILog logger, params IPipelineStep[] pipeline)
            : base(urlsToCrawl, pipeline)
        {
            m_Logger = logger;
            RunningContextId = runningContextId;
            _updateContext = updateContext;
        }

        public LuceneUpdateContext UpdateContext
        {
            get { return _updateContext; }
        }

    }
}