module Controllers

open System
open System.Web.Http
open System.Net
open System.Net.Http
open System.Reactive.Subjects
open BookingApi.Http
open Renditions
open BookingApi.Http.Reservations

type HomeController () =
    inherit ApiController()

    member this.Get () = new HttpResponseMessage()


type ReservationsController () =
    inherit ApiController()

    let subject = new Subject<Envelope<MakeReservation>>()
    
    member this.post (rendition:MakeReservationRendition) =
        let cmd =
            {
                MakeReservation.Date = DateTime.Parse rendition.Date
                Name = rendition.Name
                Email = rendition.Email
                Quantity = rendition.Quantity
            }
            |> EnvelopWithDefaults

        subject.OnNext cmd

        this.Request.CreateResponse(
            HttpStatusCode.Accepted,
            {
                Links =
                    [| {
                            Rel = "http://localhost:53743/notifications"
                            Href = "notifications/" + cmd.Id.ToString("N") } |] })

    interface IObservable<Envelope<MakeReservation>> with
        member this.Subscribe observer = subject.Subscribe observer

    override this.Dispose disposing =
        if disposing then subject.Dispose()
        base.Dispose disposing


type NotificationsController (notifications:Notifications.INotifications) =
    inherit ApiController()

    member this.Get id =
        let toRendition (n:Envelope<Notification>) = {
            About = n.Item.About.ToString()
            Type = n.Item.Type
            Message = n.Item.Message
        }

        let matches =
            notifications
            |> Notifications.About id
            |> Seq.map toRendition
            |> Seq.toArray

        this.Request.CreateResponse(HttpStatusCode.OK, {Notifications = matches })

    member this.Notifications = notifications


type AvailabilityController(reservations:IReservations, seatingCapacity:int) =
    inherit ApiController()

    let getAvailableSeats map (now:DateTimeOffset) date =
        if date < now.Date then 0
        elif map |> Map.containsKey date then
            seatingCapacity - (map |> Map.find date)
        else
            seatingCapacity

    let toMapOfDatesAndQuantities (min, max) reservations =
        reservations
            |> Reservations.Between min max
            |> Seq.groupBy (fun d -> d.Item.Date)
            |> Seq.map (fun (d, rs) ->
                    (d, rs |> Seq.sumBy (fun r -> r.Item.Quantity)))
            |> Map.ofSeq

    let toOpening ((date:DateTime), seats) =
        { Date = date.ToString "yyyy.MM.dd"; Seats = seats }

    let getOpeningsIn period =
        let map = reservations |> toMapOfDatesAndQuantities (Dates.BounderiesIn period)
        let getAvailable = getAvailableSeats map (DateTimeOffset.Now)

        Dates.In period
            |> Seq.map(fun d -> d, (getAvailable d))
            |> Seq.map toOpening
            |> Seq.toArray

    member this.Get year =
        let openings = getOpeningsIn (Year year)

        this.Request.CreateResponse(
            HttpStatusCode.OK,
            { Openings = openings })

    member this.Get (year, month) =
        let openings = getOpeningsIn (Month(year, month))

        this.Request.CreateResponse(
            HttpStatusCode.OK,
            { Openings = openings })

    member this.Get (year, month, day) =
        let openings = getOpeningsIn(Day(year, month, day))

        this.Request.CreateResponse(
            HttpStatusCode.OK,
            { Openings = openings })

    member this.SeatingCapacity = seatingCapacity
