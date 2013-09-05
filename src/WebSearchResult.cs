using System;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Search;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class WebSearchResult
    {
        public WebSearchResult(SearchHit hit)
        {
            var result = new SearchResult(hit);
            
            PathAndQuery = hit.Document.Get(BuiltinFields.Path);
            Title = result.Title;
            Description = hit.Document.Get(CustomFields.Description);
            Score = hit.Score;

            if (!string.IsNullOrEmpty(result.Url) && ItemUri.IsItemUri(result.Url))
            {
                var uri = ItemUri.Parse(result.Url);
                var db = !String.IsNullOrEmpty(uri.DatabaseName)
                             ? Factory.GetDatabase(uri.DatabaseName)
                             : global::Sitecore.Context.Database;
                if (db != null)
                {
                    this.Item = db.GetItem(new DataUri(uri));
                }
            }
        }

        public string Description { get; set; }
        public string Title { get; set; }
        public Item Item { get; set; }
        public string PathAndQuery { get; set; }
        public float Score { get; set; }
    }
}