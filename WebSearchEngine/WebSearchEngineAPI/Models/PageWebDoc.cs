using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSearchEngineAPI.Models
{
    /// <summary>
    /// Data model with content to be used in Lucene.NET
    /// </summary>
    public class PageWebDoc : IDisposable
    {
        private HtmlDocument _doc;

        public PageWebDoc(string url)
        {
            PageUrl = url;

            // Recupera o documento
            HtmlWeb web = new()
            {
                AutoDetectEncoding = false,
                OverrideEncoding = Encoding.UTF8,
                UserAgent = "FeevaleBot"
            };

            _doc = web.Load(url);
        }

        /// <summary>
        /// Page URL.
        /// </summary>
        public string PageUrl { get; }

        /// <summary>
        /// When the last crawl was made.
        /// </summary>
        public DateTime LastCrawl => DateTime.Now;

        /// <summary>
        /// The page title.
        /// </summary>
        public string Title
        {
            get
            {
                var words = _doc.DocumentNode?.SelectSingleNode("//head/title")?.InnerText.Trim();
                return words != null ? string.Join(" ", words.Trim()) : string.Empty;
            }
        }

        /// <summary>
        /// The HTML content from the page.
        /// </summary>
        public string Content
        {
            get
            {
                // removes HTML comments
                _doc.DocumentNode.Descendants()
                    .Where(n => n.NodeType == HtmlNodeType.Comment)
                    .ToList()
                    .ForEach(n => n.Remove());

                // gets the text
                var words = _doc.DocumentNode?.SelectNodes("//body//text()")?.Select(x => x.InnerText.Trim());

                return words != null ? string.Join(" ",
                    words.Where(w => !string.IsNullOrWhiteSpace(w)).Select(w => w.Trim())) : string.Empty;
            }
        }

        /// <summary>
        /// Gets the description metatag.
        /// </summary>
        public string Description
        {
            get
            {
                var words = _doc.DocumentNode?.SelectSingleNode("//head/description")?.InnerText.Trim();
                return words != null ? string.Join(" ", words.Trim()) : string.Empty;
            }
        }

        /// <summary>
        /// Gets the page links to be used by the crawler.
        /// </summary>
        public ICollection<string> PageLinks
        {
            get
            {
                List<string> result = new List<string>();

                if (_doc.DocumentNode?.SelectNodes("//a[@href]") != null)
                {
                    foreach (HtmlNode link in _doc.DocumentNode?.SelectNodes("//a[@href]"))
                    {
                        // Recupera o link
                        string linkValue = link.Attributes["href"].Value.Trim();

                        // Converte o link relativo
                        if (linkValue.StartsWith("/"))
                            linkValue = string.Concat(PageUrl, linkValue);

                        if (!string.IsNullOrEmpty(linkValue))
                            result.Add(linkValue);

                        // Ignores empty or parameter links
                        if (
                            !string.IsNullOrEmpty(linkValue)
                            && !linkValue.StartsWith("#")
                            && !linkValue.StartsWith("?")
                            && !linkValue.StartsWith("javascript")
                            && !linkValue.StartsWith("mailto:")
                            && !linkValue.StartsWith("tel:")
                            )
                        {
                            result.Add(linkValue);
                        }
                    }
                }

                return result;
            }
        }

        public void Dispose()
        {
            _doc = null;
        }
    }
}
