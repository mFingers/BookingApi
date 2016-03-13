namespace BookingApi.WebHost

open System
open System.IO
open System.Web.Http
open BookingApi.Http.Infrastructure
open System.Collections.Concurrent
open BookingApi.Http
open BookingApi.Http.Reservations
open BookingApi.Http.Notifications
open System.Reactive
open FSharp.Control.Reactive
open FSharp.Control.Reactive.Observable
open Newtonsoft.Json

type ReservationsInFiles(directory:DirectoryInfo) =
    let toReservation (f:FileInfo) =
        let json = File.ReadAllText(f.FullName)
        JsonConvert.DeserializeObject<Envelope<Reservation>>(json)

    let toEnumerator(s:seq<'T>) = s.GetEnumerator()

    let getContainingDirectory (d:DateTime) =
        Path.Combine(
            directory.FullName,
            d.Year.ToString(),
            d.Month.ToString(),
            d.Day.ToString()
        )

    let appendPath p2 p1 = Path.Combine(p1, p2)

    let getJsonFiles (dir:DirectoryInfo) =
        if Directory.Exists(dir.FullName)
            then dir.EnumerateFiles("*.json", SearchOption.AllDirectories)
            else Seq.empty<FileInfo>

    member this.Write (reservation:Envelope<Reservation>) =
        let withExtension extension path = Path.ChangeExtension(path, extension)
        let directoryName = reservation.Item.Date |> getContainingDirectory
        let filename =
            directoryName
            |> appendPath (reservation.Id.ToString())
            |> withExtension "json"

        let json = JsonConvert.SerializeObject(reservation)
        Directory.CreateDirectory(directoryName) |> ignore
        File.WriteAllText(filename, json)

    interface IReservations with
        member x.Between min max =
            Dates.InitInfinite min
            |> Seq.takeWhile (fun d -> d <= max)
            |> Seq.map getContainingDirectory
            |> Seq.collect (fun dir -> DirectoryInfo(dir) |> getJsonFiles)
            |> Seq.map toReservation

        member x.GetEnumerator() =
            directory
            |> getJsonFiles
            |> Seq.map toReservation
            |> toEnumerator

        member x.GetEnumerator() =
            (x :> seq<Envelope<Reservation>>).GetEnumerator() :> System.Collections.IEnumerator

type NotificationsInFiles(directory:DirectoryInfo) =
    let toNotification (f:FileInfo) =
        let json = File.ReadAllText(f.FullName)
        JsonConvert.DeserializeObject<Envelope<BookingApi.Http.Notification>>(json)

    let toEnumerator(s:seq<'T>) = s.GetEnumerator()

    let getContainingDirectory id =
        Path.Combine(directory.FullName, id.ToString())

    let appendPath p2 p1 = Path.Combine(p1, p2)

    let getJsonFiles (dir:DirectoryInfo) =
        if Directory.Exists(dir.FullName)
            then dir.EnumerateFiles("*.json", SearchOption.AllDirectories)
            else Seq.empty<FileInfo>

    member this.Write (notification:Envelope<BookingApi.Http.Notification>) =
        let withExtension extension path = Path.ChangeExtension(path, extension)
        let directoryName = notification.Item.About |> getContainingDirectory
        let filename =
            directoryName
            |> appendPath (notification.Id.ToString())
            |> withExtension "json"

        let json = JsonConvert.SerializeObject(notification)
        Directory.CreateDirectory(directoryName) |> ignore
        File.WriteAllText(filename, json)

    interface INotifications with
        member x.About id =
            id
            |> getContainingDirectory
            |> DirectoryInfo
            |> getJsonFiles
            |> Seq.map toNotification

        member x.GetEnumerator() =
            directory
            |> getJsonFiles
            |> Seq.map toNotification
            |> toEnumerator

        member x.GetEnumerator() =
            (x :> seq<Envelope<BookingApi.Http.Notification>>).GetEnumerator() :> System.Collections.IEnumerator


type Agent<'T> = Microsoft.FSharp.Control.MailboxProcessor<'T>

type Global() =
    inherit System.Web.HttpApplication()

    member this.Application_Start (sender:obj) (e:EventArgs) =
        let seatingCapacity = 10
        
        let reservations =
            ReservationsInFiles(
                DirectoryInfo(System.Web.HttpContext.Current.Server.MapPath("~/ReservationStore")))

        let notifications =
            NotificationsInFiles(
                DirectoryInfo(System.Web.HttpContext.Current.Server.MapPath("~/NotificationStore")))

        let reservationSubject = new Subjects.Subject<Envelope<Reservation>>()
        reservationSubject.Subscribe reservations.Write |> ignore

        let notificationSubject = new Subjects.Subject<BookingApi.Http.Notification>()
        notificationSubject
        |> Observable.map EnvelopWithDefaults
        |> Observable.subscribe notifications.Write |> ignore

        let agent = new Agent<Envelope<MakeReservation>>(fun inbox ->
            let rec loop () =
                async { 
                    let! cmd = inbox.Receive()
                    let handle = Handle seatingCapacity reservations
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
            reservations
            notifications
            (Observer.Create agent.Post)
            seatingCapacity
            GlobalConfiguration.Configuration