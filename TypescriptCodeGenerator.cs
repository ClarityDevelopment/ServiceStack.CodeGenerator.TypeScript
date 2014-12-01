using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*
 * Todo:
 *      Instead of scanning for all classes that have the route attribute, look for all ServiceStack.Services and then only grab the routes
 *      that are implemented.
 * 
 *      Types that have a default value (number, string, bool) should be optional parameters,
 *          unless marked with an ApiMember.isRequired
 *      
 *      Combine routes into a nested hierarchy
 *         Instead of:
 *              /Product
 *              /Products
 *              /ProductType
 *              /ProductTypes
 *         Have:
 *              /Product/Product
 *              /Product/Products
 *              /Product/ProductType
 *              /Product/ProductTypes
 */


namespace Clarity.TypeScript.CodeGenerator
{
    using System.CodeDom.Compiler;
    using System.IO;
    using System.Net.Configuration;
    using System.Reflection;
    using System.Runtime.InteropServices;

    using ServiceStack;

    public class TypescriptCodeGenerator
    {
        readonly HashSet<Type> _DTOs = new HashSet<Type>();

        private IndentedTextWriter _ClientWriter;

        private IndentedTextWriter _ClientRoutesWriter;

        private IndentedTextWriter _RoutesWriter;

        private readonly string _ModuleNamespace;

        private readonly string[] _ExclusionNamespaces;

        private readonly string _ServiceName;

        private const string TAB = "    ";

        public TypescriptCodeGenerator(Type routeType, string moduleNamespace, string[] exclusionNamespaces)
            : this(new[] { routeType }, moduleNamespace, exclusionNamespaces)
        {

        }

        public TypescriptCodeGenerator(
            IEnumerable<Type> routeTypes,
            string moduleNamespace) : this(routeTypes, moduleNamespace, new string[]{})
        {
            
        }

        public TypescriptCodeGenerator(IEnumerable<Type> routeTypes, string moduleNamespace, string[] exclusionNamespaces, string serviceName = "")
        {
            _ModuleNamespace = moduleNamespace;
            _ExclusionNamespaces = exclusionNamespaces;
            _ServiceName = serviceName;
            ProcessTypes(routeTypes);
        }

        public string GenerateClient()
        {
            var writer = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 0 };
            writer.WriteLine("///<reference path=\"dtos.ts\"/>");
            writer.WriteLine("///<reference path=\"routes.ts\"/>");

            writer.WriteLine("\nmodule " + _ModuleNamespace + " {");
            writer.Indent++;


            // Code to expose the service
            writer.WriteLine(@"angular
        .module('" + _ModuleNamespace + @"', [])
        .service('" + _ServiceName + @"', ($http : ng.IHttpService) => {
            return new Client($http);
        });");

            writer.WriteLine(_ClientWriter.InnerWriter);

            writer.Indent--;
            writer.WriteLine("}");

            return writer.InnerWriter.ToString();
        }

        public string GenerateDtos()
        {
            var writer = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 0 };
            writer.WriteLine("module " + _ModuleNamespace + ".dtos {");
            writer.Indent++;
           
            // Write out the DTOs
            foreach (var dto in _DTOs.OrderBy(t => t.Name))
            {
                writer.WriteLine("export interface " + dto.Name + " {");
                writer.Indent++;

                foreach (var property in dto.GetProperties().Where(prop => prop.CanRead && prop.CanWrite))
                {
                    try
                    {
                        var returnType = property.GetMethod.ReturnType;
                        // Optional?
                        if (returnType.IsNullableType() || returnType.IsClass())
                        {
                            writer.WriteLine(property.Name + "?: " + DetermineTsType(returnType) + ";");
                        }
                        else // Required 
                        {
                            writer.WriteLine(property.Name + ": " + DetermineTsType(returnType) + ";");
                        }
                    }
                    catch (Exception e)
                    {
                        writer.WriteLine("// ERROR - Unable to emit property " + property.Name);
                        writer.WriteLine("//     " + e.Message);
                    }
                }

                writer.Indent--;
                writer.WriteLine("}\n");
            }

            writer.Indent--;
            writer.WriteLine("}");

            return writer.InnerWriter.ToString();
        }

        public string GenerateRoutes()
        {
            var writer = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 0 };
            writer.WriteLine("///<reference path=\"dtos.ts\"/>");

