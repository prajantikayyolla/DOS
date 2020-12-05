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


let registerSuccess userId = getResponse SUCCESS "SUCCESS" "Register" userId

let registerBadRequest userId = getResponse BADREQUEST "BADREQUEST" "Register" userId

let registerUnauthorized userId = getResponse UNAUTHORIZED "UNAUTHORIZED" "Register" userId

let registerNotAcceptable userId = getResponse NOTACCEPTABLE "NOTACCEPTABLE" "Register" userId

let registerFailed userId = getResponse FAILED "FAILED" "Register" userId

let logInSuccess userId = getResponse SUCCESS "SUCCESS" "LogIn" userId

let logInBadRequest userId = getResponse BADREQUEST "BADREQUEST" "LogIn" userId

let logInUnauthorized userId = getResponse UNAUTHORIZED "UNAUTHORIZED" "LogIn" userId

let logInNotAcceptable userId = getResponse NOTACCEPTABLE "NOTACCEPTABLE" "LogIn" userId

let logInFailed userId = getResponse FAILED "FAILED" "LogIn" userId

let subscribeSuccess userId = getResponse SUCCESS "SUCCESS" "Subscribe" userId

let subscribeBadRequest userId = getResponse BADREQUEST "BADREQUEST" "Subscribe" userId

let subscribeUnauthorized userId = getResponse UNAUTHORIZED "UNAUTHORIZED" "Subscribe" userId

let subscribeNotAcceptable userId = getResponse NOTACCEPTABLE "NOTACCEPTABLE" "Subscribe" userId

let subscribeFailed userId = getResponse FAILED "FAILED" "Subscribe" userId

let tweetSuccess userId = getResponse SUCCESS "SUCCESS" "Tweet" userId

let tweetBadRequest userId = getResponse BADREQUEST "BADREQUEST" "Tweet" userId

let tweetUnauthorized userId = getResponse UNAUTHORIZED "UNAUTHORIZED" "Tweet" userId

let tweetNotAcceptable userId = getResponse NOTACCEPTABLE "NOTACCEPTABLE" "Tweet" userId

let tweetFailed userId = getResponse FAILED "FAILED" "Tweet" userId

let retweetSuccess userId = getResponse SUCCESS "SUCCESS" "Retweet" userId

let retweetBadRequest userId = getResponse BADREQUEST "BADREQUEST" "Retweet" userId

let retweetUnauthorized userId = getResponse UNAUTHORIZED "UNAUTHORIZED" "Retweet" userId

let retweetNotAcceptable userId = getResponse NOTACCEPTABLE "NOTACCEPTABLE" "Retweet" userId

let retweetFailed userId = getResponse FAILED "FAILED" "Retweet" userId

let querySuccess userId queryType tweets = getQueryResponse SUCCESS "SUCCESS" ("Query-"+queryType) userId tweets

let queryBadRequest userId queryType tweets = getQueryResponse BADREQUEST "BADREQUEST" ("Query-"+queryType) userId tweets

let queryUnauthorized userId queryType tweets = getQueryResponse UNAUTHORIZED "UNAUTHORIZED" ("Query-"+queryType) userId tweets

let queryNotAcceptable userId queryType tweets = getQueryResponse NOTACCEPTABLE "NOTACCEPTABLE" ("Query-"+queryType) userId tweets

let queryFailed userId queryType tweets = getQueryResponse FAILED "FAILED" ("Query-"+queryType) userId tweets

let logOutSuccess userId = getResponse SUCCESS "SUCCESS" "LogOut" userId

let logOutBadRequest userId = getResponse BADREQUEST "BADREQUEST" "LogOut" userId

let logOutUnauthorized userId = getResponse UNAUTHORIZED "UNAUTHORIZED" "LogOut" userId

let logOutNotAcceptable userId = getResponse NOTACCEPTABLE "NOTACCEPTABLE" "LogOut" userId

