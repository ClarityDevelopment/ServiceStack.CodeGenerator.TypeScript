using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceStack.CodeGenerator.TypeScript {
    [AttributeUsage(AttributeTargets.Class)]
    public class TypescriptCodeGeneratorAttribute : Attribute {
        public TypescriptCodeGeneratorAttribute() {
            CacheResult = false;
        }

        public bool CacheResult { get; set; }
    }
}
