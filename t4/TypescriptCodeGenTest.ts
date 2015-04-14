 
 
 
/// <reference path="../../typescript-definitions/angularjs/angular.d.ts" />

module cv.cef {

    /**
     * Exposes access to the ServiceStack routes
    */
    export interface IClarityEcomService {
        $http: ng.IHttpService;
        rootUrl:string;
        // Routes
        test: Test;
    }
    
    /**
     * Exposes access to the ServiceStack routes
    */
    export class ClarityEcomService implements IClarityEcomService{
        test : Test
        
        constructor($http: ng.IHttpService, rootUrl : string) {
            this._$http = $http;
           this.rootUrl = rootUrl;

            this.test = new Test(this);
        }

        
        private _$http: ng.IHttpService;
        get $http():ng.IHttpService {
                return this._$http;
            }        

        private _rootUrl: string;

        set rootUrl(value) { this._rootUrl = value; }
        get rootUrl():string {
                 // Remove trailing slash from URL if present
            return this._rootUrl.substr(-1) != '/'
                ? this._rootUrl
                : this._rootUrl.substr(0, this._rootUrl.length - 1);
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

    export interface GetMetadataDto {
        categories?: {[name: number]: SearchProductCatalog};
        price?: {[name: number]: SearchProductCatalog};
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
         * C# Type:  ServiceStack.CodeGenerator.TypeScript.Tests.GetMetadata
         * Path: /Test/GetMetadata
        */
        GetMetadata = (routeParams ?: GetMetadataDto) => {
            return this.$http<void>({
                url:  [this.rootUrl, "Test", "GetMetadata"].join('/'),
                method: 'GET',
                params:  routeParams
            });
        }
        
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

    // ----- DTOS -----
    export interface SearchProductCatalog {
        TestBool: boolean;
        TestString?: string;
    }

    export interface SomeDto {
        ID: number;
        Value?: string;
    }


}

