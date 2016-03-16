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
open Microsoft.WindowsAzure.Storage.Blob
open Microsoft.WindowsAzure.Storage
open Microsoft.Azure

type ErrorsInAzureBlobs(blobContainer:CloudBlobContainer) =
    let getId (d: DateTime) =
        String.Join(
            "/",
            [
                d.Year.ToString()
                d.Month.ToString()
                d.Day.ToString()
                Guid.NewGuid().ToString()
            ])
            |> sprintf "%s.txt"

    member this.Write e =
        let id = getId DateTimeOffset.UtcNow.Date
        let b = blobContainer.GetBlockBlobReference id
        b.Properties.ContentType <- "text/plain; charset=uft-8"
        b.UploadText(e.ToString())

    interface System.Web.Http.Filters.IExceptionFilter with
        member this.AllowMultiple = true
        member this.ExecuteExceptionFilterAsync(actionExecutedContext, cancellationtoken) =
            System.Threading.Tasks.Task.Factory.StartNew(
                fun () -> this.Write actionExecutedContext.Exception )

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

[<CLIMutable>]
type StoredReservations = {
    Reservations: Envelope<Reservation> array
    AcceptedCommandIds: Guid array
}

type ReservationsInAzureBlobs(blobContainer: CloudBlobContainer) =
    let toReservation (b: CloudBlockBlob) =
        let json = b.DownloadText()
        let sr = JsonConvert.DeserializeObject<StoredReservations> json
        sr.Reservations

    let toEnumerator (s: seq<'T>) = s.GetEnumerator()

    let getId (d:DateTime) =
        String.Join(
            "/",
            [
                d.Year.ToString(),
                d.Month.ToString(),
                d.Day.ToString()
            ])
            |> sprintf "%s.json"

    member this.GetAccessCondition date =
        let id = date |> getId
        let b = blobContainer.GetBlockBlobReference id
        try
            b.FetchAttributes()
            b.Properties.ETag |> AccessCondition.GenerateIfMatchCondition
        with
        | :? StorageException as e when e.RequestInformation.HttpStatusCode = 404 ->
                AccessCondition.GenerateIfNoneMatchCondition "*"

    member this.Write (reservation: Envelope<Reservation>, commandId, condition) =
        let id = reservation.Item.Date |> getId
        let b = blobContainer.GetBlockBlobReference id
        let inStore = 
            try
                let jsonInStore = b.DownloadText(accessCondition = condition)
                JsonConvert.DeserializeObject<StoredReservations> jsonInStore
            with
            | :? StorageException as e when e.RequestInformation.HttpStatusCode = 404 ->
                { Reservations = [||]; AcceptedCommandIds = [||] }

        let isReplay =
            inStore.AcceptedCommandIds
            |> Array.exists (fun id -> commandId = id)

        if not isReplay then
            let updated =
                {
                    Reservations =
                        Array.append [| reservation |] inStore.Reservations
                    AcceptedCommandIds =
                        Array.append [| commandId |] inStore.AcceptedCommandIds
                }

            let json = JsonConvert.SerializeObject updated
            b.Properties.ContentType <- "application/json"
            b.UploadText(json, accessCondition = condition)

    interface IReservations with
        member this.Between min max =
            Dates.InitInfinite min
            |> Seq.takeWhile (fun d -> d <= max)
            |> Seq.map getId
            |> Seq.map blobContainer.GetBlockBlobReference
            |> Seq.filter (fun b -> b.Exists())
            |> Seq.collect toReservation

        member this.GetEnumerator() =
            blobContainer.ListBlobs()
            |> Seq.cast<CloudBlockBlob>
            |> Seq.collect toReservation
            |> toEnumerator

        member this.GetEnumerator() =
            (this :> seq<Envelope<Reservation>>).GetEnumerator() :> System.Collections.IEnumerator

type NotificationsInAzureBlobs(blobContainer: CloudBlobContainer) =
    let toNotification (b: CloudBlockBlob) =
        let json = b.DownloadText()
        JsonConvert.DeserializeObject<Envelope<BookingApi.Http.Notification>> json

    let toEnumerator (s:seq<'T>) = s.GetEnumerator()

    member this.Write notification =
        let id = sprintf "%O/%O.json" notification.Item.About notification.Id
        let b = blobContainer.GetBlockBlobReference id
        
        let json = JsonConvert.SerializeObject notification
        b.Properties.ContentType <- "application/json"
        b.UploadText json

    interface INotifications with
        member this.About id = 
            blobContainer.ListBlobs(id.ToString(), true)
            |> Seq.cast<CloudBlockBlob>
            |> Seq.map toNotification

        member this.GetEnumerator() =
            blobContainer.ListBlobs(useFlatBlobListing = true)
            |> Seq.cast<CloudBlockBlob>
            |> Seq.map toNotification
            |> toEnumerator

        member this.GetEnumerator() =
            (this :> seq<Envelope<BookingApi.Http.Notification>>).GetEnumerator() :> System.Collections.IEnumerator
            

module AzureQ =
    let enqueue (q:Queue.CloudQueue) msg =
        let json = JsonConvert.SerializeObject msg
        Queue.CloudQueueMessage(json) |> q.AddMessage

    let dequeue (q:Queue.CloudQueue) =
        match q.GetMessage() with
        | null -> None
        | msg -> Some(msg)

type Agent<'T> = Microsoft.FSharp.Control.MailboxProcessor<'T>

type Global() =
    inherit System.Web.HttpApplication()

    member this.Application_Start (sender:obj) (e:EventArgs) =
        let seatingCapacity = 10

        let storageAccount = 
            CloudConfigurationManager.GetSetting "storageConnectionString"
            |> CloudStorageAccount.Parse

        let errorContainer =
            storageAccount
                .CreateCloudBlobClient()
                .GetContainerReference("errors")
        errorContainer.CreateIfNotExists() |> ignore
        let errorHandler = ErrorsInAzureBlobs(errorContainer)
        GlobalConfiguration.Configuration.Filters.Add errorHandler

        let rq =
            storageAccount
                .CreateCloudQueueClient()
                .GetQueueReference("reservations")
        rq.CreateIfNotExists() |> ignore

        let reservationsContainer =
            storageAccount
                .CreateCloudBlobClient()
                .GetContainerReference("reservations")
        reservationsContainer.CreateIfNotExists() |> ignore

        let reservations = ReservationsInAzureBlobs(reservationsContainer)

        let nq =
            storageAccount
                .CreateCloudQueueClient()
                .GetQueueReference("notifications")
        nq.CreateIfNotExists() |> ignore

        let notificationsContainer =
            storageAccount
                .CreateCloudBlobClient()
                .GetContainerReference("notifications")
        notificationsContainer.CreateIfNotExists() |> ignore

        let notifications = NotificationsInAzureBlobs(notificationsContainer)

        let reservationSubject = new Subjects.Subject<Envelope<Reservation> * Guid * AccessCondition>()
        reservationSubject.Subscribe reservations.Write |> ignore

        let notificationSubject = new Subjects.Subject<BookingApi.Http.Notification>()
        notificationSubject
        |> Observable.map EnvelopWithDefaults
        |> Observable.subscribe (AzureQ.enqueue nq) |> ignore 

        let handleR (msg: Queue.CloudQueueMessage) =
            let json = msg.AsString
            let cmd = JsonConvert.DeserializeObject<Envelope<MakeReservation>> json
            let condition = reservations.GetAccessCondition cmd.Item.Date
            let newReservations = Handle seatingCapacity reservations cmd
            match newReservations with
                | Some (r) ->
                    reservationSubject.OnNext (r, cmd.Id, condition)
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
            rq.DeleteMessage msg

        System.Reactive.Linq.Observable.Interval(TimeSpan.FromSeconds 10.)
        |> Observable.map (fun _ -> AzureQ.dequeue rq)
        |> Observable.choose id
        |> Observable.subscribeObserver (Observer.Create handleR)
        |> ignore

        let handleN (msg: Queue.CloudQueueMessage) =
            let json = msg.AsString
            let notification = JsonConvert.DeserializeObject<Envelope<BookingApi.Http.Notification>> json
            notifications.Write notification

            nq.DeleteMessage msg

        System.Reactive.Linq.Observable.Interval(TimeSpan.FromSeconds 10.)
        |> Observable.map (fun _ -> AzureQ.dequeue nq)
        |> Observable.choose id
        |> Observable.subscribeObserver (Observer.Create handleN)
        |> ignore

        Configure
            reservations
            notifications
            (Observer.Create (AzureQ.enqueue rq))
            seatingCapacity
            GlobalConfiguration.Configuration