using System;
using System.Collections.Generic;
using System.Linq;
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

        public UpdateContextAwareCrawler(IndexUpdateContext updateContext, ShortID runningContextId, IEnumerable<Uri> urlsToCrawl, ILog logger, params IPipelineStep[] pipeline)
            : base(urlsToCrawl, pipeline)
        {
            m_Logger = logger;
            RunningContextId = runningContextId;
            _updateContext = updateContext;
        }


        public IndexUpdateContext UpdateContext
        {
            get { return _updateContext; }
        }

    }
}