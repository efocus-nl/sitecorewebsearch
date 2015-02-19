﻿using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.LuceneProvider;
using Sitecore.Data.Items;
using Sitecore.Globalization;

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

        public BooleanQuery GetFullTextQuery(string query, float minimumSimilarity = 0.5f, float boostTitle = 1.5f, string[] stopwords = null)
        {
            var textQueries = new BooleanQuery();
            var hasMoreWords = query.Contains(" ");
            textQueries.Add(GetFullTextQueryOnWord(query, hasMoreWords ? 0.7f : 0, minimumSimilarity), Occur.SHOULD);
            if (hasMoreWords)
            {
                var parts = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts.Where(part => stopwords == null || !stopwords.Contains(part.ToLowerInvariant())))
                {
                    textQueries.Add(GetFullTextQueryOnWord(part, 0, 0.7f, boostTitle), Occur.SHOULD);
                }
            }

            return textQueries;
        }
        
        protected virtual BooleanQuery GetFullTextQueryOnWord(string query, float extraBoost, float minimumSimilarity, float boostTitle = 1.5f)
        {
            var boolq = new BooleanQuery();
            
            var contentTerm = new Term(BuiltinFields.Content, QueryParser.Escape(query).ToLower());
            var qbody = minimumSimilarity < 1 ? (Query) new FuzzyQuery(contentTerm, minimumSimilarity) : new TermQuery(contentTerm);
            qbody.Boost = 1.0f + extraBoost;

            boolq.Add(qbody, Occur.SHOULD);

            var nameTerm = new Term(BuiltinFields.Name, QueryParser.Escape(query).ToLower());
            var qtitle = minimumSimilarity < 1 ? (Query) new FuzzyQuery(nameTerm, minimumSimilarity) : new TermQuery(nameTerm);
            qtitle.Boost = boostTitle + extraBoost;
            boolq.Add(qtitle, Occur.SHOULD);

            var descriptionTerm = new Term(CustomFields.Description, QueryParser.Escape(query).ToLower());
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
            //Include comments if language fix is needed. Current Searcher.Search can't use an other search context, so i removed it completely!
            //var searchContext = new FixedSearchContext() { TemplateID = templateId };

            var boolQuery = new BooleanQuery();
            if (query != null)
                boolQuery.Add(query, Occur.MUST);

            //if (rootItem != null && rootItem.ID.ToGuid() != global::Sitecore.ItemIDs.RootID.ToGuid())
            //{
            //    searchContext.Item = rootItem;
            //}

            //searchContext.ContentLanguages = Languages;

            using (var context = ContentSearchManager.GetIndex(_indexName).CreateSearchContext() as LuceneSearchContext)
            {
                if (context != null)
                {
                    var searchHits = sort == null ? context.Searcher.Search(boolQuery, int.MaxValue) : context.Searcher.Search(boolQuery, null, int.MaxValue, sort);
                    totalResults = searchHits.TotalHits;

                    List<ScoreDoc> scoreDocs = searchHits.ScoreDocs.ToList();
                    if (start != null)
                    {
                        if (start > totalResults)
                            start = totalResults;

                        scoreDocs = scoreDocs.Skip(start.Value).ToList();
                    }
                    if (count != null)
                    {
                        if (count > totalResults - start)
                            count = totalResults - start;

                        scoreDocs = scoreDocs.Take(count.Value).ToList();
                    }

                    return scoreDocs.Select(hit => new WebSearchResult(context.Searcher, hit)).ToList();
                }
            }

            totalResults = 0;
            return Enumerable.Empty<WebSearchResult>();
        }

    }
}
