using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebSearchEngineAPI.Models
{
    /// <summary>
    /// A page model.
    /// </summary>
    public class PageItem
    {
        public string PageUrl { get; set; }
        public string Content { get; set; }
        public string Description { get; set; }
        public string Title { get; set; }
        public DateTime LastCrawl { get; set; }
    }
}
