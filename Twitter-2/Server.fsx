#r ".\\bin\\Debug\\netcoreapp3.1\\Suave.dll"
#r ".\\bin\\Debug\\netcoreapp3.1\\FSharp.Json.dll"
#r ".\\bin\\Debug\\netcoreapp3.1\\Newtonsoft.Json.FSharp.dll"
// #r ".\\bin\\Debug\\netcoreapp3.1\\Twitter.dll"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote" 
#r "nuget: Akka.TestKit"
// #load "DataTables.fsx"
#load "TwitterEngine.fsx"

open Suave
// open Suave.Http
open Suave.Operators
open Suave.Filters
// open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
// open Suave.Utils

open System
// open System.Net

open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket

open Newtonsoft.Json
// open FSharp.Json
// open Akka.Actor
// open Akka.Configuration
open Akka.FSharp
// open DataTables
open DataTypes
open TwitterEngine
// open System

let settings = new JsonSerializerSettings(TypeNameHandling = TypeNameHandling.All)

let mutable id = 1

let sendws (webSocket : WebSocket) (context: HttpContext) (data: string) =
    let dosomething() = socket {
        let byteResponse =
            data
            |> System.Text.Encoding.ASCII.GetBytes
            |> ByteSegment
        do! webSocket.send Text byteResponse true
    }
    dosomething()

let task (webSocket : WebSocket) (context: HttpContext) (data: string) = 
    async {
        let! ws = sendws webSocket context data
        ws |> ignore       
    }

let webSocketActor (webSocket : WebSocket) (context: HttpContext) (mailbox: Actor<'a>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()  
        match box message with 
        | :? string as str ->
            let splits = str.Split("\n")
            let from = splits.[1].Split("RequestAPI: ").[1]
            let data = str.Substring(str.IndexOf("Body: ") + 6)
            // printfn "%A %A %A" splits from data
            // let services = ["Register"; "LogIn"; "LogOut"; "Subscribe"; "Tweet"; "Query-subscribedTo"; "Query-hashTag"; "Query-mention"; "Retweet"]
            match from with
            | "Register" ->
                registerAPI1 <! JsonConvert.DeserializeObject<Credentials>(data,settings)
            | "LogIn" ->
                logInAPI1 <! JsonConvert.DeserializeObject<Credentials>(data,settings)
            | "LogOut" ->
                logOutAPI1 <! JsonConvert.DeserializeObject<Credentials>(data,settings)
            | "Subscribe" ->
                subscribeAPI1 <! JsonConvert.DeserializeObject<Follow>(data,settings)
            | "Tweet" ->
                tweetAPI1 <! JsonConvert.DeserializeObject<TweetInfo>(data,settings)
            | "Query-subscribedTo" ->
                queryAPI1 <! JsonConvert.DeserializeObject<Query>(data,settings)
            | "Query-hashTag" ->
                queryAPI1 <! JsonConvert.DeserializeObject<Query>(data,settings)
            | "Query-mention" ->
                queryAPI1 <! JsonConvert.DeserializeObject<Query>(data,settings)
            | "Retweet" ->
                retweetAPI1 <! JsonConvert.DeserializeObject<Retweet>(data,settings)
            | _ ->
                ()
        | :? Response as response ->
            printfn "%A" response
            Async.StartImmediateAsTask((task webSocket context (JsonConvert.SerializeObject response))) |> ignore    
        | :? QueryResponse as queryResponse ->
            printfn "%A" queryResponse
            Async.StartImmediateAsTask((task webSocket context (JsonConvert.SerializeObject queryResponse))) |> ignore    
        | :? LiveTweet as liveTweet ->
            printfn "%A" liveTweet
            Async.StartImmediateAsTask((task webSocket context (JsonConvert.SerializeObject liveTweet))) |> ignore    
        | _ ->
            ()
        return! loop ()
    }
    loop ()

let ws (webSocket : WebSocket) (context: HttpContext) =
  let webSocketActor = spawn system ("webSocketActor" + (string) id) (webSocketActor webSocket context)
//   printfn "%A" id
  id <- id + 1  
  socket {
    // if `loop` is set to false, the server will stop receiving messages
    let mutable loop = true

    // Async.StartImmediateAsTask((task webSocket context "something"))
    while loop do
      // the server will wait for a message to be received without blocking the thread
      let! msg = webSocket.read()

      match msg with
      // the message has type (Opcode * byte [] * bool)
      //
      // Opcode type:
      //   type Opcode = Continuation | Text | Binary | Reserved | Close | Ping | Pong
      //
      // byte [] contains the actual message
      //
      // the last element is the FIN byte, explained later
      | (Text, data, true) ->
        // the message can be converted to a string
        let str = UTF8.toString data
        webSocketActor <! str
        printfn "\n%A" str
        // let response = sprintf "response to %s" str

        // let json = Json.fromJson data
        // let json = JsonConvert.DeserializeObject str
        // match json with
        // | :? Credentials as credentials ->
        //     printfn "hi"
        // | _ ->
        //     printfn "bad"

        // let response = Json.serialize (registerSuccess json.UserName json.TimeStamp)

        // let response =  Json.serialize (Async.RunSynchronously(registerAPI1 <? {Socket = webSocket; Data = json}))
        // Async.RunSynchronously(registerAPI1 <? {Socket = webSocket; SocketMd = socket; Data = json})
        // printfn "%A" <| webSocket.read()

        // // the response needs to be converted to a ByteSegment
        // let byteResponse =
        //   response
        //   |> System.Text.Encoding.ASCII.GetBytes
        //   |> ByteSegment

        // the `send` function sends a message back to the client
        // do! webSocket.send Text byteResponse true

      | (Close, _, _) ->
        let emptyResponse = [||] |> ByteSegment
        do! webSocket.send Close emptyResponse true

        // after sending a Close message, stop the loop
        loop <- false

      | _ -> ()
    }

/// An example of explictly fetching websocket errors and handling them in your codebase.
let wsWithErrorHandling (webSocket : WebSocket) (context: HttpContext) = 
   
   let exampleDisposableResource = { new IDisposable with member __.Dispose() = printfn "Resource needed by websocket connection disposed" }
   let websocketWorkflow = ws webSocket context
   
   async {
    let! successOrError = websocketWorkflow
    match successOrError with
    // Success case
    | Choice1Of2() -> ()
    // Error case
    | Choice2Of2(error) ->
        // Example error handling logic here
        printfn "Error: [%A]" error
        exampleDisposableResource.Dispose()
        
    return successOrError
   }

let app : WebPart = 
  choose [
    path "/websocket" >=> handShake ws
    path "/websocketWithSubprotocol" >=> handShakeWithSubprotocol (chooseSubprotocol "test") ws
    path "/websocketWithError" >=> handShake wsWithErrorHandling
    GET >=> choose [ path "/" >=> file "index.html"; browseHome ]
    NOT_FOUND "Found no handlers." ]


startWebServer { defaultConfig with logger = Targets.create Verbose [||] } app