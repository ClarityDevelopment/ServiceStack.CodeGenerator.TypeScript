using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceStack.CodeGenerator.TypeScript.Tests {
    public class SearchProductCatalog {
        public bool TestBool { get; set; }
        public string TestString { get; set; }
    }
    [Route("/Test/GetMetadata")]
    public class GetMetadata : IReturnVoid {
        [ApiMember]
        public Dictionary<int, SearchProductCatalog> categories { get; set; }

        [ApiMember]
        public Dictionary<int, SearchProductCatalog> price { get; set; }
    }
}
