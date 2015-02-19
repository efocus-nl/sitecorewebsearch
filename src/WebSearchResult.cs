using System;
using Lucene.Net.Documents;
using Lucene.Net.Search;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.SecurityModel;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class WebSearchResult
    {
        public WebSearchResult(IndexSearcher searcher, ScoreDoc scoreDoc)
        {
            Document doc = searcher.Doc(scoreDoc.Doc);

            var result = new SearchResultItem();
            Url = doc.Get(BuiltinFields.Url);
            PathAndQuery = doc.Get(BuiltinFields.Path);
            Title = doc.Get(BuiltinFields.Name);
            Description = doc.Get(CustomFields.Description);
            Score = scoreDoc.Score;

            if (!string.IsNullOrEmpty(Url) && ItemUri.IsItemUri(Url))
            {
                var uri = ItemUri.Parse(Url);
                var db = !String.IsNullOrEmpty(uri.DatabaseName)
                             ? Factory.GetDatabase(uri.DatabaseName)
                             : global::Sitecore.Context.Database;
                if (db != null)
                {
                    using (new SecurityDisabler())
                    {
                        this.Item = db.GetItem(new DataUri(uri));
                    }
                }
            }
        }

        public string Url { get; set; }
        public string Description { get; set; }
        public string Title { get; set; }

        /// <summary>
        /// Item without security
        /// </summary>
        public Item Item
        {
            get;
            private set;
        }

        public string PathAndQuery { get; set; }
        public float Score { get; set; }

        private Item LoadUserItem()
        {
            if (Item != null)
            {
                var currentItem = Context.Database.GetItem(Item.ID);

                if (currentItem != null && currentItem.Versions.Count > 0)
                {
                    return currentItem;
                }
            }
            return null;
        }

        private Item _userItem;

        public Item UserItem
        {
            get
            {
                if (_userItem != null)
                    return _userItem;

                return _userItem = LoadUserItem();
            }
        }
    }
}