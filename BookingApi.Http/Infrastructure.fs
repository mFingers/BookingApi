module BookingApi.Http.Infrastructure

open System
open System.Net.Http
open System.Web.Http
open System.Web.Http.Dispatcher
open Controllers
open Reservations

type Agent<'T> = Microsoft.FSharp.Control.MailboxProcessor<'T>

type CompositionRoot() =
    let seatingCapacity = 10
    let reservations = System.Collections.Concurrent.ConcurrentBag<Envelope<Reservation>>()

    let agent = new Agent<Envelope<MakeReservation>>(fun inbox ->
        let rec loop () =
            async {
                let! cmd = inbox.Receive()
                let rs = reservations |> ToReservations
                let handle = Handle seatingCapacity rs
                let newReservations = handle cmd
                match newReservations with
                | Some (r) -> reservations.Add r
                | None     -> ()
                return! loop()
            }
        loop())

    do agent.Start()

    interface IHttpControllerActivator with
        member this.Create(request, controllerDescriptor, controllerType) =
            if controllerType = typeof<HomeController> then
                new HomeController() :> IHttpController
            elif controllerType = typeof<ReservationsController> then
                let c = new ReservationsController()
                let sub = c.Subscribe agent.Post
                request.RegisterForDispose sub

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