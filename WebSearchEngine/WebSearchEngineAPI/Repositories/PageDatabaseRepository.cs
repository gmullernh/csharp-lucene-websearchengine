using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebSearchEngineAPI.Persistence;

namespace WebSearchEngineAPI.Repositories
{
    /// <summary>
    /// Data model used by SQLite.
    /// </summary>
    public class PageDatabaseItem
    {
        [Key]
        public int Id { get; set; }
        public string Url { get; set; }
        public bool WasCrawled { get; set; }
    }

    /// <summary>
    /// Page repository
    /// </summary>
    public class PageDatabaseRepository
    {
        private readonly WebCrawlerContext _context;
        private readonly object _lock = new();

        public PageDatabaseRepository(WebCrawlerContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Add a new page to the database.
        /// </summary>
        /// <param name="pageUrl"></param>
        /// <returns></returns>
        public async Task AddPageAsync(string pageUrl)
        {
            Monitor.Enter(_lock);

            string tmpUrl = pageUrl.ToLowerInvariant().Trim();

            // If already exists, return.
            if (await GetByUrlAsync(tmpUrl).ConfigureAwait(false) != null)
                return;

            await _context.Items.AddAsync(new PageDatabaseItem() { Url = tmpUrl }).ConfigureAwait(false);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            Monitor.Exit(_lock);
        }

        /// <summary>
        /// Sets that a page has been crawled.
        /// </summary>
        /// <param name="pageUrl"></param>
        /// <returns></returns>
        public async Task SetCrawledAsync(string pageUrl)
        {
            Monitor.Enter(_lock);

            PageDatabaseItem pageDatabaseItem = await GetByUrlAsync(pageUrl).ConfigureAwait(false);
            if (pageDatabaseItem == null)
                return;

            pageDatabaseItem.WasCrawled = true;

            _context.Items.Update(pageDatabaseItem);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            Monitor.Exit(_lock);
        }

        /// <summary>
        /// Get a page by its url.
        /// </summary>
        /// <param name="pageUrl"></param>
        /// <returns></returns>
        private async Task<PageDatabaseItem> GetByUrlAsync(string pageUrl)
        {
            return
                await 
                _context
                .Items
                .Where(p => p.Url == pageUrl)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Recover latest not crawled urls.
        /// </summary>
        public async Task<List<PageDatabaseItem>> GetLatestAsync(int amount)
        {
            return await _context
                .Items
                .Where(p => !p.WasCrawled)
                .Take(amount)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all pages.
        /// </summary>
        /// <returns></returns>
        public List<PageDatabaseItem> GetAllPages()
        {
            return _context.Items.ToList();
        }

    }
}
