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
open System.Text.RegularExpressions
open DataTables
open DataTypes

let db = HASHMAP

let configuration = 
                    ConfigurationFactory.ParseString(
                        @"akka {
                            actor {
                                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                            }
                            remote {
                                helios.tcp {
                                    port = 8080
                                    hostname = localhost
                                }
                            }
                        }")


let system = ActorSystem.Create("RemoteSystem", configuration)


let registerActor (mailbox : Actor<'a>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive()
        printfn "register actor: %A" message
        match box message with
        | :? Credentials as credentials ->
            match registeredUser credentials.UserName credentials.Password db with
            | true ->
                mailbox.Sender() <! NOTACCEPTABLE
            | false ->
                match addUser credentials.UserName credentials.Password db with
                | true ->
                    mailbox.Sender() <! SUCCESS
                | false ->
                    mailbox.Sender() <! FAILED                
        | _ ->
            mailbox.Sender() <! BADREQUEST
        mailbox.Self.GracefulStop |> ignore
        return! loop()
    }
    loop ()

let registerParentActor (mailbox: Actor<'a>) = 
    let mutable id = 0         
    let rec loop () = actor {
        let! message = mailbox.Receive ()  
        let childActor = spawn system ("registerActor" + string(id)) registerActor                     
        childActor.Forward message
        id <- id + 1
        return! loop ()
    }
    loop ()

let logInActor (mailbox: Actor<'a>) = 
        let rec loop () = actor {
            let! message = mailbox.Receive ()
            printfn "logIn actor: %A" message
            let rows = registerTable.Select()            
            for row in rows do 
                printfn "%A" row.ItemArray
            match box message with 
            | :? Credentials as c ->
                match registeredUser c.UserName c.Password db with
                | true ->
                        match loggedInUser c.UserName db with
                        | true -> 
                            mailbox.Sender() <! NOTACCEPTABLE//406
                        | false ->
                            match logInUser c.UserName c.Password db with        
                            | true -> 
                                mailbox.Sender() <! SUCCESS//200
                            | false ->
                                mailbox.Sender() <! FAILED//500
                | false ->
                        mailbox.Sender() <! UNAUTHORIZED//401
            | _ ->
                mailbox.Sender() <! BADREQUEST//400
            mailbox.Self.GracefulStop |> ignore
            return! loop ()
        }
        loop ()

let logInParentActor (mailbox: Actor<'a>) = 
    let mutable id = 0         
    let rec loop () = actor {
        let! message = mailbox.Receive ()  
        let actor1 = spawn system ("logInActor" + string(id)) logInActor                     
        actor1.Forward message
        id <- id + 1
        return! loop ()
    }
    loop ()

let processTweetActor (mailbox: Actor<'a>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "process tweet actor: %A" message
        match box message with
        | :? Tweet as tweet ->
            let hashTags = Regex(@"#\w+").Matches tweet.Content |> Seq.distinctBy (id)
            let mentions = Regex(@"@\w+").Matches tweet.Content |> Seq.distinctBy (id)
            for hashTag in hashTags do
                addHashTagMention hashTag.Value true tweet.Id db
            for mention in mentions do
                addHashTagMention mention.Value false tweet.Id db            
        | _ ->
            ()
        mailbox.Self.GracefulStop |> ignore
        return! loop()
    }
    loop()

let processTweetParentActor (mailbox: Actor<'a>) = 
    let mutable id = 0         
    let rec loop () = actor {
        let! message = mailbox.Receive ()  
        let actor1 = spawn system ("processTweetActor" + string(id)) processTweetActor                     
        actor1.Forward message
        id <- id + 1
        return! loop ()
    }
    loop ()

let processTweetAPI = spawn system "processTweetAPI" processTweetParentActor

let tweetActor (mailbox: Actor<'a>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "tweet actor: %A" message
        match box message with 
        | :? DataTypes.Tweet as tweet ->
            match loggedInUser tweet.UserId db with
            | true ->
                match isNull tweet.IsRetweet with
                | true ->
                    processTweetAPI <! tweet
                    addTweet tweet db //********************************check failure
                    addTweetToUser tweet db
                    // printfn "%A %A %A %A" hashTagDB mentionDB tweetDB userTweetsDB
                    mailbox.Sender() <! SUCCESS//200
                | false ->
                    mailbox.Sender() <! BADREQUEST//400
            | false -> 
                mailbox.Sender() <! UNAUTHORIZED//401
        | _ ->
            mailbox.Sender() <! BADREQUEST//400
        mailbox.Self.GracefulStop |> ignore
        return! loop ()
    }
    loop ()

let tweetParentActor (mailbox: Actor<'a>) = 
    let mutable id = 0         
    let rec loop () = actor {
        let! message = mailbox.Receive ()  
        let actor1 = spawn system ("tweetActor" + string(id)) tweetActor                     
        actor1.Forward message
        id <- id + 1
        return! loop ()
    }
    loop ()

let subscribeActor (mailbox: Actor<'a>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "subscribe actor: %A" message
        match box message with 
        | :? DataTypes.Follow as follow ->
            match bothRegisteredUser follow db with
            | true ->
                match loggedInUser follow.UserId db with
                | true ->
                    match subscribedUser follow db with
                    | true ->
                        mailbox.Sender() <! NOTACCEPTABLE//406
                    | false ->
                        match addSubscriber follow db with
                        | true ->
                            mailbox.Sender() <! SUCCESS//200
                        | false ->
                            mailbox.Sender() <! FAILED//500
                | false -> 
                    mailbox.Sender() <! UNAUTHORIZED//401
            | false ->
                mailbox.Sender() <! UNAUTHORIZED//401
        | _ ->
            mailbox.Sender() <! BADREQUEST//400
        mailbox.Self.GracefulStop |> ignore
        return! loop ()
    }
    loop ()

let subscribeParentActor (mailbox: Actor<'a>) = 
    let mutable id = 0         
    let rec loop () = actor {
        let! message = mailbox.Receive ()  
        let actor1 = spawn system ("subscribeActor" + string(id)) subscribeActor                     
        actor1.Forward message
        id <- id + 1
        return! loop ()
    }
    loop ()

let retweetActor (mailbox: Actor<'a>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "retweet actor: %A" message
        match box message with 
        | :? DataTypes.Tweet as tweet ->
            match loggedInUser tweet.UserId db with
            | true ->
                match not (isNull tweet.IsRetweet) with
                | true ->
                    addRetweet tweet db
                    addTweetToUser tweet db
                    mailbox.Sender() <! SUCCESS
                | false ->
                    mailbox.Sender() <! BADREQUEST//400 asking retweet actor with original tweet
                // printfn "%A %A %A %A" hashTagDB mentionDB tweetDB userTweetsDB
                mailbox.Sender() <! SUCCESS//200
            | false -> 
                mailbox.Sender() <! UNAUTHORIZED//401
        | _ ->
            mailbox.Sender() <! BADREQUEST//400
        mailbox.Self.GracefulStop |> ignore
        return! loop ()
    }
    loop ()

let retweetParentActor (mailbox: Actor<'a>) = 
    let mutable id = 0         
    let rec loop () = actor {
        let! message = mailbox.Receive ()  
        let actor1 = spawn system ("retweetActor" + string(id)) retweetActor                     
        actor1.Forward message
        id <- id + 1
        return! loop ()
    }
    loop ()

let echoActor (mailbox: Actor<'a>) =         
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        mailbox.Sender() <! DataTables.echo
        return! loop ()
    }
    loop ()



let registerAPI1 = spawn system "registerActor" registerParentActor

let logInAPI1 = spawn system "logInActor" logInParentActor

let tweetAPI1 = spawn system "tweetActor" tweetParentActor

let subscribeAPI1 = spawn system "subscribeActor" subscribeParentActor

let echoAPI1 = spawn system "echoActor" echoActor

printfn "%A %A %A %A %A" registerAPI1 logInAPI1 tweetAPI1 subscribeAPI1 echoAPI1

Console.ReadLine()

// dotnet fsi --langversion:preview TwitterEngine.fsx