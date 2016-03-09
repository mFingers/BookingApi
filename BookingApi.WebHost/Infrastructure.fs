namespace BookingApi.WebHost

open System
open System.Web.Http
open BookingApi.Http.Infrastructure
open System.Collections.Concurrent
open BookingApi.Http
open BookingApi.Http.Reservations
open System.Reactive
open FSharp.Control.Reactive
open FSharp.Control.Reactive.Observable

type Agent<'T> = Microsoft.FSharp.Control.MailboxProcessor<'T>

type Global() =
    inherit System.Web.HttpApplication()

    member this.Application_Start (sender:obj) (e:EventArgs) =
        let seatingCapacity = 10
        let reservations = ConcurrentBag<Envelope<Reservation>>()

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

        Configure
            (reservations |> ToReservations)
            (Observer.Create agent.Post)
            GlobalConfiguration.Configuration