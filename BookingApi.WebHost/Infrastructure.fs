namespace BookingApi.WebHost

open System
open System.Web.Http
open BookingApi.Http.Infrastructure
open System.Collections.Concurrent
open BookingApi.Http
open BookingApi.Http.Reservations
open BookingApi.Http.Notifications
open System.Reactive
open FSharp.Control.Reactive
open FSharp.Control.Reactive.Observable

type Agent<'T> = Microsoft.FSharp.Control.MailboxProcessor<'T>

type Global() =
    inherit System.Web.HttpApplication()

    member this.Application_Start (sender:obj) (e:EventArgs) =
        let seatingCapacity = 10
        let reservations = ConcurrentBag<Envelope<Reservation>>()
        let notifications = ConcurrentBag<Envelope<BookingApi.Http.Notification>>()

        let reservationSubject = new Subjects.Subject<Envelope<Reservation>>()
        reservationSubject.Subscribe reservations.Add |> ignore

        let notificationSubject = new Subjects.Subject<BookingApi.Http.Notification>()
        notificationSubject
        |> Observable.map EnvelopWithDefaults
        |> Observable.subscribe notifications.Add
        |> ignore

        let agent = new Agent<Envelope<MakeReservation>>(fun inbox ->
            let rec loop () =
                async { 
                    let! cmd = inbox.Receive()
                    let rs = reservations |> ToReservations
                    let handle = Handle seatingCapacity rs
                    let newReservations = handle cmd
                    match newReservations with
                    | Some (r) ->
                        reservationSubject.OnNext r
                        notificationSubject.OnNext
                            {
                                About = cmd.Id
                                Type = "Success"
                                Message = sprintf "Your reservation for %s was completed.  We look forward to seeing you."
                                            (cmd.Item.Date.ToString "yyyy.MM.dd")
                            }
                    | None     ->
                        notificationSubject.OnNext
                            {
                                About = cmd.Id
                                Type = "Failure"
                                Message = sprintf "We regret to inform you that your reservation for %s could not be completed, because we are already fully booked."
                                            (cmd.Item.Date.ToString "yyyy.MM.dd")
                            }
                    return! loop()
                }
            loop())
    
        do agent.Start()

        Configure
            (reservations |> ToReservations)
            (notifications |> ToNotifications)
            (Observer.Create agent.Post)
            GlobalConfiguration.Configuration