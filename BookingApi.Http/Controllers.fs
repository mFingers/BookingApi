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

        new HttpResponseMessage(HttpStatusCode.Accepted)

    interface IObservable<Envelope<MakeReservation>> with
        member this.Subscribe observer = subject.Subscribe observer

    override this.Dispose disposing =
        if disposing then subject.Dispose()
        base.Dispose disposing