using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebSearchEngineAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly ILogger<SearchController> _logger;

        public SearchController(ILogger<SearchController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Endpoint to be queried.
        /// </summary>
        [HttpGet]
        [Route("/search/{page}/{amount}/{q}")]
        public IEnumerable<object> Get(
            [FromServices] Services.SearchEngine.LuceneSearchEngineService lucene,
            int page, int amount, string q)
        {
            try
            {
                if (string.IsNullOrEmpty(q))
                    return new List<object>();

                if (page < 0) page = 0;
                if (amount < 1) amount = 1;

                return lucene
                    .GetFromIndexPaginated(q.Split(), page, amount)
                    .Select(l =>
                    {
                        string desc = l.Content;
                        if (l.Content.Length > 140)
                        {
                            int location = l.Content.IndexOf(q);
                            desc = l.Content.Substring(location - 69, 140);
                        }

                        return new
                        {
                            Url = l.PageUrl,
                            Descricao = desc
                        };
                    }).ToArray();
            }
            catch
            {
                return new List<object>();
            }
        }


    }
}
