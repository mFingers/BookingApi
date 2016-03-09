module BookingApi.Http.Infrastructure

open FSharp.Control.Reactive
open FSharp.Control.Reactive.Observable
open System
open System.Net.Http
open System.Reactive
open System.Web.Http
open System.Web.Http.Dispatcher
open Controllers
open Reservations

type CompositionRoot (reservations:IReservations, reservationRequestObserver) =
    interface IHttpControllerActivator with
        member this.Create(request, controllerDescriptor, controllerType) =
            if controllerType = typeof<HomeController> then
                new HomeController() :> IHttpController
            elif controllerType = typeof<ReservationsController> then
                let c = new ReservationsController()
                c
                |> Observable.subscribeObserver reservationRequestObserver
                |> request.RegisterForDispose

                c :> IHttpController
            else
                raise
                <| ArgumentException(
                    sprintf "Unknown controller type requested: %O" controllerType, "controllerType")

type HttpRouteDefaults = { Controller:string; Id:obj }

let ConfigureServices reservations reservationRequestObserver (config:HttpConfiguration) =
    config.Services.Replace(
        typeof<IHttpControllerActivator>,
        CompositionRoot (reservations, reservationRequestObserver))

let ConfigureRoutes (config:HttpConfiguration) =
    config.Routes.MapHttpRoute(
        "DefaultAPI",
        "{controller}/{id}",
        { Controller = "Home"; Id = RouteParameter.Optional }) |> ignore

let ConfigureFormatting (config:HttpConfiguration) =
    config.Formatters.JsonFormatter.SerializerSettings.ContractResolver <-
        Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()

let Configure reservations reservationRequestObserver config =
    ConfigureRoutes config
    ConfigureServices reservations reservationRequestObserver config
    ConfigureFormatting config