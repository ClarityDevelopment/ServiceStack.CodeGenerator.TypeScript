 
 
 
/// <script path="../scripts/typings/angularjs/angular.d.ts" />

module cv.cef {

    /**
     * Exposes access to the ServiceStack routes
    */
    export interface IClarityEcomService {
        test: Test;
    }
    
    /**
     * Exposes access to the ServiceStack routes
    */
    class ClarityEcomService implements IClarityEcomService{
        test : Test
        
        constructor($http: ng.IHttpService, rootUrl : string) {
            this._$http = $http;

            // Remove trailing slash from URL if present
            this._rootUrl = rootUrl.substr(-1) != '/'
                ? rootUrl
                : rootUrl.substr(0, rootUrl.length - 1);

            this.test = new Test(this);
        }
        
        private _$http: ng.IHttpService;
        get $http():ng.IHttpService {
                return this._$http;
            }
        

        private _rootUrl: string;
        get rootUrl():string {
                return this._rootUrl;
            }            
  
        

    }

    export interface ReturnStringWithParamDto {
        Flag: boolean;
    }
    export interface RouteWithDTODto {
        // API Member Description about Flag
        Flag: boolean;
    }
    export interface RouteWithParamDto {
        // API Member Description about Flag
        Flag: boolean;
    }

    export class Test extends ServiceStackRoute {
        /**
         * C# Type:  ServiceStack.CodeGenerator.TypeScript.Tests.ReturnString
         * Path: /Test/ReturnString
        */
        ReturnString = () => {
            return this.$http<string>({
                url:  [this.rootUrl, "Test", "ReturnString"].join('/'),
                method: 'GET',
            });
        }
        
        /**
         * C# Type:  ServiceStack.CodeGenerator.TypeScript.Tests.ReturnStringWithParam
         * Path: /Test/ReturnStringWithParam
        */
        ReturnStringWithParam = (routeParams : ReturnStringWithParamDto) => {
            return this.$http<string>({
                url:  [this.rootUrl, "Test", "ReturnStringWithParam"].join('/'),
                method: 'GET',
                params:  routeParams
            });
        }
        
        /**
         * C# Type:  ServiceStack.CodeGenerator.TypeScript.Tests.RouteWithDTO
         * Path: /Test/{ID}/ReturnDTO
        */
        RouteWithDTO = (id: number, routeParams : RouteWithDTODto) => {
            return this.$http<SomeDto>({
                url:  [this.rootUrl, "Test", id, "ReturnDTO"].join('/'),
                method: 'GET',
                params:  routeParams
            });
        }
        
        /**
         * C# Type:  ServiceStack.CodeGenerator.TypeScript.Tests.RouteWithParam
         * Path: /Test/{ID}/ReturnVoid
        */
        RouteWithParam = (id: number, routeParams : RouteWithParamDto) => {
            return this.$http<void>({
                url:  [this.rootUrl, "Test", id, "ReturnVoid"].join('/'),
                method: 'GET',
                params:  routeParams
            });
        }
        
        /**
         * C# Type:  ServiceStack.CodeGenerator.TypeScript.Tests.VoidReturnRoute
         * Path: /Test/ReturnVoid
        */
        VoidReturnRoute = () => {
            return this.$http<void>({
                url:  [this.rootUrl, "Test", "ReturnVoid"].join('/'),
                method: 'GET',
            });
        }
        
    }

       
    // ----- Routes -----
    
    /**
    * Base class for service stack routes
    */
    export class ServiceStackRoute {
        public service : ClarityEcomService;
        
        // The root URL for making RESTful calls
        get rootUrl() : string { return this.service.rootUrl; }
        get $http() : ng.IHttpService { return this.service.$http; }

        constructor(service: ClarityEcomService) {
            this.service = service;                
        }
    }

    // ----- DTOS -----
    export interface SomeDto {
        ID: number;
        Value?: string;
    }


}

