using System;
using System.Collections.Generic;
using System.Net;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public interface IWebDownloaderWithCookies
    {
        Uri DefaultDomain { get; set; }

        List<String> Keys { get; set; }

        IEnumerable<Cookie> KeyCookies { get; }

        CookieCollection Cookies { get; set; }
    }
}
