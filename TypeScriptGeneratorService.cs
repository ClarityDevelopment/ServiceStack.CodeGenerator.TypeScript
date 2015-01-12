namespace ServiceStack.CodeGenerator.TypeScript {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    [Route("/CodeGen/", "GET", Summary = @"Generates typescript from our routes.")]
    public class CodeGenRoute : IReturn<string> {
        #region Public Properties

        [ApiMember(IsRequired = false)]
        public string ClrNamespace { get; set; }

        [ApiMember(IsRequired = false)]
        public string TypeNamePattern { get; set; }

        #endregion
    }

    public class CodeGenService : Service {
        #region Public Methods and Operators

        public string Any(CodeGenRoute codeGen) {
            // http://localhost/service/CodeGen?TypeNamePattern=GetShipments
            var routeTypes =
                AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.FullName.StartsWith(string.IsNullOrEmpty(codeGen.ClrNamespace) ? "Clarity.Ecommerce.Service" : codeGen.ClrNamespace))
                    .SelectMany(a => a.GetTypes().Where(t => t.CustomAttributes.Any(attr => attr.AttributeType == typeof(RouteAttribute))))
                    .ToList();

            if (!string.IsNullOrEmpty(codeGen.TypeNamePattern)) {
                var r = new Regex(codeGen.TypeNamePattern);
                routeTypes = routeTypes.Where(rt => r.Match(rt.Name).Success).ToList();
            }

            var cg = new TypescriptCodeGenerator(routeTypes, "cv.cef.api", new[] { "Clarity.Ecommerce.DataModel" });
            return cg.Generate();
        }

        #endregion
    }
}