let logOutFailed userId = getResponse FAILED "FAILED" "LogOut" userId

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
                mailbox.Sender() <! registerNotAcceptable credentials.UserName
            | false ->
                match addUser credentials.UserName credentials.Password db with
                | true ->
                    mailbox.Sender() <! registerSuccess credentials.UserName
                | false ->
                    mailbox.Sender() <! registerFailed credentials.UserName               
        | _ ->
            mailbox.Sender() <! registerBadRequest null
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
                            mailbox.Sender() <! logInNotAcceptable c.UserName//406
                        | false ->
                            match logInUser c.UserName c.Password db with        
                            | true -> 
                                mailbox.Sender() <! logInSuccess c.UserName//200
                            | false ->
                                mailbox.Sender() <! logInFailed c.UserName//500
                | false ->
                        mailbox.Sender() <! logInUnauthorized c.UserName//401
            | _ ->
                mailbox.Sender() <! logInBadRequest null//400
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
        | :? DataTypes.TweetInfo as tweetInfo ->
            match loggedInUser tweetInfo.UserId db with
            | true ->
                let tweet = getTweet ((Guid.NewGuid()).ToString()) tweetInfo.UserId tweetInfo.Content (System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")) null
                processTweetAPI <! tweet
                addTweet tweet db //********************************check failure
                addTweetToUser tweet db
                // printfn "%A %A %A %A" hashTagDB mentionDB tweetDB userTweetsDB
                mailbox.Sender() <! tweetSuccess tweetInfo.UserId//200
            | false -> 
                mailbox.Sender() <! tweetUnauthorized tweetInfo.UserId//401
        | _ ->
            mailbox.Sender() <! tweetBadRequest null//400
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
                        mailbox.Sender() <! subscribeNotAcceptable follow.UserId//406
                    | false ->
                        match addSubscriber follow db with
                        | true ->
                            mailbox.Sender() <! subscribeSuccess follow.UserId//200
                        | false ->
                            mailbox.Sender() <! subscribeFailed follow.UserId//500
                | false -> 
                    mailbox.Sender() <! subscribeUnauthorized follow.UserId//401
            | false ->
                mailbox.Sender() <! subscribeBadRequest follow.UserId//401
        | _ ->
            mailbox.Sender() <! subscribeBadRequest null//400
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
        | :? DataTypes.Retweet as retweet ->
            match loggedInUser retweet.UserId db with
            | true ->
                match tweetIdExists retweet.TweetId db with
                | true ->
                    let tweet = getTweet ((Guid.NewGuid()).ToString()) retweet.UserId (getContent retweet.TweetId db) (System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")) retweet.TweetId
                    processTweetAPI <! tweet
                    addRetweet tweet db
                    addTweetToUser tweet db
                    // printfn "%A %A %A %A" hashTagDB mentionDB tweetDB userTweetsDB
                    mailbox.Sender() <! retweetSuccess retweet.UserId//200
                | false ->
                    mailbox.Sender() <! retweetBadRequest retweet.UserId//400
            | false -> 
                mailbox.Sender() <! retweetUnauthorized retweet.UserId//401
        | _ ->
            mailbox.Sender() <! retweetBadRequest null//400
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

let queryActor (mailbox: Actor<'a>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "query actor: %A" message
        match box message with 
        | :? DataTypes.Query as query ->
            match loggedInUser query.UserId db with
            | true ->
                match query.QueryType with
                | "subscribedTo" ->
                    let tweets = getTweetsBySubcribedTo query db
                    mailbox.Sender() <! querySuccess query.UserId query.QueryType tweets
                | "hashTag" ->
                    match query.QueryContent.StartsWith "#" with
                    | true ->
                        let tweets = getTweetsByHashTag query db
                        mailbox.Sender() <! querySuccess query.UserId query.QueryType tweets
                    | false -> 
                        mailbox.Sender() <! queryBadRequest query.UserId query.QueryType (new List<Tweet>())
                | "mention" ->
                    let tweets = getTweetsByMention query db
                    mailbox.Sender() <! querySuccess query.UserId query.QueryType tweets
                | _ ->
                    mailbox.Sender() <! queryBadRequest query.UserId query.QueryType (new List<Tweet>())
            | false -> 
                mailbox.Sender() <! queryUnauthorized query.UserId query.QueryType (new List<Tweet>())//401
        | _ ->
            mailbox.Sender() <! queryBadRequest null null (new List<Tweet>())//400
        mailbox.Self.GracefulStop |> ignore
        return! loop ()
    }
    loop ()

let queryParentActor (mailbox: Actor<'a>) = 
    let mutable id = 0         
    let rec loop () = actor {
        let! message = mailbox.Receive ()  
        let actor1 = spawn system ("queryActor" + string(id)) queryActor                     
        actor1.Forward message
        id <- id + 1
        return! loop ()
    }
    loop ()

let logOutActor (mailbox: Actor<'a>) = 
        let rec loop () = actor {
            let! message = mailbox.Receive ()
            printfn "logOut actor: %A" message
            match box message with 
            | :? Credentials as c ->
                match registeredUser c.UserName c.Password db with
                | true ->
                    match loggedInUser c.UserName db with
                    | true ->
                        match logOut c.UserName db with
                        | true ->
                            mailbox.Sender() <! logOutSuccess c.UserName//200
                        | false ->
                            mailbox.Sender() <! logOutFailed c.UserName//200
                    | false ->                            
                        mailbox.Sender() <! logOutNotAcceptable c.UserName//406
                | false ->
                        mailbox.Sender() <! logOutUnauthorized c.UserName//401
            | _ ->
                mailbox.Sender() <! logOutBadRequest null//400
            mailbox.Self.GracefulStop |> ignore
            return! loop ()
        }
        loop ()

let logOutParentActor (mailbox: Actor<'a>) = 
    let mutable id = 0         
    let rec loop () = actor {
        let! message = mailbox.Receive ()  
        let actor1 = spawn system ("logOutActor" + string(id)) logOutActor                     
        actor1.Forward message
        id <- id + 1
        return! loop ()
    }
    loop ()

let echoActor (mailbox: Actor<'a>) =         
    let rec loop () = actor {
        let! message = mailbox.Receive()
        mailbox.Sender() <! echo()
        return! loop ()
    }
    loop ()



let registerAPI1 = spawn system "registerActor" registerParentActor

let logInAPI1 = spawn system "logInActor" logInParentActor

let tweetAPI1 = spawn system "tweetActor" tweetParentActor

let subscribeAPI1 = spawn system "subscribeActor" subscribeParentActor

let echoAPI1 = spawn system "echoActor" echoActor

let retweetAPI1 = spawn system "retweetActor" retweetParentActor

let queryAPI1 = spawn system "queryActor" queryParentActor

let logOutAPI1 = spawn system "logOutActor" logOutParentActor

printfn "%A %A %A %A %A %A %A %A" registerAPI1 logInAPI1 tweetAPI1 subscribeAPI1 retweetAPI1 echoAPI1 queryAPI1 logOutAPI1

Console.ReadLine()

// dotnet fsi --langversion:preview TwitterEngine.fsx