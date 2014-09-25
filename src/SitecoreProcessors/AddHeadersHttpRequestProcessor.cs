using Sitecore.Data;
using Sitecore.Pipelines.HttpRequest;

namespace Efocus.Sitecore.LuceneWebSearch.SitecoreProcessors
{
    public class AddHeadersHttpRequestProcessor : HttpRequestProcessor
    {
        public const string SitecoreItemHeaderKey = "sitecore-item";

        public override void Process(HttpRequestArgs args)
        {
            if (global::Sitecore.Context.Item != null)
                args.Context.Response.AddHeader(SitecoreItemHeaderKey, new ItemUri(global::Sitecore.Context.Item).ToString());
        }
    }
}
