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

namespace ServiceStack.CodeGenerator.TypeScript {
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Security.Authentication.ExtendedProtection;

    public class TypescriptCodeGenerator {
        #region Constants

        private const string TAB = "    ";

        #endregion

        #region Fields

        private readonly HashSet<Type> _DTOs = new HashSet<Type>();

        private readonly string[] _ExclusionNamespaces;

        private IndentedTextWriter _ServiceRoutesWriter;

        private IndentedTextWriter _ServiceWriter;

        private IndentedTextWriter _RoutesWriter;

        private readonly string _ServiceName;

        #endregion

        #region Constructors and Destructors

        public TypescriptCodeGenerator(IEnumerable<Type> routeTypes, string serviceName, string[] exclusionNamespaces) {
            _ServiceName = serviceName;
            _ExclusionNamespaces = exclusionNamespaces;
            ProcessTypes(routeTypes);
        }

        #endregion

        #region Public Methods and Operators

        public string GenerateClient() {
            var writer = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 1 };

            writer.WriteLine(_ServiceWriter.InnerWriter);

            return writer.InnerWriter.ToString();
        }

        public string GenerateDtos() {
            var writer = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 1 };
            writer.WriteLine("// ----- DTOS -----");

            // Write out the DTOs
            foreach (Type dto in _DTOs.OrderBy(t => t.Name)) {
                PropertyInfo[] dtoProperties = dto.GetProperties().Where(prop => prop.CanRead && !prop.HasAttribute<IgnoreDataMemberAttribute>()).ToArray();

                GenerateJsDoc(writer, dto, dtoProperties, true);

                bool isInheritedClass = dto.BaseType != null && dto.BaseType != typeof(object) && _DTOs.Contains(dto.BaseType);

                if (isInheritedClass) {
                    writer.WriteLine("export interface " + dto.Name + " extends " + dto.BaseType.Name + " {");
                }
                else {
                    writer.WriteLine("export interface " + dto.Name + " {");
                }
                writer.Indent++;

                foreach (PropertyInfo property in dtoProperties) {
                    try {
                        // Don't redeclare inherited properties
                        if (isInheritedClass && dto.BaseType.GetProperty(property.Name) != null) {
                            continue;
                        }

                        // Property on this class
                        Type returnType = property.GetMethod.ReturnType;
                        // Optional?
                        if (returnType.IsNullableType() || returnType.IsClass()) writer.WriteLine(property.Name + "?: " + DetermineTsType(returnType) + ";");
                        else // Required 
                            writer.WriteLine(property.Name + ": " + DetermineTsType(returnType) + ";");
                    }
                    catch (Exception e) {
                        writer.WriteLine("// ERROR - Unable to emit property " + property.Name);
                        writer.WriteLine("//     " + e.Message);
                    }
                }

                writer.Indent--;
                writer.WriteLine("}\n");
            }