            writer.WriteLine("\nmodule " + _ModuleNamespace + ".routes {");
            writer.Indent++;


            writer.WriteLine(@"
    /**
      Base class for classes implementing communication with a service stack route.
    */
    export class RouteAggregator {
        public client : Client;
        get rootUrl() : string { return this.client.rootUrl; }
        get $http() : ng.IHttpService { return this.client.$http; }

        constructor(client: Client) {
            this.client = client;            
        }
    }
");

            writer.WriteLine(_RoutesWriter.InnerWriter);

            writer.Indent--;
            writer.WriteLine("}");

            return writer.InnerWriter.ToString();
        }

        private void ProcessTypes(IEnumerable<Type> routeTypes)
        {
            _ClientWriter = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 1 };
            _ClientWriter.WriteLine(@"
    /**
    Exposes access to the ServiceStack routes
    */
    export class Client {");
            _ClientWriter.Indent++;

            _ClientRoutesWriter = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 2 };
            var clientConstructorWriter = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 1 };
            clientConstructorWriter.WriteLine(
@"
        private _$http: ng.IHttpService;
        get $http():ng.IHttpService {
                return this._$http;
            }
        

        private _rootUrl: string;

        get rootUrl():string {
                return this._rootUrl;
            }
            set rootUrl(value:string) {
                // Remove trailing slash if it exists
                this._rootUrl = value.substr(-1) != '/'
                    ? value
                    : value.substr(0, value.length - 1);
            }
  
        constructor($http: ng.IHttpService) {
            this._$http = $http;
");
            clientConstructorWriter.Indent += 2;

            _RoutesWriter = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 1 };

            foreach (var routeRoot in GetRegisteredServiceStackRoutes(routeTypes).OrderBy(rr => rr.Key))
            {
                var classWriter = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 1 };
                var routeDtosWriter = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 1 };

                _ClientRoutesWriter.WriteLine(routeRoot.Key.ToCamelCase() + ": routes." + routeRoot.Key);

                classWriter.WriteLine(
                    "export class " + routeRoot.Key + " extends RouteAggregator {");

                clientConstructorWriter.WriteLine(
                    "this." + routeRoot.Key.ToCamelCase() + " = new routes." + routeRoot.Key + "(this);");

                classWriter.Indent++;

                foreach (var type in routeRoot.Value.Keys.OrderBy(t => t.Name))
                {
                    try
                    {
                        var returnTsType = DetermineTsType(type);

                        if (routeRoot.Value[type].Count > 1)
                        {
                            _ClientWriter.WriteLine(
                                "// " + type
                                + "/ exports multiple routes.  Typescript does not support operator overloading and this operation is not supported.  Make seperate routes instead.");
                        }

                        foreach (var route in routeRoot.Value[type])
                        {
                            WriteMethodHeader(classWriter, type, route);

                            var cg = new RouteCodeGeneration(this, route, type, returnTsType);

                            // Translate the path into a coded URL
                            // We may have tokens like {ID} in the route
                            cg.ParseRoutePath();

                            // Generate code for route properties
                            cg.ProcessRouteProperties();

                            foreach (var verb in cg.Verbs)
                            {
                                WriteTypescriptMethod(classWriter, routeDtosWriter, cg, verb, cg.Verbs.Length > 1, verb == cg.Verbs[0]);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _RoutesWriter.WriteLine("// ERROR Processing " + routeRoot.Key + " - " + type.Name);
                        _RoutesWriter.WriteLine("//    " + e.Message);
                    }
                }

                // Close the class
                classWriter.Indent--;
                classWriter.WriteLine("}\n");

                _RoutesWriter.WriteLine(routeDtosWriter.InnerWriter);
                _RoutesWriter.WriteLine(classWriter.InnerWriter);
            }

            clientConstructorWriter.Indent--;
            clientConstructorWriter.WriteLine("}");
            _ClientWriter.WriteLine(clientConstructorWriter.InnerWriter);

            _ClientWriter.WriteLine(_ClientRoutesWriter.InnerWriter);

            // End of Client Repository Singleton 
            _ClientWriter.Indent--;
            _ClientWriter.WriteLine("}");
        }

