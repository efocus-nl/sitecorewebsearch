using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Sitecore.Data.Items;
using Sitecore.Globalization;
using Sitecore.Search;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class Searcher
    {
        private readonly string _indexName;
        public List<Language> Languages { get; set; }

        public Searcher(string indexName, params Language[] languages)
        {
            _indexName = indexName;
            Languages = new List<Language>(languages);
        }

        public IEnumerable<WebSearchResult> Query(String query, out int totalResults,
                                                  Item rootItem = null,
                                                  int? start = null,
                                                  int? count = null,
                                                  Sort sort = null,
                                                  Guid templateId = default(Guid))
        {
            if (string.IsNullOrEmpty(query))
            {
                totalResults = 0;
                return Enumerable.Empty<WebSearchResult>();
            }
            // first escape the queryString so that e.g. ~ will be escaped

            return Query(GetFullTextQuery(query), out totalResults, rootItem, start, count, sort, templateId);
        }

        public BooleanQuery GetFullTextQuery(string query, float minimumSimilarity = 0.5f)
        {
            query = QueryParser.Escape(query);
            var textQueries = new BooleanQuery();
            var hasMoreWords = query.Contains(" ");
            textQueries.Add(GetFullTextQuery(query, hasMoreWords ? 0.7f : 0, minimumSimilarity), Occur.SHOULD);
            if (hasMoreWords)
            {
                var parts = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    textQueries.Add(GetFullTextQuery(part, 0, 0.7f), Occur.SHOULD);
                }
            }

            return textQueries;
        }
        
        public BooleanQuery GetFullTextQuery(string query, float extraBoost, float minimumSimilarity, float boostTitle = 1.5f)
        {
            query = QueryParser.Escape(query);
            var boolq = new BooleanQuery();
            
            var contentTerm = new Term(BuiltinFields.Content, query.ToLower());
            var qbody = minimumSimilarity < 1 ? (Query) new FuzzyQuery(contentTerm, minimumSimilarity) : new TermQuery(contentTerm);
            qbody.Boost = 1.0f + extraBoost;

            boolq.Add(qbody, Occur.SHOULD);

            var nameTerm = new Term(BuiltinFields.Name, query.ToLower());
            var qtitle = minimumSimilarity < 1 ? (Query) new FuzzyQuery(nameTerm, minimumSimilarity) : new TermQuery(nameTerm);
            qtitle.Boost = boostTitle + extraBoost;
            boolq.Add(qtitle, Occur.SHOULD);

            var descriptionTerm = new Term(CustomFields.Description, query.ToLower());
            var qdescription =  minimumSimilarity < 1 ? (Query) new FuzzyQuery(descriptionTerm, minimumSimilarity) : new TermQuery(descriptionTerm);
            qdescription.Boost = 1.1f + extraBoost;
            boolq.Add(qdescription, Occur.SHOULD);

            return boolq;
        }

        public IEnumerable<WebSearchResult> Query(Query query, out int totalResults, 
            Item rootItem = null, 
            int? start = null, 
            int? count = null, 
            Sort sort = null,
            Guid templateId = default(Guid))
        {
            var searchContext = new FixedSearchContext() { TemplateID = templateId };

            var boolQuery = new BooleanQuery();
            if (query != null)
                boolQuery.Add(query, Occur.MUST);

            if (rootItem != null && rootItem.ID.ToGuid() != global::Sitecore.ItemIDs.RootID.ToGuid())
            {
                searchContext.Item = rootItem;
            }

            searchContext.ContentLanguages = Languages;

            using (var context = new SortableIndexSearchContext(SearchManager.GetIndex(_indexName)))
            {
                var searchHits = sort == null ? context.Search(boolQuery, int.MaxValue, searchContext) : context.Search(boolQuery, searchContext, sort);
                totalResults = searchHits.Length;
                //var resultCollection = searchHits.FetchResults(start, count);
                IEnumerable<SearchHit> hits;
                if (start != null)
                {
                    hits = (count.HasValue) ? searchHits.Slice(start.Value, count.Value) : searchHits.Slice(start.Value);
                }
                else
                {
                    hits = searchHits.Hits;
                }
                return hits.Select(hit => new WebSearchResult(hit)).ToList();
            }
        }

    }
}
