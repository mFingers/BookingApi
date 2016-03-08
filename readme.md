# Booking API

From "A Functional Architecure with F#" course.



# Setting Up F# Web Project
## Package Management
[Paket](https://fsprojects.github.io/Paket/getting-started.html#Manual-setup)

## Build
- Install [FAKE](http://fsharp.github.io/FAKE/gettingstarted.html)
- Create build.cmd file
- Create build.fsx file

## Create the web project
- Create F# class library
- Add the Project Type Guids to the fsproj file:  `<ProjectTypeGuids>{E53F8FEA-EAE0-44A6-8774-FFD645390401};{349C5851-65DF-11DA-9384-00065B846F21};{F2A71F9B-5D33-465A-A702-920D77279786}</ProjectTypeGuids>`
- Install Microsoft.AspNet.WebApi.WebHost
- Add reference to System.Web
- Add web.config, with binding redirects for Json.Net
```
  <runtime>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="Newtonsoft.Json" publicKeyToken="30ad4fe6b2a6aeed"/>
        <bindingRedirect oldVersion="0.0.0.0-8.0.0.0" newVersion="8.0.0.0"/>
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
```
- Add Infrastructure file with Global type for configuring the application
- Add global.asax
- Change Target Framework to 4.6.1
- Change build output directory to just "bin"
- Clear the XML Documentation file checkbox

## Separate API concerns from hosting concerns
- Create F# class library
- Install Microsoft.AspNet.WebApi.WebHost
- Add reference to System.Web
- Move infrastructure code that should be owned by the API out of the host
- Create a controller to prove it works
- Setup JSON serialization defaults

## Setup the reservation controller as a publisher of messages
- Create the types for the messages
    - The message content
    - An envelope for the message
- Install [ReactiveX](http://reactivex.io/) `nuget Rx-Main`
- Implement IObservable in the controller
- Turn the http post into the publisher:  People from the web site will be asking for reservations.  The post message will receive those requests, then publish their messages to the application.

## Create business logic around making a reservation
- Create a Reservation module
    - operations to work on a list of reservations

## Subscribe business logic to reservations controller
1. Create a mailbox processor (agent) to handle the incoming reservations
1. Hook into WebApi where it creates controllers, and subscribe to the controller, using the agent

