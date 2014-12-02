namespace ServiceStack.CodeGenerator.TypeScript
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;

    using ServiceStack;

    [Route("/CodeGen/", "GET", Summary = @"Generates typescript from our routes.")]
    public class CodeGenRoute : IReturn<string>
    {
        [ApiMember(IsRequired = false)]
        public string TypeNamePattern { get; set; }

        [ApiMember(IsRequired = false)]
        public string ClrNamespace { get; set; }
    }


    public class CodeGenService : Service
    {
        public string Any(CodeGenRoute codeGen)
        {
            // http://localhost/service/CodeGen?TypeNamePattern=GetShipments
            var routeTypes =
                AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName.StartsWith(string.IsNullOrEmpty(codeGen.ClrNamespace) ? "Clarity.Ecommerce.Service" : codeGen.ClrNamespace))
                    .SelectMany(
                        a =>
                        a.GetTypes()
                            .Where(
                                t => t.CustomAttributes.Any(attr => attr.AttributeType == typeof(RouteAttribute))));

            if (!string.IsNullOrEmpty(codeGen.TypeNamePattern))
            {
                var r = new Regex(codeGen.TypeNamePattern);
                routeTypes = routeTypes.Where(rt => r.Match(rt.Name).Success);
            }

            var cg = new TypescriptCodeGenerator(routeTypes, "cv.cef.api", new string[] { "Clarity.Ecommerce.DataModel" }, "$cef");
            return cg.GenerateClient() + "\n" + cg.GenerateDtos() + "\n" + cg.GenerateRoutes();
        }

    }
}