        private void WriteMethodHeader(IndentedTextWriter writer, Type type, RouteAttribute route)
        {
            writer.WriteLine("/**");
            writer.WriteLine("C# Type:  " + type.Namespace + "." + type.Name);
            if (!string.IsNullOrEmpty(route.Path)) writer.WriteLine("Path: " + route.Path);
            if (!string.IsNullOrEmpty(route.Verbs)) writer.WriteLine("Verbs: " + route.Verbs);
            if (!string.IsNullOrEmpty(route.Summary)) writer.WriteLine("Summary:  " + route.Summary);
            if (!string.IsNullOrEmpty(route.Notes)) writer.WriteLine("Notes:  " + route.Notes);
            writer.WriteLine("*/");
        }

        private void WriteTypescriptMethod(IndentedTextWriter classWriter, IndentedTextWriter routeDtoWriter, RouteCodeGeneration cg, string verb, bool includeVerbNameInMethod, bool writeInputDto)
        {
            classWriter.Write(cg.RouteType.Name + (includeVerbNameInMethod ? "_" + verb : string.Empty));
            classWriter.Write(" = (");

            // Optional parameters must come last in typescript
            for (int i = 0; i < cg.MethodParameters.Count; i++)
            {
                if (i > 0)
                {
                    classWriter.Write(", ");
                }

                classWriter.Write(cg.MethodParameters[i]);
            }

            if (cg.RouteInputPropertyLines.Count > 0)
            {
                if (cg.MethodParameters.Count > 0)
                    classWriter.Write(", ");

                classWriter.Write("routeParams : " + cg.RouteInputDtoName);

                if (writeInputDto)
                {
                    routeDtoWriter.WriteLine("export interface " + cg.RouteInputDtoName + " {");
                    routeDtoWriter.Indent++;

                    foreach (var line in cg.RouteInputPropertyLines)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            routeDtoWriter.WriteLine(line);
                        }
                    }

                    routeDtoWriter.Indent--;
                    routeDtoWriter.WriteLine("}");
                }
            }

            classWriter.Write(")");

            /*
            for (int i = 0; i < cg.MethodParametersOptional.Count; i++)
            {
                if (i > 0 || cg.MethodParameters.Count > 0)
                {
                    classWriter.Write(", ");
                }

                classWriter.Write(cg.MethodParametersOptional[i]);
            }
            classWriter.Write(");
             */
            classWriter.WriteLine(" => {");

            WriteClassBody(classWriter, cg, verb);
        }

        private void WriteClassBody(IndentedTextWriter classWriter, RouteCodeGeneration cg, string verb)
        {
            classWriter.Indent++;
            /*
                        if (cg.RouteInputProperties.Any())
                        {
                            classWriter.WriteLine("var jsonData = {");
                            classWriter.Indent++;

                            for (var i = 0; i < cg.RouteInputProperties.Count; i++)
                            {
                                classWriter.WriteLine(cg.RouteInputProperties[i] + (i + 1 < cg.RouteInputProperties.Count ? "," : ""));
                            }

                            classWriter.Indent--;
                            classWriter.WriteLine("};");
                        }*/

            classWriter.WriteLine("return this.$http<" + cg.ReturnTsType + ">({");
            classWriter.Indent++;

            // Write out the url array
            classWriter.Write("url:  [");
            for (int i = 0; i < cg.UrlPath.Count; i++)
            {
                if (i > 0)
                {
                    classWriter.Write(", ");
                }
                classWriter.Write(cg.UrlPath[i]);
            }
            classWriter.WriteLine("].join('/'),");
            classWriter.WriteLine("method: '" + verb.ToUpper() + "',");

            if (cg.RouteInputPropertyLines.Count > cg.MethodParameters.Count)
            {
                if (verb.ToUpper() == "GET")
                {
                    classWriter.WriteLine("params:  routeParams");
                }
                else
                {
                    classWriter.WriteLine("data:  routeParams");
                }
            }

            classWriter.Indent--;

            classWriter.WriteLine("});");

            // Close the method
            classWriter.Indent--;
            classWriter.WriteLine("}");
            classWriter.WriteLine();
        }

        internal string DetermineTsType(Type type)
        {
            if (type == typeof(bool)) return "boolean";
            else if (type == typeof(string) || type == typeof(char)) return "string";
            else if (type == typeof(int) || type == typeof(byte) || type == typeof(short)
                || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort)
                || type == typeof(Int64) || type == typeof(UInt64)
                || type == typeof(double) || type == typeof(decimal)) return "number";
            else if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return "Date";

            string result;
            if (type.HasAttribute<RouteAttribute>())
            {
                var interfaces = type.GetTypeInterfaces();
                if (interfaces.Any(ti => ti == typeof(IReturnVoid)))
                {
                    result = "void";
                }
                else
                {
                    if (type.HasInterface(typeof(IReturn)))
                    {
                        // When a route implements IReturn<whatever> it ends up with two interfaces on it
                        // One is a straight IReturn, the other is a generic IReturn
                        var ireturn = type.Interfaces().FirstOrDefault(ti => ti.IsGenericType && ti.Name.StartsWith("IReturn`"));

                        if (ireturn == null)
                        {
                            result = "void";
                        }
                        else
                        {
                            result = DetermineTsType(ireturn.GenericTypeArguments[0]);
                        }
                    }
                    else
                    {
                        result = "void";
                    }
                }
            }
            else if (type.IsArray())
            {
                result = "Array<" + DetermineTsType(type.GetElementType()) + ">";
            }
            else if (type.IsNullableType())
            {
                // int?, bool?, etc.  Use the more underlying type
                result = DetermineTsType(type.GenericTypeArguments[0]);
            }
            else if (type.IsGenericType)
            {
                var genericDefinition = type.GetGenericTypeDefinition();

                if (genericDefinition.Name.StartsWith("Dictionary") || genericDefinition.Name.StartsWith("IDictionary"))
                {
                    result = "{[name: " + DetermineTsType(type.GenericTypeArguments[0]) + "]: " + DetermineTsType(type.GenericTypeArguments()[1]) + "}";
                }
                else if (genericDefinition.Name.StartsWith("List`")
                    || genericDefinition.Name.StartsWith("IList`")
                    || genericDefinition.Name.StartsWith("ICollection`")
                    || genericDefinition.Name.StartsWith("IEnumerable`"))
                {
                    result = "Array<" + DetermineTsType(type.GenericTypeArguments[0]) + ">";
                }
                else
                {
                    throw new Exception("Error processing " + type.Name + " - Unknown generic type " + type.GetGenericTypeDefinition().Name);
                }
            }
            else
            {
                if (type.Namespace != null && !_DTOs.Contains(type) && !_ExclusionNamespaces.Any(ns => type.Namespace.StartsWith(ns)))
                {
                    _DTOs.Add(type);

                    // Since the DTO might expose other DTOs we need to examine all of the return types of properties
                    foreach (var property in type.Properties().Where(p => p.CanRead && p.CanWrite))
                    {
                        DetermineTsType(property.GetMethod.ReturnType);
                    }
                }

                // We put our models in a dtos module in typescript
                result = _DTOs.Contains(type) ? "dtos." + type.Name : "any";
            }

            return result;
        }

        /*private static Dictionary<string, Dictionary<Type, List<RouteAttribute>>> GetRegisteredServiceStackRoutes()
        {
            var routeTypes =
                AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.FullName.StartsWith("Clarity.Ecommerce.Service"))
                    .SelectMany(
                        a =>
                        a.GetTypes()
                            .Where(t => t.CustomAttributes.Any(attr => attr.AttributeType == typeof(RouteAttribute))));
        }*/

        Dictionary<string, Dictionary<Type, List<RouteAttribute>>> GetRegisteredServiceStackRoutes(IEnumerable<Type> routeTypes)
        {
            var routes = new Dictionary<string, Dictionary<Type, List<RouteAttribute>>>();
            foreach (var rt in routeTypes)
            {
                foreach (var attribute in rt.GetCustomAttributes(typeof(RouteAttribute)))
                {
                    var route = (RouteAttribute)attribute;
                    var pathHierarchy = route.Path.TrimStart('/').Split('/');
                    var root = pathHierarchy[0];

                    if (!routes.ContainsKey(root))
                    {
                        routes.Add(root, new Dictionary<Type, List<RouteAttribute>>());
                    }

                    if (!routes[root].ContainsKey(rt))
                    {
                        routes[root].Add(rt, new List<RouteAttribute>());
                    }

                    routes[root][rt].Add(route);
                }
            }
            return routes;
        }
    }

    internal class RouteCodeGeneration
    {
        private readonly TypescriptCodeGenerator _CodeGenerator;

        public int ParamsWritten { get; set; }

        public List<string> MethodParameters { get; private set; }
        public List<string> MethodParametersOptional { get; private set; }


        public Type RouteType { get; private set; }

        public List<string> UrlPath { get; private set; }
        public HashSet<string> PropertiesProcessed { get; private set; }
        public List<string> RouteInputPropertyLines { get; private set; }
        public string[] Verbs { get; private set; }
        public RouteCodeGeneration(TypescriptCodeGenerator codeGenerator, RouteAttribute route, Type routeType, string returnTsType)
        {
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
        }

        public string ReturnTsType { get; private set; }

        public RouteAttribute Route { get; private set; }

        public string RouteInputDtoName
        {
            get
            {
                return RouteType.Name + "Dto";
            }
        }

        public void ParseRoutePath()
        {
            var pathHierarchy = Route.Path.Trim('/').Split('/');

            for (var i = 0; i < pathHierarchy.Length; i++)
            {
                var param = pathHierarchy[i];

                if (!IsRouteParam(param))
                {
                    UrlPath.Add("\"" + param + "\"");
                }
                else
                {
                    ProcessRouteParameter(param);
                }
            }
        }

        private void ProcessRouteParameter(string param)
        {
            param = param.Trim('{', '}');

            PropertyInfo property = null;
            try
            {
                property = RouteType.GetProperty(param);
            }
            catch (Exception)
            {
            }

            if (property == null)
            {
                MethodParameters.Add("\n      /* CANNOT FIND " + param + " */\n   ");
            }
            else
            {
                param = param.ToCamelCase();
                ProcessClrProperty(property, IsRouteParam: true);

                // Uri Encode string parameters.
                if (property.GetGetMethod().ReturnType == typeof(string))
                {
                    UrlPath.Add("encodeURIComponent(" + param + ")");
                }
                else
                {
                    UrlPath.Add(param);
                }

            }
        }

        /// <summary>
        /// Generates typescript code for a given property
        /// </summary>
        /// <param name="property"></param>
        /// <param name="IsRouteParam"></param>
        private void ProcessClrProperty(PropertyInfo property, bool IsRouteParam = false)
        {
            this.PropertiesProcessed.Add(property.Name);

            // TODO: Add comments for ApiMember properties
            var docAttr = property.GetCustomAttribute<ApiMemberAttribute>();
            
            var returnType = property.GetMethod.ReturnType;
            // Optional parameters
            if (!IsRouteParam &&
                (returnType.IsNullableType() || returnType.IsClass()) && (docAttr == null || !docAttr.IsRequired)) // Optional param.  Could be string or a DTO type.  
            {
                /*MethodParametersOptional.Add(property.Name.ToCamelCase() + "?: "
                                  + _CodeGenerator.DetermineTsType(returnType));
            */
                if (docAttr != null)
                {
                    RouteInputPropertyLines.Add(EmitComment(docAttr));
                }
                RouteInputPropertyLines.Add(property.Name + "?: " + _CodeGenerator.DetermineTsType(returnType) + ";");
            }
            else // Required parameter
            {
                if (!IsRouteParam)
                {
                    if (docAttr != null)
                    {
                        RouteInputPropertyLines.Add(EmitComment(docAttr));
                    }
                    RouteInputPropertyLines.Add(property.Name + ": " + _CodeGenerator.DetermineTsType(returnType) + ";");
                }
                else
                {
                    MethodParameters.Add(
                        property.Name.ToCamelCase() + ": " + _CodeGenerator.DetermineTsType(returnType));
                }
            }

            ParamsWritten++;
        }

        private string EmitComment(ApiMemberAttribute docAttr)
        {
            var result = string.Empty;
            if (!string.IsNullOrEmpty(docAttr.Description))
            {
                result += "// " + docAttr.Description;
            }
            return result;
        }

        public void ProcessRouteProperties()
        {
            foreach (var property in
                RouteType.GetProperties()
                    .Where(p => p.HasAttribute<ApiMemberAttribute>() || (p.CanRead && p.CanWrite)))
            {
                if (PropertiesProcessed.Contains(property.Name)) continue;

                ProcessClrProperty(property);
            }
        }

        private static bool IsRouteParam(string param)
        {
            return param.StartsWith("{") && param.EndsWith("}");
        }
    }

}