using Lucene.Net.Analysis.Pt;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using WebSearchEngineAPI.Models;

namespace WebSearchEngineAPI.Services.SearchEngine
{
    /// <summary>
    /// Lucene.NET implementation to handle text search
    /// </summary>
    public class LuceneSearchEngineService
    {
        private readonly ILogger<LuceneSearchEngineService> _logger;
        private readonly IConfiguration _configuration;

        private readonly string _indexPath;

        private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
        private readonly PortugueseAnalyzer _portugueseAnalyzer;

        private readonly IndexWriterConfig _indexConfig;

        public LuceneSearchEngineService(ILogger<LuceneSearchEngineService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // Gets Lucene.NET database
            DirectoryInfo directoryInfo = new(Path.Combine("AppData","index"));
            _indexPath = directoryInfo.FullName;

            _logger.LogInformation("Starting Lucene.NET at {0}", _indexPath);

            // Create an analyzer to process the text
            _portugueseAnalyzer = new PortugueseAnalyzer(AppLuceneVersion);

            // Create an index writer
            _indexConfig = new IndexWriterConfig(AppLuceneVersion, _portugueseAnalyzer);
        }

        /// <summary>
        /// Adds or updates a document.
        /// </summary>
        public void AddOrUpdateToIndex(PageWebDoc page)
        {
            try
            {
                using var dir = FSDirectory.Open(_indexPath);

                using var writer = new IndexWriter(dir, _indexConfig);
                using var reader = writer.GetReader(applyAllDeletes: true);

                var searcher = new IndexSearcher(reader);

                string pageDescription = page.Description;

                // if a page doesn't have a description, fetches the first 140 chars from the content as description.
                if (string.IsNullOrEmpty(pageDescription))
                    pageDescription = page.Content.Length > 140 ? page.Content.Substring(0,140) : page.Content;

                // Creates a new Lucene document
                var doc = new Document {
                    // StringField indexes but doesn't tokenize
                    new StringField("name", page.Title, Field.Store.YES),
                    new TextField("description", pageDescription, Field.Store.YES),
                    new TextField("content", page.Content, Field.Store.YES),
                    new StringField("url", page.PageUrl, Field.Store.YES),
                    new StringField("lastcrawl", page.LastCrawl.ToString(), Field.Store.YES)
                };

                // Searches for a specific term
                Term term = new("url", page.PageUrl);
                Query query = new TermQuery(term);
                ScoreDoc[] r = searcher.Search(query, 1).ScoreDocs;

                // If document doesn't exists in the index.
                if (r.Length == 0)
                {
                    // Adds.
                    writer.AddDocument(doc);
                    _logger.LogInformation($"Adding document: {page.PageUrl}");
                }
                else
                {
                    // Updates based on the URL
                    writer.UpdateDocument(term, doc, _portugueseAnalyzer);
                    _logger.LogInformation($"Updating document {page.PageUrl}");
                }

                writer.Flush(triggerMerge: false, applyAllDeletes: false);

            }
            catch (AlreadySetException e)
            {
                _logger.LogError($"Object already exists: {e.Message} - {page.PageUrl}");
            }
            catch (Exception e)
            {
                _logger.LogError($"Error while trying to add/update: {e}");
                throw;
            }
        }

        /// <summary>
        /// Query in Lucene.NET.
        /// </summary>
        public IEnumerable<PageItem> GetFromIndexPaginated(string[] terms, int page, int amount)
        {
            List<PageItem> pages = new();

            using var dir = FSDirectory.Open(_indexPath);

            // Uses the PT-BR analyzer
            var query = new QueryBuilder(_portugueseAnalyzer);

            // Creates a boolean query (equivalent to a OR)
            // To change the query algorithm, update the content below.
            var bQuery = new BooleanQuery();

            foreach (var term in terms)
            {
                bQuery.Add(query.CreatePhraseQuery("name", term), Occur.SHOULD);
                bQuery.Add(query.CreatePhraseQuery("content", term), Occur.SHOULD);
                bQuery.Add(query.CreatePhraseQuery("description", term), Occur.SHOULD);
            }

            // Re-use the writer to get real-time updates
            using var reader = DirectoryReader.Open(dir);
            var searcher = new IndexSearcher(reader);

            // Fetches 1000 results.
            TopScoreDocCollector collector = TopScoreDocCollector.Create(1000, true);
            searcher.Search(bQuery, collector);

            // Get paginated.
            ScoreDoc[] docs = collector.GetTopDocs(page * amount, amount).ScoreDocs;

            // Convert the document to the DTO model.
            foreach (var hit in docs)
            {
                var foundDoc = searcher.Doc(hit.Doc);

                _ = DateTime.TryParse(foundDoc.Get("lastcrawl"), out DateTime d);

                pages.Add(new PageItem()
                {
                    PageUrl = foundDoc.Get("url"),
                    Content = foundDoc.Get("content"),
                    Description = foundDoc.Get("description"),
                    Title = foundDoc.Get("name"),
                    LastCrawl = d
                });
            }

            return pages;
        }
    }
}
