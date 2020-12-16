using Azure.Search.Documents.Models;
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
}
