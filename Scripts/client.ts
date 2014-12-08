
 
 
 
///<reference path="_references.ts"/>

///<reference path="dtos.ts"/>
///<reference path="routes.ts"/>

module cv.cef.api {
    angular
        .module('cv.cef.api', [])
        .service('$cef', ['$http', ($http : ng.IHttpService) => {
            return new Client($http);
        }]);
    
    /**
     * Exposes access to the ServiceStack routes
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

