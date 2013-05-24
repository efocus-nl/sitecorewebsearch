using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sitecore.Data;
using Sitecore.Pipelines.HttpRequest;

namespace Efocus.LuceneWebSearch.SitecoreProcessors
{
    public class AddHeadersHttpRequestProcessor : HttpRequestProcessor
    {
        public const string SitecoreItemHeaderKey = "sitecore-item";

        public override void Process(HttpRequestArgs args)
        {
            if (Sitecore.Context.Item != null)
                args.Context.Response.AddHeader(SitecoreItemHeaderKey, new ItemUri(Sitecore.Context.Item).ToString());
        }
    }
}
