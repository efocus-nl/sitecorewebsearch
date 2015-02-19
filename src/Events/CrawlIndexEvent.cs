using System;
using System.Runtime.Serialization;

namespace Efocus.Sitecore.LuceneWebSearch.Events
{
    [DataContract]
    public enum CrawlMethod
    {
        Update,
        Rebuild
    }

    [DataContract]
    public class CrawlIndexEvent
    {
        [DataMember]
        public String IndexName
        {
            get;
            set;
        }

        [DataMember]
        public CrawlMethod Method
        {
            get;
            set;
        }
    }
}
