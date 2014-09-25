using System;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Search;
using Sitecore.SecurityModel;

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
            Score = hit.Position;

            if (!string.IsNullOrEmpty(result.Url) && ItemUri.IsItemUri(result.Url))
            {
                var uri = ItemUri.Parse(result.Url);
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