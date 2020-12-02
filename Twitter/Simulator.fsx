#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote" 
#r "nuget: Akka.TestKit" 
#r "C:\\Prajan\\Sem-3\\DOS\\DOS\\Twitter\\bin\\Debug\\netcoreapp3.1\\Twitter.dll"
#load "DataTables.fsx"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Collections.Generic
open DataTypes
open DataTables

let configuration = 
                    ConfigurationFactory.ParseString(
                        @"akka {
                            actor {
                                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                            }
                            remote {
                                helios.tcp {
                                    port = 8085
                                    hostname = localhost
                                }
                            }
                        }")


let system = ActorSystem.Create("System", configuration)

let registerAPI = system.ActorSelection(
                            "akka.tcp://RemoteSystem@localhost:8080/user/registerActor")

let logInAPI = system.ActorSelection(
                            "akka.tcp://RemoteSystem@localhost:8080/user/logInActor")

let tweetAPI = system.ActorSelection(
                            "akka.tcp://RemoteSystem@localhost:8080/user/tweetActor")

let subscribeAPI = system.ActorSelection(
                            "akka.tcp://RemoteSystem@localhost:8080/user/subscribeActor")

let echoAPI = system.ActorSelection(
                            "akka.tcp://RemoteSystem@localhost:8080/user/echoActor")

printfn "%A %A" registerAPI logInAPI

let clientActor id (mailbox : Actor<'a>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive()
        printfn "%A %A" message (System.DateTime.Now.ToString("HH:mm:ss.ffffff"))
        match message with
        | "start" ->
            let response = (Async.RunSynchronously(registerAPI <? getCredentials id id))
            match response with
            | 200 ->
                printfn "%A" (Async.RunSynchronously(logInAPI <? getCredentials id id))     
                printfn "%A" (Async.RunSynchronously(tweetAPI <? getTweet id id ("First tweet #" + id + " of @" + id) DateTime.Now))
                printfn "%A" (Async.RunSynchronously(subscribeAPI <? getFollow id (string((int id) - 1))))  
                printfn "%A" (Async.RunSynchronously(echoAPI <? "hi"))
            | _ -> printfn " not success"
            printfn "%A %A %A %A" registerDB logInDB tweetDB hashTagDB
        | _ ->
            ()
        mailbox.Self.GracefulStop |> ignore
        return! loop()
    }
    loop()

for i in 1 .. 20 do
    let c = spawn system ("actor" + string(i)) (clientActor (string(i)))
    c <! "start"  

Console.ReadLine() |> ignore

// dotnet fsi --langversion:preview Simulator.fsx