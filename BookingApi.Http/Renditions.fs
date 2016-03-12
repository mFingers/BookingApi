module Renditions

[<CLIMutable>]
type MakeReservationRendition = {
    Date:string
    Name:string
    Email:string
    Quantity:int  }

[<CLIMutable>]
type NotificationRendition = {
    About:string
    Type:string
    Message:string
}

[<CLIMutable>]
type NotificationListRendition = {
    Notifications: NotificationRendition array
}