
 
 
 
// ----- DTOS -----
    export interface SomeDto {
        ID: number;
        Value?: string;
    }


   
    // ----- Routes -----
    
    /**
    * Base class for classes implementing communication with a service stack route.
    */
    export class RouteAggregator {
        public service : cef;
        get rootUrl() : string { return this.service.rootUrl; }
        get $http() : ng.IHttpService { return this.service.$http; }

        constructor(service: cef) {
            this.service = service;            
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

        export class Test extends RouteAggregator {
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




///<reference path="_references.ts"/>


    /**
     * Exposes access to the ServiceStack routes
    */
    export class cef {
        
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

            this.test = new Test(this);
        }

        test: Test;

    }


