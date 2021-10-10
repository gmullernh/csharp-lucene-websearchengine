using Microsoft.EntityFrameworkCore;
using WebSearchEngineAPI.Repositories;

namespace WebSearchEngineAPI.Persistence
{
    public class WebCrawlerContext : DbContext
    {
        public DbSet<PageDatabaseItem> Items { get; set; }

        public WebCrawlerContext(DbContextOptions<WebCrawlerContext> options)
            : base(options)
        {
            Database.EnsureCreated();
        }
    }
}
