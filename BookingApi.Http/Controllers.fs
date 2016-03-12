module Controllers

open System
open System.Web.Http
open System.Net
open System.Net.Http
open System.Reactive.Subjects
open BookingApi.Http
open Renditions

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


type AvailabilityController(seatingCapacity:int) =
    inherit ApiController()

    member this.Get year =
        let openings =
            Dates.In (Year year)
            |> Seq.map (fun d -> 
                {
                    Date = d.ToString "yyyy.MM.dd"
                    Seats = seatingCapacity })
            |> Seq.toArray

        this.Request.CreateResponse(
            HttpStatusCode.OK,
            { Openings = openings })

    member this.Get (year, month) =
        let openings =
            Dates.In (Month(year, month))
            |> Seq.map (fun d -> 
                {
                    Date = d.ToString "yyyy.MM.dd"
                    Seats = seatingCapacity })
            |> Seq.toArray

        this.Request.CreateResponse(
            HttpStatusCode.OK,
            { Openings = openings })

    member this.Get (year, month, day) =
        let openings =
            Dates.In (Day(year, month, day))
            |> Seq.map (fun d -> 
                {
                    Date = d.ToString "yyyy.MM.dd"
                    Seats = seatingCapacity })
            |> Seq.toArray

        this.Request.CreateResponse(
            HttpStatusCode.OK,
            { Openings = openings })