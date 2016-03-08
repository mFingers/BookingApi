namespace BookingApi.WebHost

open System
open System.Web.Http

type HttpRouteDefaults = { Controller:string; Id:obj }

type Global() =
    inherit System.Web.HttpApplication()

    member this.Application_Start (sender:obj) (e:EventArgs) =
        GlobalConfiguration.Configuration.Routes.MapHttpRoute(
            "DefaultAPI",
            "{controller}/{id}",
            { Controller = "Home"; Id = RouteParameter.Optional }) |> ignore