namespace BookingApi.Http

open System

type Period =
    | Year of int
    | Month of int * int
    | Day of int * int * int

module Dates =
    let InitInfinite (date:DateTime) =
        date |> Seq.unfold (fun d -> Some(d, d.AddDays 1.))

    let In period =
        let generate dt predicate =
            dt |> InitInfinite |> Seq.takeWhile predicate

        match period with
        | Year(y) -> generate (DateTime(y, 1, 1)) (fun d -> d.Year = y)
        | Month(y, m) -> generate (DateTime(y, m, 1)) (fun d-> d.Month = m)
        | Day(y, m, d) -> DateTime(y, m, d) |> Seq.singleton

    let BounderiesIn period =
        let getBoundaries firstTick (forward : DateTime -> DateTime) =
            let lastTick = forward(firstTick).AddTicks -1L
            (firstTick, lastTick)

        match period with
        | Year(y) -> getBoundaries (DateTime(y, 1, 1)) (fun d -> d.AddYears 1)
        | Month(y, m) -> getBoundaries (DateTime(y, m, 1)) (fun d -> d.AddMonths 1)
        | Day(y, m, d) -> getBoundaries (DateTime(y, m, d)) (fun d -> d.AddDays 1.)
        
module Reservations =
    type IReservations =
        inherit seq<Envelope<Reservation>>        
        abstract Between : DateTime -> DateTime -> seq<Envelope<Reservation>>

    let Between min max (reservations : IReservations) = 
        reservations.Between min max

    let On (date:DateTime) reservations =
        let min = date.Date
        let max = (min.AddDays 1.) - TimeSpan.FromTicks 1L
        reservations |> Between min max

    let Handle capacity reservations (request : Envelope<MakeReservation>) =
        let reservedSeatsOnDate =
            reservations
            |> On request.Item.Date
            |> Seq.sumBy (fun r -> r.Item.Quantity)

        if capacity - reservedSeatsOnDate < request.Item.Quantity then
            None
        else
            {
                Date = request.Item.Date
                Name = request.Item.Name
                Email = request.Item.Email
                Quantity = request.Item.Quantity
            }
            |> EnvelopWithDefaults
            |> Some

module Notifications =
    type INotifications =
        inherit seq<Envelope<Notification>>
        abstract About : Guid -> seq<Envelope<Notification>>

    let About id (notifications: INotifications) = notifications.About id