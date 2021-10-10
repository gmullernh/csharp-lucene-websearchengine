using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebSearchEngineAPI.Services.SearchEngine;
using WebSearchEngineAPI.Models;
using WebSearchEngineAPI.Repositories;

namespace WebSearchEngineAPI.Services.Crawler
{
    /// <summary>
    /// Background
    /// </summary>
    public class CrawlerService : BackgroundService
    {
        private readonly ILogger<CrawlerService> _logger;
        private readonly IServiceProvider _provider;
        private readonly IConfiguration _configuration;
        private readonly IServiceScope _serviceScope;

        private readonly PageDatabaseRepository _webPageRepository;
        private readonly LuceneSearchEngineService _luceneSearchEngineService;

        private readonly List<PageWebDoc> _crawlers = new();
        private readonly string _rootPage;
        private readonly int _defaultTimeout = 1000;

        private readonly object _lock = new();

        // Flags
        private bool _isFirstRunFlag = true;

        // Constants
        private const int CRAWLER_LIMIT = 8;

        /// <summary>
        /// Allowed domains.
        /// </summary>
        private readonly string[] _allowedDomains;

        public CrawlerService(
            ILogger<CrawlerService> logger,
            IServiceProvider provider,
            IConfiguration configuration)
        {
            _logger = logger;
            _provider = provider;
            _configuration = configuration;

            // Recovers allowed domains.
            _allowedDomains = _configuration
                .GetSection("CrawlSettings:AllowedDomains")
                .Get<string[]>();

            // Gets the root page from appsettings.
            _rootPage = _configuration.GetSection("CrawlSettings:Root").Get<string>();

            // Gets the timeout.
            _ = int.TryParse(_configuration.GetSection("CrawlSettings:Delay").Get<string>(), out int _defaultTimeout);

            _serviceScope = _provider.CreateScope();
            _webPageRepository = _serviceScope.ServiceProvider.GetRequiredService<PageDatabaseRepository>();
            _luceneSearchEngineService = _serviceScope.ServiceProvider.GetRequiredService<LuceneSearchEngineService>();
        }

        /// <summary>
        /// Starts the service.
        /// </summary>
        /// <param name="stoppingToken"></param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service started at {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                // GET data from settings
                if (_isFirstRunFlag)
                {
                    // Creates a new task.
                    _crawlers.Add(new PageWebDoc(_rootPage));

                    _isFirstRunFlag = false;
                }

                List<PageDatabaseItem> pages = await _webPageRepository.GetLatestAsync(CRAWLER_LIMIT);

                // GET data from database
                foreach (var page in pages)
                {
                    _crawlers.Add(new PageWebDoc(page.Url));
                }

                // Starts crawlers in Parallel.
                Parallel.ForEach(
                    _crawlers,
                    new ParallelOptions { MaxDegreeOfParallelism = CRAWLER_LIMIT },
                    crawlerunit => Task.Run(async () =>
                    {
                        try
                        {
                            // If successful, remove from the stack.
                            if(await DoWorkAsync(crawlerunit).ConfigureAwait(false))
                                _crawlers.Remove(crawlerunit);
                        }
                        catch (Exception e)
                        {
                            _logger.LogInformation("Error: {0}", e);

                        }
                    }));

                _logger.LogInformation("Running each {0} (ms)", _defaultTimeout);

                await Task
                    .Delay(_defaultTimeout, stoppingToken)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Do the crawling.
        /// </summary>
        private async Task<bool> DoWorkAsync(PageWebDoc page)
        {
            string workerId = Guid.NewGuid().ToString();

            try
            {
                _logger.LogInformation($"Worker[{workerId}] New worker crawling the link: {page.PageUrl}");

                // Saves all new links to SQLite
                foreach (string link in page.PageLinks)
                {
                    // If the url isn't in the allowed domains, skips.
                    if (!_allowedDomains.Any(l => link.StartsWith(l)))
                        continue;

                    await _webPageRepository
                        .AddPageAsync(link)
                        .ConfigureAwait(false);
                }

                // Saves the content in Lucene.NET.
                _luceneSearchEngineService.AddOrUpdateToIndex(page);

                // Remove from the database.
                await _webPageRepository.SetCrawledAsync(page.PageUrl).ConfigureAwait(false);

                return true;
            }
            catch (Exception e)
            {
                _logger.LogError($"Worker[{workerId}] threw an error: {e}.");
                throw;
            }
            finally
            {
                _logger.LogInformation($"Worker[{workerId}] finished.");
            }

        }

        public override void Dispose()
        {
            base.Dispose();
            _serviceScope.Dispose();
        }

    }
}
