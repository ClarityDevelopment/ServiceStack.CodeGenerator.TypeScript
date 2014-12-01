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

