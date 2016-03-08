module BookingApi.Http.Infrastructure

open System
open System.Web.Http
open System.Web.Http.Dispatcher
open Controllers

type CompositionRoot() =
    interface IHttpControllerActivator with
        member this.Create(request, controllerDescriptor, controllerType) =
            if controllerType = typeof<HomeController> then
                new HomeController() :> IHttpController
            elif controllerType = typeof<ReservationsController> then
                let c = new ReservationsController()
                c :> IHttpController
            else
                raise
                <| ArgumentException(
                    sprintf "Unknown controller type requested: %O" controllerType, "controllerType")

type HttpRouteDefaults = { Controller:string; Id:obj }

let ConfigureServices (config:HttpConfiguration) =
    config.Services.Replace(
        typeof<IHttpControllerActivator>,
        CompositionRoot())

let ConfigureRoutes (config:HttpConfiguration) =
    config.Routes.MapHttpRoute(
        "DefaultAPI",
        "{controller}/{id}",
        { Controller = "Home"; Id = RouteParameter.Optional }) |> ignore

let ConfigureFormatting (config:HttpConfiguration) =
    config.Formatters.JsonFormatter.SerializerSettings.ContractResolver <-
        Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()

let Configure config =
    ConfigureRoutes config
    ConfigureServices config
    ConfigureFormatting config