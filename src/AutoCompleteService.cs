using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.NGram;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Sitecore.ContentSearch.Diagnostics;
using SpellChecker.Net.Search.Spell;

namespace Efocus.Sitecore.LuceneWebSearch
{
    public class AutoCompleteService
    {
        public int MaxResults { get; set; }

        private class AutoCompleteAnalyzer : Analyzer
        {
            public override TokenStream TokenStream(string fieldName, TextReader reader)
            {
                TokenStream result = new StandardTokenizer(kLuceneVersion, reader);

                result = new StandardFilter(result);
                result = new LowerCaseFilter(result);
                result = new ASCIIFoldingFilter(result);
                result = new StopFilter(false, result, StopFilter.MakeStopSet(kDutchStopWords));
                result = new EdgeNGramTokenFilter(result, Side.FRONT, 1, 20);

                return result;
            }
        }

        private static readonly Lucene.Net.Util.Version kLuceneVersion = Lucene.Net.Util.Version.LUCENE_30;

        private readonly String kGrammedWordsField = "words";

        private readonly String kSourceWordField = "sourceWord";

        private readonly String kCountField = "count";

        private readonly String[] kEnglishStopWords = {
            "a", "an", "and", "are", "as", "at", "be", "but", "by",
            "for", "i", "if", "in", "into", "is",
            "no", "not", "of", "on", "or", "s", "such",
            "t", "that", "the", "their", "then", "there", "these",
            "they", "this", "to", "was", "will", "with"
        };

        private static readonly String[] kDutchStopWords = {
            "en", "van", "het", "de", "een", "voor"
        };

        private FSDirectory m_directory;

        private IndexReader m_reader;

        private IndexSearcher m_searcher;

        public AutoCompleteService(string autoCompleteDir)
        {
            m_directory = FSDirectory.Open(autoCompleteDir);
        }

        public void SearchAutoComplete(string autoCompleteDir)
        {
            SearchAutoComplete(FSDirectory.Open(autoCompleteDir));
        }

        public void SearchAutoComplete(FSDirectory autoCompleteDir, int maxResults = 8)
        {
            this.m_directory = autoCompleteDir;
            MaxResults = maxResults;

            ReplaceSearcher();
        }

        /// <summary>
        /// Find terms matching the given partial word that appear in the highest number of documents.</summary>
        /// <param name="term">A word or part of a word</param>
        /// <returns>A list of suggested completions</returns>
        public IEnumerable<String> SuggestTermsFor(string term)
        {
            if (m_searcher == null)
                return new string[] { };

            // get the top terms for query
            Query query = new TermQuery(new Term(kGrammedWordsField, term.ToLower()));
            Sort sort = new Sort(new SortField(kCountField, SortField.INT));

            TopDocs docs = m_searcher.Search(query, null, MaxResults, sort);
            string[] suggestions = docs.ScoreDocs.Select(doc =>
                m_reader.Document(doc.Doc).Get(kSourceWordField)).ToArray();

            return suggestions;
        }


        /// <summary>
        /// Open the index in the given directory and create a new index of word frequency for the 
        /// given index.</summary>
        /// <param name="sourceDirectory">Directory containing the index to count words in.</param>
        /// <param name="fieldToAutocomplete">The field in the index that should be analyzed.</param>
        public void BuildAutoCompleteIndex(FSDirectory sourceDirectory, String fieldToAutocomplete)
        {
            CrawlingLog.Log.Info(string.Format("Started rebuilding AutoComplete index for {0} in directory {1}", sourceDirectory.Directory.FullName, m_directory.Directory.FullName));

            // build a dictionary (from the spell package)
            using (IndexReader sourceReader = IndexReader.Open(sourceDirectory, true))
            {
                LuceneDictionary dict = new LuceneDictionary(sourceReader, fieldToAutocomplete);

                // code from
                // org.apache.lucene.search.spell.SpellChecker.indexDictionary(
                // Dictionary)
                //IndexWriter.Unlock(m_directory);

                // use a custom analyzer so we can do EdgeNGramFiltering
                var analyzer = new AutoCompleteAnalyzer();
                using (var writer = new IndexWriter(m_directory, analyzer, true, IndexWriter.MaxFieldLength.LIMITED))
                {
                    writer.MergeFactor = 300;
                    writer.SetMaxBufferedDocs(150);

                    // go through every word, storing the original word (incl. n-grams) 
                    // and the number of times it occurs
                    foreach (string word in dict)
                    {
                        if (word.Length < 3)
                            continue; // too short we bail but "too long" is fine...

                        // ok index the word
                        // use the number of documents this word appears in
                        int freq = sourceReader.DocFreq(new Term(fieldToAutocomplete, word));
                        var doc = MakeDocument(fieldToAutocomplete, word, freq);

                        writer.AddDocument(doc);
                    }

                    writer.Optimize();
                }

            }

            // re-open our reader
            ReplaceSearcher();

            CrawlingLog.Log.Info(string.Format("Finished rebuilding AutoComplete index for {0} in directory {1}", sourceDirectory.Directory.FullName, m_directory.Directory.FullName));
        }

        private Document MakeDocument(String fieldToAutocomplete, string word, int frequency)
        {
            var doc = new Document();
            doc.Add(new Field(kSourceWordField, word, Field.Store.YES,
                    Field.Index.NOT_ANALYZED)); // orig term
            doc.Add(new Field(kGrammedWordsField, word, Field.Store.YES,
                    Field.Index.ANALYZED)); // grammed
            doc.Add(new Field(kCountField,
                    frequency.ToString(), Field.Store.NO,
                    Field.Index.NOT_ANALYZED)); // count
            return doc;
        }

        private void ReplaceSearcher()
        {
            if (IndexReader.IndexExists(m_directory))
            {
                if (m_reader == null)
                    m_reader = IndexReader.Open(m_directory, true);
                else
                    m_reader.Reopen();

                m_searcher = new IndexSearcher(m_reader);
            }
            else
            {
                m_searcher = null;
            }
        }
    }
}
