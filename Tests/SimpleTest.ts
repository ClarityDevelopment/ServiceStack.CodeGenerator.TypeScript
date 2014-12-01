 
 module cv.cef.clientApi {
   export class Test extends cv.cef.clientApi.CefApiClient {
      // Generated from C# Type:  Clarity.TypeScript.CodeGenerator.Tests.VoidReturnRoute
      // Path: /Test/ReturnVoid
      VoidReturnRoute() : ng.IHttpPromise<void> {
         return this.$http<void>({
            url:  [this.cefApiRoot, "Test", "ReturnVoid"].join('/'),
            method: 'get',
         });
      }
      
      // Generated from C# Type:  Clarity.TypeScript.CodeGenerator.Tests.ReturnString
      // Path: /Test/ReturnString
      ReturnString() : ng.IHttpPromise<String> {
         return this.$http<String>({
            url:  [this.cefApiRoot, "Test", "ReturnString"].join('/'),
            method: 'get',
         });
      }
      
      // Generated from C# Type:  Clarity.TypeScript.CodeGenerator.Tests.ReturnStringWithParam
      // Path: /Test/ReturnStringWithParam
      ReturnStringWithParam(flag: boolean) : ng.IHttpPromise<String> {
         var jsonData = {
            Flag: flag
         };
         return this.$http<String>({
            url:  [this.cefApiRoot, "Test", "ReturnStringWithParam"].join('/'),
            method: 'get',
            data:  jsonData
         });
      }
      
      // Generated from C# Type:  Clarity.TypeScript.CodeGenerator.Tests.RouteWithParam
      // Path: /Test/{ID}/ReturnVoid
      RouteWithParam(id: number, flag: boolean) : ng.IHttpPromise<void> {
         var jsonData = {
            Flag: flag
         };
         return this.$http<void>({
            url:  [this.cefApiRoot, "Test", id, "ReturnVoid"].join('/'),
            method: 'get',
            data:  jsonData
         });
      }
      
   }
}

