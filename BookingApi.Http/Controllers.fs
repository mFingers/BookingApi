module Controllers

open System.Web.Http
open System.Net.Http

type HomeController () =
    inherit ApiController()

    member this.Get () = new HttpResponseMessage()