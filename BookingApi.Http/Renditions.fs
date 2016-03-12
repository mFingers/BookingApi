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

//Atom syndication format
//XML requires more, but since we are targetting JSON, we'll just leave it at this
[<CLIMutable>]
type AtomLinkRendition = {
    Rel:string
    Href:string
}

[<CLIMutable>]
type LinkListRendition = {
    Links:AtomLinkRendition array
}