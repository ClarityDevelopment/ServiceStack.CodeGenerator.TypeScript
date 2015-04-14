using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceStack.CodeGenerator.TypeScript {
    using System.CodeDom.Compiler;
    using System.IO;
    using System.Reflection;

    public partial class TypescriptCodeGenerator {
        /// <summary>        
        /// Responsible for transforming our .NET ServiceStack Routes into TypeScript code capable of calling those routes
        /// 
        /// Here's a sample:
        ///     C#: 
        ///            [Route("/Dashboard/SalesGrossVsNet", "POST")]
        ///            public class GetSalesGrossVsNet : IReturn<List<GrossVsNetSales>> {
        ///                 [ApiMember()]
        ///                 public string StartDate { get; set; }
        ///                 [ApiMember]
        ///                 public string EndDate { get; set; }
        ///             }
        /// 
        ///             public class GrossVsNetSales {
        ///                 public DateTime Date { get; set; }	
        ///                 public double GrossSales {get; set;}
        ///                 public double NetSales {get; set;}
        ///             }
        ///     
        ///     Typescript:
        ///         export interface GrossVsNetSales {
        ///             Date: Date;
        ///             GrossSales: number;
        ///             NetSales: number;
        ///         }
        /// 
        ///         /**
        ///          * Route:      /Dashboard/SalesGrossVsNet
        ///          * Source:     Clarity.Ecommerce.Framework.Dashboards.GetSalesGrossVsNet
        ///         */
        ///         GetSalesGrossVsNet = (routeParams ?: GetSalesGrossVsNetDto) => {
        ///             return this.$http<Array<GrossVsNetSales>>({
        ///                 url:  [this.rootUrl, "Dashboard", "SalesGrossVsNet"].join('/'),
        ///                 method: 'POST',
        ///                 data:  routeParams
        ///             });
        ///         }        
        /// </summary>
        /// <param name="writer"></param>
        private void WriteRouteClasses(IndentedTextWriter writer) {
            writer.WriteLine(@"   
    // ----- Routes -----
    
    /**
    * Base class for service stack routes
    */
    export class ServiceStackRoute {
        public service : " + _ServiceName + @";
        
        // The root URL for making RESTful calls
        get rootUrl() : string { return this.service.rootUrl; }
        get $http() : ng.IHttpService { return this.service.$http; }

        constructor(service: " + _ServiceName + @") {
            this.service = service;                
        }
    }
");
            
            foreach (var routeRoot in _ServiceStackRoutes) {
                var routeClassWriter = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 1 };
                var routeDtosWriter = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 1 };

                routeClassWriter.WriteLine("export class " + routeRoot.Key + " extends ServiceStackRoute {");

                routeClassWriter.Indent++;

                foreach (Type type in routeRoot.Value.Keys.OrderBy(t => t.Name)) {
                    try {
                        string returnTsType = DetermineTsType(type);

                        if (routeRoot.Value[type].Count > 1) {
                            routeClassWriter.WriteLine(
                                "// " + type + "/ exports multiple routes.  Typescript does not support operator overloading and this operation is not supported.  Make seperate routes instead.");
                        }

                        var customAttribute = type.GetCustomAttribute<TypescriptCodeGeneratorAttribute>();
                        if (customAttribute != null && customAttribute.CacheResult) {
                            routeClassWriter.WriteLine("private _" + type.Name + "Cached: ng.IHttpPromise<" + returnTsType + ">;");
                        }

                        foreach (RouteAttribute route in routeRoot.Value[type]) {
                            WriteMethodHeader(routeClassWriter, type, route);

                            var cg = new RouteCodeGeneration(this, route, type, returnTsType);

                            // Translate the path into a coded URL
                            // We may have tokens like {ID} in the route
                            cg.ParseRoutePath();

                            // Generate code for route properties
                            cg.ProcessRouteProperties();

                            foreach (string verb in cg.Verbs) WriteTypescriptMethod(routeClassWriter, routeDtosWriter, cg, verb, cg.Verbs.Length > 1, verb == cg.Verbs[0]);
                        }
                    }
                    catch (Exception e) {
                        writer.WriteLine("// ERROR Processing " + routeRoot.Key + " - " + type.Name);
                        writer.WriteLine("//    " + e.Message);
                    }
                }

                routeClassWriter.Indent--;
                routeClassWriter.WriteLine("}");

                writer.WriteLine(routeDtosWriter.InnerWriter);
                writer.WriteLine(routeClassWriter.InnerWriter);
            }           
        }
    }

    internal class RouteCodeGeneration {
        #region Fields

        private readonly TypeScript.TypescriptCodeGenerator _CodeGenerator;

        #endregion

        #region Constructors and Destructors

        public RouteCodeGeneration(TypeScript.TypescriptCodeGenerator codeGenerator, RouteAttribute route, Type routeType, string returnTsType) {
            _CodeGenerator = codeGenerator;
            Route = route;
            RouteType = routeType;
            UrlPath = new List<string> { "this.rootUrl" };

            MethodParameters = new List<string>();
            MethodParametersOptional = new List<string>();
            PropertiesProcessed = new HashSet<string>();
            RouteInputPropertyLines = new List<string>();
            Verbs = string.IsNullOrEmpty(route.Verbs) ? new[] { "get" } : route.Verbs.ToLower().Split(',');
            ReturnTsType = returnTsType;
            RouteInputHasOnlyOptionalParams = true;
        }

        #endregion

        #region Public Properties

        public List<string> MethodParameters { get; private set; }

        public List<string> MethodParametersOptional { get; private set; }

        public int ParamsWritten { get; set; }

        public HashSet<string> PropertiesProcessed { get; private set; }

        public string ReturnTsType { get; private set; }

        public RouteAttribute Route { get; private set; }

        public string RouteInputDtoName {
            get {
                return RouteType.Name + "Dto";
            }
        }

        public List<string> RouteInputPropertyLines { get; private set; }

        public Type RouteType { get; private set; }

        public List<string> UrlPath { get; private set; }

        public string[] Verbs { get; private set; }

        /// <summary>
        /// Is every input DTO property optional?
        /// </summary>
        public bool RouteInputHasOnlyOptionalParams { get; set; }

        #endregion

        #region Public Methods and Operators

        public void ParseRoutePath() {
            string[] pathHierarchy = Route.Path.Trim('/').Split('/');

            for (int i = 0; i < pathHierarchy.Length; i++) {
                string param = pathHierarchy[i];

                if (!IsRouteParam(param)) UrlPath.Add("\"" + param + "\"");
                else ProcessRouteParameter(param);
            }
        }

        public void ProcessRouteProperties() {
            foreach (PropertyInfo property in
                RouteType.GetProperties().Where(p => p.HasAttribute<ApiMemberAttribute>() || (p.CanRead && p.CanWrite))) {
                if (PropertiesProcessed.Contains(property.Name)) continue;

                ProcessClrProperty(property);
            }
        }

        #endregion

        #region Methods

        private static bool IsRouteParam(string param) {
            return param.StartsWith("{") && param.EndsWith("}");
        }

        private string EmitComment(ApiMemberAttribute docAttr) {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(docAttr.Description)) result += "// " + docAttr.Description;
            return result;
        }

        /// <summary>
        ///     Generates typescript code for a given property
        /// </summary>
        /// <param name="property"></param>
        /// <param name="IsRouteParam"></param>
        private void ProcessClrProperty(PropertyInfo property, bool IsRouteParam = false) {
            PropertiesProcessed.Add(property.Name);

            // TODO: Add comments for ApiMember properties
            var docAttr = property.GetCustomAttribute<ApiMemberAttribute>();

            Type returnType = property.GetMethod.ReturnType;
            // Optional parameters
            if (!IsRouteParam && (returnType.IsNullableType() || returnType.IsClass()) && (docAttr == null || !docAttr.IsRequired)) // Optional param.  Could be string or a DTO type.  
            {
                /*MethodParametersOptional.Add(property.Name.ToCamelCase() + "?: "
                                  + _CodeGenerator.DetermineTsType(returnType));
            */
                if (docAttr != null) RouteInputPropertyLines.Add(EmitComment(docAttr));
                RouteInputPropertyLines.Add(property.Name + "?: " + _CodeGenerator.DetermineTsType(returnType, true) + ";");
            }
            else // Required parameter
            {
                if (!IsRouteParam) {
                    if (docAttr != null) RouteInputPropertyLines.Add(EmitComment(docAttr));
                    RouteInputHasOnlyOptionalParams = false;
                    RouteInputPropertyLines.Add(property.Name + ": " + _CodeGenerator.DetermineTsType(returnType, true) + ";");
                }
                else MethodParameters.Add(property.Name.ToCamelCase() + ": " + _CodeGenerator.DetermineTsType(returnType, true));
            }

            ParamsWritten++;
        }

        private void ProcessRouteParameter(string param) {
            param = param.Trim('{', '}');

            PropertyInfo property = null;
            try {
                property = RouteType.GetProperty(param);
            }
            catch (Exception) { }

            if (property == null) MethodParameters.Add("\n      /* CANNOT FIND " + param + " */\n   ");
            else {
                param = param.ToCamelCase();
                ProcessClrProperty(property, true);

                // Uri Encode string parameters.
                if (property.GetGetMethod().ReturnType == typeof(string)) UrlPath.Add("encodeURIComponent(" + param + ")");
                else UrlPath.Add(param);
            }
        }

        #endregion
    }

}
