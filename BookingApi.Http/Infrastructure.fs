module BookingApi.Http.Infrastructure

open System.Web.Http

type HttpRouteDefaults = { Controller:string; Id:obj }

let ConfigureRoutes (config:HttpConfiguration) =
    config.Routes.MapHttpRoute(
        "DefaultAPI",
        "{controller}/{id}",
        { Controller = "Home"; Id = RouteParameter.Optional }) |> ignore

let Configure = ConfigureRoutes