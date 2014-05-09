using System;
using Lucene.Net.Search;
using Sitecore.Search;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class SortableIndexSearchContext : IndexSearchContext, IDisposable
    {
        public SortableIndexSearchContext(ILuceneIndex index)
        {
            if (index != null)
                Initialize(index, true);
        }
        public SearchHits Search(Query query, Sort sort)
        {
            return Search(query, SearchContext.Empty, sort);
        }

        public SearchHits Search(PreparedQuery query, Sort sort)
        {
            return Search(query.Query, SearchContext.Empty, sort);
        }

        public SearchHits Search(QueryBase query, Sort sort)
        {
            return Search(query, SearchContext.Empty, sort);
        }

        public SearchHits Search(string query, Sort sort)
        {
            return Search(query, SearchContext.Empty, sort);
        }

        public SearchHits Search(Query query, ISearchContext context, Sort sort)
        {
            return Search(Prepare(query, context), sort);
        }

        public SearchHits Search(QueryBase query, ISearchContext context, Sort sort)
        {
            return this.Search(Prepare(Translate(query), context), sort);
        }

        public SearchHits Search(string query, ISearchContext context, Sort sort)
        {
            return this.Search(Parse(query, context), sort);
        }

        void IDisposable.Dispose()
        {
            base.Dispose();
        }
    }
}
