using System.Net;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class CookiesAwareWebClient : WebClient
    {
        private CookieContainer outboundCookies = new CookieContainer();
        private CookieCollection inboundCookies = new CookieCollection();

        public CookieContainer OutboundCookies
        {
            get { return outboundCookies; }
        }

        public CookieCollection InboundCookies
        {
            get { return inboundCookies; }
        }

        public bool IgnoreRedirects { get; set; }

        protected override WebRequest GetWebRequest(System.Uri address)
        {
            var request = base.GetWebRequest(address);
            if (request is HttpWebRequest)
            {
                (request as HttpWebRequest).CookieContainer = outboundCookies;
                (request as HttpWebRequest).AllowAutoRedirect = !IgnoreRedirects;
                (request as HttpWebRequest).UserAgent =
                    "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; .NET CLR 1.0.3705;)";
                (request as HttpWebRequest).ContentType = "application/x-www-form-urlencoded";
            }
            return request;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            var response = base.GetWebResponse(request);
            if (response is HttpWebResponse)
            {
                inboundCookies = (response as HttpWebResponse).Cookies ?? inboundCookies;
            }
            return response;
        }

    }
}