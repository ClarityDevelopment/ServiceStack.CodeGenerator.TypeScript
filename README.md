Clarity.ServiceStack.CodeGenerator.TypeScript
==============================

A library and Text Transform file that generate a strongly typed TypeScript client from 
your service's ServiceStack routes.

The files created by the .tt are:
* client.ts     
* routes.ts
* dtos.ts       

A Simple Example
==============

```csharp
[Route("/Test/{ID}/ReturnVoid")]
public class RouteWithParam : IReturnVoid
{
    [ApiMember]
    public int ID { get; set; }
    [ApiMember]
    public bool Flag { get; set; }
}
```

```typescript
///<reference path="dtos.ts"/>
///<reference path="routes.ts"/>

module cv.cef.api {
    angular
        .module('cv.cef.api', [])
        .service('$cef', ($http : ng.IHttpService) => {
            return new Client($http);
        });
    
    /**
    Exposes access to the ServiceStack routes
    */
    export class Client {
        
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

            this.test = new routes.Test(this);
        }

        test: routes.Test

    }

}

///<reference path="client.ts"/>
///<reference path="dtos.ts"/>

module cv.cef.api.routes {
    
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

    export interface ReturnStringWithParamDto {
        Flag: boolean;
    }
    export interface RouteWithParamDto {
        Flag: boolean;
    }

    export class Test extends RouteAggregator {
        /**
        C# Type:  Clarity.ServiceStack.CodeGenerator.TypeScript.Tests.ReturnString
        Path: /Test/ReturnString
        */
        ReturnString = () => {
            return this.$http<string>({
                url:  [this.rootUrl, "Test", "ReturnString"].join('/'),
                method: 'GET',
            });
        }
        
        /**
        C# Type:  Clarity.ServiceStack.CodeGenerator.TypeScript.Tests.ReturnStringWithParam
        Path: /Test/ReturnStringWithParam
        */
        ReturnStringWithParam = (routeParams : ReturnStringWithParamDto) => {
            return this.$http<string>({
                url:  [this.rootUrl, "Test", "ReturnStringWithParam"].join('/'),
                method: 'GET',
                params:  routeParams
            });
        }
        
        /**
        C# Type:  Clarity.ServiceStack.CodeGenerator.TypeScript.Tests.RouteWithParam
        Path: /Test/{ID}/ReturnVoid
        */
        RouteWithParam = (id: number, routeParams : RouteWithParamDto) => {
            return this.$http<void>({
                url:  [this.rootUrl, "Test", id, "ReturnVoid"].join('/'),
                method: 'GET',
                params:  routeParams
            });
        }
        
        /**
        C# Type:  Clarity.ServiceStack.CodeGenerator.TypeScript.Tests.VoidReturnRoute
        Path: /Test/ReturnVoid
        */
        VoidReturnRoute = () => {
            return this.$http<void>({
                url:  [this.rootUrl, "Test", "ReturnVoid"].join('/'),
                method: 'GET',
            });
        }        
    }
}
```