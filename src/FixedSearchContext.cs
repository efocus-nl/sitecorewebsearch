﻿using System;
using System.Collections.Generic;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Sitecore.Data;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Search;
using Sitecore.Shell;
using Sitecore.Shell.Data;

namespace Efocus.Sitecore.LuceneWebSearch
{
    //naming became somewhat awkward, but isearchcontext asks for naming this a searchcontext as well....
    public class FixedSearchContext : ISearchContext
    {
        private bool _ignoreContentEditorOptions;
        private global::Sitecore.Data.Items.Item _item;
        private global::Sitecore.Security.Accounts.User _user;

        public FixedSearchContext()
        {
            ContentLanguages = new List<Language>();
        }

        public FixedSearchContext(global::Sitecore.Data.Items.Item item)
        {
            this._item = item;
        }

        public FixedSearchContext(global::Sitecore.Security.Accounts.User user)
        {
            this._user = user;
        }

        public FixedSearchContext(global::Sitecore.Security.Accounts.User user, global::Sitecore.Data.Items.Item item)
        {
            this._user = user;
            this._item = item;
        }

        protected virtual void AddDecorations(BooleanQuery result)
        {
            Assert.ArgumentNotNull(result, "result");
            global::Sitecore.Security.Accounts.User user = this.User;
            if (user != null)
            {
                result.Add(new TermQuery(new Term(BuiltinFields.Creator, user.Name)), Occur.SHOULD);
                result.Add(new TermQuery(new Term(BuiltinFields.Editor, user.Name)), Occur.SHOULD);
            }
            global::Sitecore.Data.Items.Item item = this.Item;
            if (item != null)
            {
                result.Add(new TermQuery(new Term(BuiltinFields.Path, ShortID.Encode(item.ID).ToLowerInvariant())), Occur.MUST);
                result.Add(new TermQuery(new Term(BuiltinFields.Database, item.Database.Name.ToLowerInvariant())), Occur.MUST);
                if (this.ContentLanguages.Count == 0)
                    result.Add(new TermQuery(new Term(BuiltinFields.Language, item.Language.ToString().ToLowerInvariant())), Occur.MUST);
            }
            if (this.ContentLanguages.Count > 0)
            {
                var langQuery = new BooleanQuery(true);
                this.ContentLanguages.ForEach(lang => langQuery.Add(new TermQuery(new Term(BuiltinFields.Language, lang.ToString().ToLowerInvariant())), Occur.SHOULD));
                result.Add(langQuery, Occur.MUST);
            }
            if (!this.IgnoreContentEditorOptions)
            {
                if (!UserOptions.View.ShowHiddenItems)
                {
                    result.Add(new TermQuery(new Term(BuiltinFields.Hidden, "1")), Occur.MUST_NOT);
                }
                if (!UserOptions.View.ShowEntireTree && (item != null))
                {
                    global::Sitecore.Data.Items.Item item2 = item.Database.GetItem(RootSections.GetSection(item));
                    if (item2 != null)
                    {
                        result.Add(new TermQuery(new Term(BuiltinFields.Path, ShortID.Encode(item2.ID).ToLowerInvariant())), Occur.MUST);
                    }
                }
            }
            if (TemplateID != default(Guid))
            {
                result.Add(new TermQuery(new Term(BuiltinFields.Template, new ShortID(TemplateID).ToString().ToLowerInvariant())), Occur.MUST);
            }
        }

        public Query Decorate(Query query)
        {
            //all this class, just to remove the bug in sitecore that it doesn't check for contentlanguage to be null :(
            Assert.ArgumentNotNull(query, "query");
            BooleanQuery result = new BooleanQuery(true);
            if (!(query is MatchAllDocsQuery))
                result.Add(query, Occur.MUST);
            this.AddDecorations(result);
            return result.Clauses.Count > 0 ? result : query;
        }

        public List<Language> ContentLanguages { get; set; }

        public bool IgnoreContentEditorOptions
        {
            get
            {
                return this._ignoreContentEditorOptions;
            }
            set
            {
                this._ignoreContentEditorOptions = value;
            }
        }

        public global::Sitecore.Data.Items.Item Item
        {
            get
            {
                return this._item;
            }
            set
            {
                this._item = value;
            }
        }

        public global::Sitecore.Security.Accounts.User User
        {
            get
            {
                return this._user;
            }
            set
            {
                this._user = value;
            }
        }

        public Guid TemplateID { get; set; }

        private class EmptySearchContext : ISearchContext
        {
            public Query Decorate(Query query)
            {
                Assert.ArgumentNotNull(query, "query");
                return query;
            }
        }
    }
}