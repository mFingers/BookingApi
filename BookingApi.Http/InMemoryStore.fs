module BookingApi.Http.InMemoryStore

module Reservations =
    open Reservations

    type ReservationsInMemory(reservations) =
        interface IReservations with
            member x.Between min max =
                reservations
                |> Seq.filter (fun r -> min <= r.Item.Date && r.Item.Date <= max)

            member x.GetEnumerator () = reservations.GetEnumerator ()

            member x.GetEnumerator () =
                (x :> seq<Envelope<Reservation>>).GetEnumerator() :> System.Collections.IEnumerator

    let ToReservations reservations = ReservationsInMemory(reservations)


module Notifications =
    open Notifications

    type NotificationsInMemory(notifications:seq<Envelope<Notification>>) =
        let ToNotifications notifications = NotificationsInMemory(notifications)

        interface INotifications with
            member x.About id = 
                notifications |> Seq.filter (fun n -> n.Item.About = id)

            member x.GetEnumerator() = notifications.GetEnumerator()
            member x.GetEnumerator() =
                (x :> Envelope<Notification> seq).GetEnumerator() :> System.Collections.IEnumerator
