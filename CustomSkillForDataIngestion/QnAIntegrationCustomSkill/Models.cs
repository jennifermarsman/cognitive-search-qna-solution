using Azure.Search.Documents.Models;
using Microsoft.Azure.Documents;
using System;
using System.Collections.Generic;
using System.Text;

namespace QnAIntegrationCustomSkill
{
    class SearchOutput
    {
        public long? count { get; set; }
        public List<SearchResult<SearchDocument>> results { get; set; }
    }

    class LookupOutput
    {
        public string sasToken { get; set; }
        public SearchDocument document { get; set; }
    }
}
