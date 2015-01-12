using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceStack.CodeGenerator.TypeScript {
    using System.CodeDom.Compiler;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Serialization;

    public partial class TypescriptCodeGenerator {

        private void WriteServiceInterface(IndentedTextWriter writer) {
            writer.WriteLine(@"
    /**
     * Exposes access to the ServiceStack routes
    */
    export interface I" + _ServiceName + " {");
            writer.Indent++;

            writer.WriteLine("$http: ng.IHttpService;");
            writer.WriteLine("rootUrl:string;");
        
            writer.WriteLine("// Routes");
            foreach (var routeRoot in _ServiceStackRoutes) {
                writer.WriteLine(routeRoot.Key.ToCamelCase() + ": " + routeRoot.Key + ";");
            }

            writer.Indent--;
            writer.WriteLine("}");
        }


        private void WriteServiceClass(IndentedTextWriter writer) {
            writer.WriteLine(@"
    /**
     * Exposes access to the ServiceStack routes
    */
    export class " + _ServiceName + " implements I" + _ServiceName + "{");
            writer.Indent++;

            var constructor = new IndentedTextWriter(new StringWriter(), TAB) { Indent = 1 };
            constructor.WriteLine(@"
        constructor($http: ng.IHttpService, rootUrl : string) {
            this._$http = $http;

            // Remove trailing slash from URL if present
            this._rootUrl = rootUrl.substr(-1) != '/'
                ? rootUrl
                : rootUrl.substr(0, rootUrl.length - 1);
");
            constructor.Indent += 2;
            
            foreach (var routeRoot in _ServiceStackRoutes) {
                constructor.WriteLine("this." + routeRoot.Key.ToCamelCase() + " = new " + routeRoot.Key + "(this);");
                
                // This route property on our service
                // someRoute: ISomeRoute;  
                writer.WriteLine(routeRoot.Key.ToCamelCase() + " : " + routeRoot.Key);
            }
            
            constructor.Indent--;
            constructor.WriteLine("}");            

            writer.WriteLine(constructor.InnerWriter);

            writer.WriteLine(@"
        private _$http: ng.IHttpService;
        get $http():ng.IHttpService {
                return this._$http;
            }        

        private _rootUrl: string;
        get rootUrl():string {
                return this._rootUrl;
            }              
        ");
            
            // Close the class
            writer.Indent--;
            writer.WriteLine("}\n");            
        }        
    }
}
