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
    using System.Text;

    public partial class TypescriptCodeGenerator {
            #region Constants

        private const string TAB = "    ";

        #endregion

        #region Fields

        private readonly HashSet<Type> _DTOs = new HashSet<Type>();

        private readonly string[] _ExclusionNamespaces;

        private readonly string _ServiceName;

        private readonly IEnumerable<Type> _RouteTypes;

    private readonly List<KeyValuePair<string, Dictionary<Type, List<RouteAttribute>>>> _ServiceStackRoutes;

    #endregion

        #region Constructors and Destructors

        public TypescriptCodeGenerator(IEnumerable<Type> routeTypes, string serviceName, string[] exclusionNamespaces) {
            try {
                _ServiceName = serviceName;
                _ExclusionNamespaces = exclusionNamespaces;
                _RouteTypes = routeTypes;
                _ServiceStackRoutes = FindRegisteredServiceStackRoutes(_RouteTypes).OrderBy(rr => rr.Key).ToList();
            }
            // Pretty up type exceptions
            catch (ReflectionTypeLoadException ex) {
                var sb = new StringBuilder();
                
                foreach (Exception exSub in ex.LoaderExceptions) {
                    sb.AppendLine(exSub.Message);
                    var exFileNotFound = exSub as FileNotFoundException;
                    if (exFileNotFound != null) {
                        if (!string.IsNullOrEmpty(exFileNotFound.FusionLog)) {
                            sb.AppendLine("Fusion Log:");
                            sb.AppendLine(exFileNotFound.FusionLog);
                        }
                    }
                    sb.AppendLine();
                }
                string errorMessage = sb.ToString();
                throw new Exception(errorMessage);
            }
        }

        #endregion

        #region Public Methods and Operators

    public string Generate() {
        var writer = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 1 };

        WriteServiceInterface(writer);
        WriteServiceClass(writer);
        WriteRouteClasses(writer);
        WriteDtoClasses(writer);

        return writer.InnerWriter.ToString();
    }

   
        #endregion

        #region Methods

    internal string DetermineTsType(Type type, bool isForDto = false) {
        if (type == typeof(bool)) return "boolean";
        if (type == typeof(string) || type == typeof(char)) return "string";
        if (type == typeof(int) || type == typeof(byte) || type == typeof(short) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(Int64)
            || type == typeof(UInt64) || type == typeof(double) || type == typeof(decimal)) return "number";
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return "Date";

        string result;
        if (!isForDto && type.HasAttribute<RouteAttribute>()) {
            Type[] interfaces = type.GetTypeInterfaces();
            if (interfaces.Any(ti => ti == typeof(IReturnVoid))) result = "void";
            else {
                if (type.HasInterface(typeof(IReturn))) {
                    // When a route implements IReturn<whatever> it ends up with two interfaces on it
                    // One is a straight IReturn, the other is a generic IReturn
                    Type ireturn = type.Interfaces().FirstOrDefault(ti => ti.IsGenericType && ti.Name.StartsWith("IReturn`"));

                    if (ireturn == null) result = "void";
                    else result = DetermineTsType(ireturn.GenericTypeArguments[0], isForDto);
                }
                else result = "void";
            }
        }
        else if (type.IsArray()) result = "Array<" + DetermineTsType(type.GetElementType(), isForDto) + ">";
        else if (type.IsNullableType()) {
            // int?, bool?, etc.  Use the more underlying type
            result = DetermineTsType(type.GenericTypeArguments[0], isForDto);
        }
        else if (type.IsGenericType) {
            Type genericDefinition = type.GetGenericTypeDefinition();

            if (genericDefinition.Name.StartsWith("Dictionary") || genericDefinition.Name.StartsWith("IDictionary")) {
                result = "{[name: " + DetermineTsType(type.GenericTypeArguments[0], isForDto) + "]: " 
                        + DetermineTsType(type.GenericTypeArguments()[1], isForDto) + "}";
            }
            else if (genericDefinition.Name.StartsWith("List`") || genericDefinition.Name.StartsWith("IList`") || genericDefinition.Name.StartsWith("ICollection`")
                     || genericDefinition.Name.StartsWith("IEnumerable`")) {
                result = "Array<" + DetermineTsType(type.GenericTypeArguments[0], isForDto) + ">";
            }
            else throw new Exception("Error processing " + type.Name + " - Unknown generic type " + type.GetGenericTypeDefinition().Name);
        }
        else {
            if (type.Namespace != null && !_DTOs.Contains(type) && !_ExclusionNamespaces.Any(ns => type.Namespace.StartsWith(ns))) {
                _DTOs.Add(type);

                // Since the DTO might expose other DTOs we need to examine all of the return types of properties
                foreach (PropertyInfo property in type.Properties().Where(p => p.CanRead && !p.HasAttribute<IgnoreDataMemberAttribute>())) {
                    DetermineTsType(property.GetMethod.ReturnType, isForDto);
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

        /// <summary>
        /// Iterate through the route types and pull out types tagged with the [Route] attribute
        /// </summary>
        /// <param name="routeTypes"></param>
        /// <returns></returns>
        private IEnumerable<KeyValuePair<string, Dictionary<Type, List<RouteAttribute>>>> FindRegisteredServiceStackRoutes(IEnumerable<Type> routeTypes) {
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
            return routes.OrderBy(rr => rr.Key).ToList();            
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
}