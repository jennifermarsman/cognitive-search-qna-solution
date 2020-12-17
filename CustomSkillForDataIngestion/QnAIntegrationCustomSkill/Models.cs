using Azure.Search.Documents.Models;
using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker.Models;
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
        public Dictionary<string, IList<FacetValue>> facets { get; set; }
        public QnASearchResult answers { get; set; }
    }

    class LookupOutput
    {
        public string sasToken { get; set; }
        public SearchDocument document { get; set; }

    }

    class GetKbOutput
    {
        public string QnAMakerKnowledgeBaseID { get; set; }
    }

    public class Facet
    {
        public string key { get; set; }
        public List<FacetValue> value { get; set; }
    }

    public class FacetValue
    {
        public string value { get; set; }
        public long? count { get; set; }
    }
}
