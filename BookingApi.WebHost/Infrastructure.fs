namespace BookingApi.WebHost

open System
open System.Web.Http
open BookingApi.Http.Infrastructure

type Global() =
    inherit System.Web.HttpApplication()

    member this.Application_Start (sender:obj) (e:EventArgs) =
        Configure GlobalConfiguration.Configuration