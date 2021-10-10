# Search Engine Lucene.NET

This is a simple web application, that contains:

- a Crawler Service that runs in the background fetching pages from a specific domain and translating them to a Lucene.NET database.
- a SQLite database to retain page info for the crawler.
- a Lucene.NET database for text document search.
- a web API to recover the fetched content as JSON.

## Querying

You can query the content using the endpoint `/search/page/amount/term` as eg. `/search/0/10/example`.

## Analyzer

By default, this project is using the Brazillian Portuguese analyzer.