            return writer.InnerWriter.ToString();
        }

        public string GenerateRoutes() {
            var writer = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 1 };

            writer.WriteLine(@"   
    // ----- Routes -----
    
    /**
    * Base class for classes implementing communication with a service stack route.
    */
    export class RouteAggregator {
        public service : " + _ServiceName + @";
        get rootUrl() : string { return this.service.rootUrl; }
        get $http() : ng.IHttpService { return this.service.$http; }

        constructor(service: " + _ServiceName + @") {
            this.service = service;            
        }
    }
");

            writer.WriteLine(_RoutesWriter.InnerWriter);

            return writer.InnerWriter.ToString();
        }

        #endregion

        #region Methods

        internal string DetermineTsType(Type type) {
            if (type == typeof(bool)) return "boolean";
            if (type == typeof(string) || type == typeof(char)) return "string";
            if (type == typeof(int) || type == typeof(byte) || type == typeof(short) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(Int64)
                || type == typeof(UInt64) || type == typeof(double) || type == typeof(decimal)) return "number";
            if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return "Date";

            string result;
            if (type.HasAttribute<RouteAttribute>()) {
                Type[] interfaces = type.GetTypeInterfaces();
                if (interfaces.Any(ti => ti == typeof(IReturnVoid))) result = "void";
                else {
                    if (type.HasInterface(typeof(IReturn))) {
                        // When a route implements IReturn<whatever> it ends up with two interfaces on it
                        // One is a straight IReturn, the other is a generic IReturn
                        Type ireturn = type.Interfaces().FirstOrDefault(ti => ti.IsGenericType && ti.Name.StartsWith("IReturn`"));

                        if (ireturn == null) result = "void";
                        else result = DetermineTsType(ireturn.GenericTypeArguments[0]);
                    }
                    else result = "void";
                }
            }
            else if (type.IsArray()) result = "Array<" + DetermineTsType(type.GetElementType()) + ">";
            else if (type.IsNullableType()) {
                // int?, bool?, etc.  Use the more underlying type
                result = DetermineTsType(type.GenericTypeArguments[0]);
            }
            else if (type.IsGenericType) {
                Type genericDefinition = type.GetGenericTypeDefinition();

                if (genericDefinition.Name.StartsWith("Dictionary") || genericDefinition.Name.StartsWith("IDictionary")) {
                    result = "{[name: " + DetermineTsType(type.GenericTypeArguments[0]) + "]: " + DetermineTsType(type.GenericTypeArguments()[1]) + "}";
                }
                else if (genericDefinition.Name.StartsWith("List`") || genericDefinition.Name.StartsWith("IList`") || genericDefinition.Name.StartsWith("ICollection`")
                         || genericDefinition.Name.StartsWith("IEnumerable`")) {
                    result = "Array<" + DetermineTsType(type.GenericTypeArguments[0]) + ">";
                }
                else throw new Exception("Error processing " + type.Name + " - Unknown generic type " + type.GetGenericTypeDefinition().Name);
            }
            else {
                if (type.Namespace != null && !_DTOs.Contains(type) && !_ExclusionNamespaces.Any(ns => type.Namespace.StartsWith(ns))) {
                    _DTOs.Add(type);

                    // Since the DTO might expose other DTOs we need to examine all of the return types of properties
                    foreach (PropertyInfo property in type.Properties().Where(p => p.CanRead && !p.HasAttribute<IgnoreDataMemberAttribute>())) {
                        DetermineTsType(property.GetMethod.ReturnType);
                    }
                }

                // We put our models in a dtos module in typescript
                result = _DTOs.Contains(type) ? type.Name : "any";
            }

            return result;
        }


        private void GenerateJsDoc(TextWriter writer, Type type, PropertyInfo[] properties, bool commentSection = true) {
            try {
                var documentation = XmlDocumentationReader.XmlDocumentationReader.XMLFromType(type);

                if (documentation != null && documentation["summary"] != null) {
                    string classSummary = documentation["summary"].InnerText.Trim();

                    if (commentSection)
                        writer.WriteLine("/**");

                    foreach (string line in classSummary.Split('\n'))
                        writer.WriteLine(" * " + line);

                    if (properties != null) {
                        foreach (PropertyInfo property in properties) {
                            try {
                                var paramDocs = XmlDocumentationReader.XmlDocumentationReader.XMLFromMember(property);
                                if (paramDocs != null && paramDocs["summmary"] != null) {
                                    writer.WriteLine(" * @param " + paramDocs["summary"].InnerText.Trim());
                                }
                            }
                            catch (XmlDocumentationReader.NoDocumentationFoundException e) {
                                writer.WriteLine(" * @param " + e.Message);
                            }
                        }
                    }

                    if (commentSection)
                        writer.WriteLine("*/");
                }
            }
            catch (XmlDocumentationReader.NoDocumentationFoundException) { }
            catch (FileNotFoundException) { }
            catch (Exception e) {

                if (commentSection)
                    writer.WriteLine("/*");
                writer.WriteLine((" * Unable to generate documentation:  ") + e.ToString());
                if (commentSection)
                    writer.WriteLine("*/");
            }
        }

        private Dictionary<string, Dictionary<Type, List<RouteAttribute>>> GetRegisteredServiceStackRoutes(IEnumerable<Type> routeTypes) {
            var routes = new Dictionary<string, Dictionary<Type, List<RouteAttribute>>>();
            foreach (Type rt in routeTypes) {
                foreach (Attribute attribute in rt.GetCustomAttributes(typeof(RouteAttribute))) {
                    var route = (RouteAttribute)attribute;
                    string[] pathHierarchy = route.Path.TrimStart('/').Split('/');
                    string root = pathHierarchy[0];

                    if (!routes.ContainsKey(root)) routes.Add(root, new Dictionary<Type, List<RouteAttribute>>());

                    if (!routes[root].ContainsKey(rt)) routes[root].Add(rt, new List<RouteAttribute>());

                    routes[root][rt].Add(route);
                }
            }
            return routes;
        }

        private void ProcessTypes(IEnumerable<Type> routeTypes) {
            _ServiceWriter = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 1 };
            _ServiceWriter.WriteLine(@"
    /**
     * Exposes access to the ServiceStack routes
    */
    export class " + _ServiceName + " {");
            _ServiceWriter.Indent++;

            _ServiceRoutesWriter = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 0 };
            var constructor = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 1 };
            constructor.WriteLine(@"
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
            constructor.Indent += 2;

            _RoutesWriter = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 2 };

            foreach (var routeRoot in GetRegisteredServiceStackRoutes(routeTypes).OrderBy(rr => rr.Key)) {
                var classWriter = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 1 };
                var routeDtosWriter = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 1 };

                _ServiceRoutesWriter.WriteLine(routeRoot.Key.ToCamelCase() + ": " + routeRoot.Key + ";");

                classWriter.WriteLine("export class " + routeRoot.Key + " extends RouteAggregator {");

                constructor.WriteLine("this." + routeRoot.Key.ToCamelCase() + " = new " + routeRoot.Key + "(this);");

                classWriter.Indent++;

                foreach (Type type in routeRoot.Value.Keys.OrderBy(t => t.Name)) {
                    try {
                        string returnTsType = DetermineTsType(type);

                        if (routeRoot.Value[type].Count > 1) {
                            _ServiceWriter.WriteLine(
                                "// " + type + "/ exports multiple routes.  Typescript does not support operator overloading and this operation is not supported.  Make seperate routes instead.");
                        }

                        var customAttribute = type.GetCustomAttribute<TypescriptCodeGeneratorAttribute>();
                        if (customAttribute != null && customAttribute.CacheResult) {
                            classWriter.WriteLine("private _" + type.Name + "Cached: ng.IHttpPromise<" + returnTsType + ">;");
                        }

                        foreach (RouteAttribute route in routeRoot.Value[type]) {
                            WriteMethodHeader(classWriter, type, route);

                            var cg = new RouteCodeGeneration(this, route, type, returnTsType);

                            // Translate the path into a coded URL
                            // We may have tokens like {ID} in the route
                            cg.ParseRoutePath();

                            // Generate code for route properties
                            cg.ProcessRouteProperties();

                            foreach (string verb in cg.Verbs) WriteTypescriptMethod(classWriter, routeDtosWriter, cg, verb, cg.Verbs.Length > 1, verb == cg.Verbs[0]);
                        }
                    }
                    catch (Exception e) {
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

            constructor.Indent--;
            constructor.WriteLine("}");
            _ServiceWriter.WriteLine(constructor.InnerWriter);

            _ServiceWriter.WriteLine(_ServiceRoutesWriter.InnerWriter);

            // End of Client Repository Singleton 
            _ServiceWriter.Indent--;
            _ServiceWriter.WriteLine("}");
        }

        private void WriteRouteMethodBody(IndentedTextWriter classWriter, RouteCodeGeneration cg, string verb) {
            classWriter.Indent++;
            var customAttribute = cg.RouteType.GetCustomAttribute<TypescriptCodeGeneratorAttribute>();
            var cacheResult = (customAttribute != null && customAttribute.CacheResult);

            if (cacheResult) {
                classWriter.WriteLine("if (this._" + cg.RouteType.Name + "Cached == null) {");
                classWriter.Indent++;
                classWriter.WriteLine("this._" + cg.RouteType.Name + "Cached = this.$http<" + cg.ReturnTsType + ">({");
            }
            else {
                classWriter.WriteLine("return this.$http<" + cg.ReturnTsType + ">({");
            }

            classWriter.Indent++;

            // Write out the url array
            classWriter.Write("url:  [");
            for (int i = 0; i < cg.UrlPath.Count; i++) {
                if (i > 0) classWriter.Write(", ");
                classWriter.Write(cg.UrlPath[i]);
            }
            classWriter.WriteLine("].join('/'),");
            classWriter.WriteLine("method: '" + verb.ToUpper() + "',");

            if (cg.RouteInputPropertyLines.Count > cg.MethodParameters.Count) {
                if (verb.ToUpper() == "GET") classWriter.WriteLine("params:  routeParams");
                else classWriter.WriteLine("data:  routeParams");
            }

            classWriter.Indent--;

            classWriter.WriteLine("});");

            if (cacheResult) {
                classWriter.Indent--;
                classWriter.WriteLine("}");
                classWriter.WriteLine("return this._" + cg.RouteType.Name + "Cached;");
            }

            // Close the method
            classWriter.Indent--;
            classWriter.WriteLine("}");
            classWriter.WriteLine();
        }

        private void WriteMethodHeader(IndentedTextWriter writer, Type type, RouteAttribute route) {
            writer.WriteLine("/**");
            writer.WriteLine(" * C# Type:  " + type.Namespace + "." + type.Name);
            if (!string.IsNullOrEmpty(route.Path)) writer.WriteLine(" * Path: " + route.Path);
            if (!string.IsNullOrEmpty(route.Verbs)) writer.WriteLine(" * Verbs: " + route.Verbs);
            if (!string.IsNullOrEmpty(route.Summary)) writer.WriteLine(" * Summary:  " + route.Summary);
            if (!string.IsNullOrEmpty(route.Notes)) writer.WriteLine(" * Notes:  " + route.Notes);
            GenerateJsDoc(writer, type, null, false);
            writer.WriteLine("*/");
        }

        private void WriteTypescriptMethod(IndentedTextWriter classWriter, IndentedTextWriter routeDtoWriter, RouteCodeGeneration cg, string verb, bool includeVerbNameInMethod, bool writeInputDto) {
            classWriter.Write(cg.RouteType.Name + (includeVerbNameInMethod ? "_" + verb : string.Empty));
            classWriter.Write(" = (");

            // Optional parameters must come last in typescript
            for (int i = 0; i < cg.MethodParameters.Count; i++) {
                if (i > 0) classWriter.Write(", ");

                classWriter.Write(cg.MethodParameters[i]);
            }

            if (cg.RouteInputPropertyLines.Count > 0) {
                if (cg.MethodParameters.Count > 0) classWriter.Write(", ");

                classWriter.Write("routeParams ");
                if (cg.RouteInputHasOnlyOptionalParams)
                    classWriter.Write("?");

                classWriter.Write(": " + cg.RouteInputDtoName);

                if (writeInputDto) {
                    //GenerateJsDoc(routeDtoWriter, cg.RouteType, );

                    routeDtoWriter.WriteLine("export interface " + cg.RouteInputDtoName + " {");
                    routeDtoWriter.Indent++;

                    foreach (string line in cg.RouteInputPropertyLines) if (!string.IsNullOrEmpty(line)) routeDtoWriter.WriteLine(line);

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

            WriteRouteMethodBody(classWriter, cg, verb);
        }

        #endregion
    }

    internal class RouteCodeGeneration {
        #region Fields

        private readonly TypescriptCodeGenerator _CodeGenerator;

        #endregion

        #region Constructors and Destructors

        public RouteCodeGeneration(TypescriptCodeGenerator codeGenerator, RouteAttribute route, Type routeType, string returnTsType) {
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
                RouteInputPropertyLines.Add(property.Name + "?: " + _CodeGenerator.DetermineTsType(returnType) + ";");
            }
            else // Required parameter
            {
                if (!IsRouteParam) {
                    if (docAttr != null) RouteInputPropertyLines.Add(EmitComment(docAttr));
                    RouteInputHasOnlyOptionalParams = false;
                    RouteInputPropertyLines.Add(property.Name + ": " + _CodeGenerator.DetermineTsType(returnType) + ";");
                }
                else MethodParameters.Add(property.Name.ToCamelCase() + ": " + _CodeGenerator.DetermineTsType(returnType));
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

    [AttributeUsage(AttributeTargets.Class)]
    public class TypescriptCodeGeneratorAttribute : Attribute {
        public TypescriptCodeGeneratorAttribute() {
            CacheResult = false;
        }

        public bool CacheResult { get; set; }
    }
}