#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote" 
#r "nuget: Akka.TestKit" 
#r ".\\bin\\Debug\\netcoreapp3.1\\Twitter.dll"
#load "DataTables.fsx"


open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Collections.Generic
open System.Text.RegularExpressions
open DataTables
open DataTypes
open System.Collections.Concurrent

let db = HASHMAP

let userActorRef = new ConcurrentDictionary<string, IActorRef>()


let registerSuccess userId timestamp = getResponse SUCCESS "SUCCESS" "Register" userId timestamp 

let registerBadRequest userId timestamp = getResponse BADREQUEST "BADREQUEST" "Register" userId timestamp 

let registerUnauthorized userId timestamp = getResponse UNAUTHORIZED "UNAUTHORIZED" "Register" userId timestamp 

let registerNotAcceptable userId timestamp = getResponse NOTACCEPTABLE "NOTACCEPTABLE" "Register" userId timestamp

let registerFailed userId timestamp = getResponse FAILED "FAILED" "Register" userId timestamp

let logInSuccess userId timestamp = getResponse SUCCESS "SUCCESS" "LogIn" userId timestamp

let logInBadRequest userId timestamp = getResponse BADREQUEST "BADREQUEST" "LogIn" userId timestamp

let logInUnauthorized userId timestamp = getResponse UNAUTHORIZED "UNAUTHORIZED" "LogIn" userId timestamp

let logInNotAcceptable userId timestamp = getResponse NOTACCEPTABLE "NOTACCEPTABLE" "LogIn" userId timestamp

let logInFailed userId timestamp = getResponse FAILED "FAILED" "LogIn" userId timestamp

let subscribeSuccess userId timestamp = getResponse SUCCESS "SUCCESS" "Subscribe" userId timestamp

let subscribeBadRequest userId timestamp = getResponse BADREQUEST "BADREQUEST" "Subscribe" userId timestamp

let subscribeUnauthorized userId timestamp = getResponse UNAUTHORIZED "UNAUTHORIZED" "Subscribe" userId timestamp

let subscribeNotAcceptable userId timestamp = getResponse NOTACCEPTABLE "NOTACCEPTABLE" "Subscribe" userId timestamp

let subscribeFailed userId timestamp = getResponse FAILED "FAILED" "Subscribe" userId timestamp

let tweetSuccess userId timestamp = getResponse SUCCESS "SUCCESS" "Tweet" userId timestamp

let tweetBadRequest userId timestamp = getResponse BADREQUEST "BADREQUEST" "Tweet" userId timestamp

let tweetUnauthorized userId timestamp = getResponse UNAUTHORIZED "UNAUTHORIZED" "Tweet" userId timestamp

let tweetNotAcceptable userId timestamp = getResponse NOTACCEPTABLE "NOTACCEPTABLE" "Tweet" userId timestamp

let tweetFailed userId timestamp = getResponse FAILED "FAILED" "Tweet" userId timestamp

let retweetSuccess userId timestamp = getResponse SUCCESS "SUCCESS" "Retweet" userId timestamp

let retweetBadRequest userId timestamp = getResponse BADREQUEST "BADREQUEST" "Retweet" userId timestamp

let retweetUnauthorized userId timestamp = getResponse UNAUTHORIZED "UNAUTHORIZED" "Retweet" userId timestamp

let retweetNotAcceptable userId timestamp = getResponse NOTACCEPTABLE "NOTACCEPTABLE" "Retweet" userId timestamp

let retweetFailed userId timestamp = getResponse FAILED "FAILED" "Retweet" userId timestamp

let querySuccess userId queryType tweets timestamp = getQueryResponse SUCCESS "SUCCESS" (queryType) userId tweets timestamp

let queryBadRequest userId queryType tweets timestamp = getQueryResponse BADREQUEST "BADREQUEST" (queryType) userId tweets timestamp

let queryUnauthorized userId queryType tweets timestamp = getQueryResponse UNAUTHORIZED "UNAUTHORIZED" (queryType) userId tweets timestamp

let queryNotAcceptable userId queryType tweets timestamp = getQueryResponse NOTACCEPTABLE "NOTACCEPTABLE" (queryType) userId tweets timestamp

let queryFailed userId queryType tweets timestamp = getQueryResponse FAILED "FAILED" (queryType) userId tweets timestamp

let logOutSuccess userId timestamp = getResponse SUCCESS "SUCCESS" "LogOut" userId timestamp

let logOutBadRequest userId timestamp = getResponse BADREQUEST "BADREQUEST" "LogOut" userId timestamp

let logOutUnauthorized userId timestamp = getResponse UNAUTHORIZED "UNAUTHORIZED" "LogOut" userId timestamp

let logOutNotAcceptable userId timestamp = getResponse NOTACCEPTABLE "NOTACCEPTABLE" "LogOut" userId timestamp

let logOutFailed userId timestamp = getResponse FAILED "FAILED" "LogOut" userId timestamp

let configuration = 
                    ConfigurationFactory.ParseString(
                        @"akka {
                            actor {
                                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                            }
                            remote {
                                helios.tcp {
                                    port = 8081
                                    hostname = localhost
                                }
                            }
                        }")


let system = ActorSystem.Create("RemoteSystem", configuration)


let registerActor (mailbox : Actor<'a>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive()
        // printfn "register actor: %A" message
        match box message with
        | :? Credentials as credentials ->
            match registeredUser credentials.UserName credentials.Password db with
            | true ->
                mailbox.Sender() <! registerNotAcceptable credentials.UserName credentials.TimeStamp
            | false ->
                match addUser credentials.UserName credentials.Password db with
                | true ->
                    mailbox.Sender() <! registerSuccess credentials.UserName credentials.TimeStamp
                | false ->
                    mailbox.Sender() <! registerFailed credentials.UserName credentials.TimeStamp             
        | _ ->
            mailbox.Sender() <! registerBadRequest null (DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds())
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
            // printfn "logIn actor: %A" message
            let rows = registerTable.Select()            
            for row in rows do 
                printfn "%A" row.ItemArray
            match box message with 
            | :? Credentials as c ->
                match registeredUser c.UserName c.Password db with
                | true ->
                        match loggedInUser c.UserName db with
                        | true -> 
                            mailbox.Sender() <! logInNotAcceptable c.UserName c.TimeStamp//406
                        | false ->
                            match logInUser c.UserName c.Password db with        
                            | true -> 
                                userActorRef.TryAdd(c.UserName, mailbox.Sender()) |> ignore
                                mailbox.Sender() <! logInSuccess c.UserName c.TimeStamp//200
                            | false ->
                                mailbox.Sender() <! logInFailed c.UserName c.TimeStamp//500
                | false ->
                        mailbox.Sender() <! logInUnauthorized c.UserName c.TimeStamp//401
            | _ ->
                mailbox.Sender() <! logInBadRequest null (DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds())//400
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
        // printfn "process tweet actor: %A" message
        match box message with
        | :? Tweet as tweet ->
            let hashTags = Regex(@"#\w+").Matches tweet.Content |> Seq.distinctBy (id)
            let mentions = Regex(@"@\w+").Matches tweet.Content |> Seq.distinctBy (id)
            for hashTag in hashTags do
                addHashTagMention hashTag.Value true tweet.Id db
            for mention in mentions do
                addHashTagMention mention.Value false tweet.Id db    
                if(mention.Value.[1..] <> tweet.UserId && userActorRef.ContainsKey(mention.Value.[1..])) then
                    userActorRef.[mention.Value.[1..]] <! getLiveTweet tweet.Id tweet.UserId tweet.Content tweet.TimeStamp tweet.IsRetweetOf    
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
        // printfn "tweet actor: %A" message
        match box message with 
        | :? DataTypes.TweetInfo as tweetInfo ->
            match loggedInUser tweetInfo.UserId db with
            | true ->
                let tweet = getTweet ((Guid.NewGuid()).ToString()) tweetInfo.UserId tweetInfo.Content (DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds()) null
                processTweetAPI <! tweet
                addTweet tweet db //********************************check failure
                addTweetToUser tweet db                
                // printfn "%A %A %A %A" hashTagDB mentionDB tweetDB userTweetsDB
                mailbox.Sender() <! tweetSuccess tweetInfo.UserId tweetInfo.TimeStamp//200
                // sending live tweets
                for follower in followersDB.GetOrAdd(tweet.UserId, new HashSet<string>()) do
                    if(loggedInUser follower db) then
                        // let actor = system.ActorSelection("akka.tcp://System@localhost:8085/user/simulatorActor" + follower)
                        let actor = userActorRef.[follower]
                        actor <! getLiveTweet tweet.Id tweet.UserId tweet.Content tweet.TimeStamp tweet.IsRetweetOf
                userActorRef.[tweet.UserId] <! getLiveTweet tweet.Id tweet.UserId tweet.Content tweet.TimeStamp tweet.IsRetweetOf
            | false -> 
                mailbox.Sender() <! tweetUnauthorized tweetInfo.UserId tweetInfo.TimeStamp//401
        | _ ->
            mailbox.Sender() <! tweetBadRequest null (DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds())//400
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
        // printfn "subscribe actor: %A" message
        let timtaken = DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds()
        // printfn "recieved in %A at %A" mailbox.Self timtaken
        match box message with 
        | :? DataTypes.Follow as follow ->
            match bothRegisteredUser follow db with
            | true ->
                match loggedInUser follow.UserId db with
                | true ->
                    match subscribedUser follow db with
                    | true ->
                        // mailbox.Sender() <! subscribeNotAcceptable follow.UserId follow.TimeStamp//406
                        mailbox.Sender() <! getResponse NOTACCEPTABLE "NOTACCEPTABLE" "Subscribe" follow.UserId follow.TimeStamp
                    | false ->
                        match addSubscriber follow db with
                        | true ->
                            // printfn "Time taken to subscribe %A" (DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds() - timtaken)
                            // mailbox.Sender() <! subscribeSuccess follow.UserId follow.TimeStamp//200
                            mailbox.Sender() <! getResponse SUCCESS "SUCCESS" "Subscribe" follow.UserId follow.TimeStamp
                            // printfn "sending at %A" (DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds())
                        | false ->
                            // mailbox.Sender() <! subscribeFailed follow.UserId follow.TimeStamp//500
                            mailbox.Sender() <! getResponse FAILED "FAILED" "Subscribe" follow.UserId follow.TimeStamp
                | false -> 
                    // mailbox.Sender() <! subscribeUnauthorized follow.UserId follow.TimeStamp//401
                    // printfn "Time taken to subscribe %A" (DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds() - timtaken)
                    mailbox.Sender() <! getResponse UNAUTHORIZED "UNAUTHORIZED" "Subscribe" follow.UserId follow.TimeStamp
                    // printfn "sending at %A" (DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds())
            | false ->
                // mailbox.Sender() <! subscribeBadRequest follow.UserId follow.TimeStamp//401
                mailbox.Sender() <! getResponse BADREQUEST "BADREQUEST" "Subscribe" follow.UserId follow.TimeStamp
        | _ ->
            mailbox.Sender() <! subscribeBadRequest null (DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds())//400
        mailbox.Self.GracefulStop |> ignore
        return! loop ()
    }
    loop ()

let subscribeParentActor (mailbox: Actor<'a>) = 
    let mutable id = 0 
    let rec loop () = actor {
        let! message = mailbox.Receive ()  
        let actor1 = spawn system ("subscribeActor" + string(id)) subscribeActor
        // printfn "sending to child %A at %A" id (DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds())                
        actor1.Forward message
        id <- id + 1
        return! loop ()
    }
    loop ()

let retweetActor (mailbox: Actor<'a>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        // printfn "retweet actor: %A" message
        match box message with 
        | :? DataTypes.Retweet as retweet ->
            match loggedInUser retweet.UserId db with
            | true ->
                match tweetIdExists retweet.TweetId db with
                | true ->
                    let tweet = getTweet ((Guid.NewGuid()).ToString()) retweet.UserId (getContent retweet.TweetId db) (DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds()) retweet.TweetId
                    processTweetAPI <! tweet
                    addRetweet tweet db
                    addTweetToUser tweet db                    
                    // printfn "%A %A %A %A" hashTagDB mentionDB tweetDB userTweetsDB
                    mailbox.Sender() <! retweetSuccess retweet.UserId retweet.TimeStamp//200
                    // sending live tweets
                    for follower in followersDB.GetOrAdd(tweet.UserId, new HashSet<string>()) do
                        if(loggedInUser follower db) then
                            // let actor = system.ActorSelection("akka.tcp://System@localhost:8085/user/simulatorActor" + follower)
                            let actor = userActorRef.[follower]
                            actor <! getLiveTweet tweet.Id tweet.UserId tweet.Content tweet.TimeStamp tweet.IsRetweetOf
                    userActorRef.[tweet.UserId] <! getLiveTweet tweet.Id tweet.UserId tweet.Content tweet.TimeStamp tweet.IsRetweetOf
                | false ->
                    mailbox.Sender() <! retweetBadRequest retweet.UserId retweet.TimeStamp//400
            | false -> 
                mailbox.Sender() <! retweetUnauthorized retweet.UserId retweet.TimeStamp//401
        | _ ->
            mailbox.Sender() <! retweetBadRequest null (DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds())//400
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
        // printfn "query actor: %A" message
        match box message with 
        | :? DataTypes.Query as query ->
            match loggedInUser query.UserId db with
            | true ->
                match query.QueryType with
                | "Query-subscribedTo" ->
                    let tweets = getTweetsBySubcribedTo query db
                    mailbox.Sender() <! querySuccess query.UserId query.QueryType tweets query.TimeStamp
                | "Query-hashTag" ->
                    match query.QueryContent.StartsWith "#" with
                    | true ->
                        let tweets = getTweetsByHashTag query db
                        mailbox.Sender() <! querySuccess query.UserId query.QueryType tweets query.TimeStamp
                    | false -> 
                        mailbox.Sender() <! queryBadRequest query.UserId query.QueryType (new List<Tweet>()) query.TimeStamp
                | "Query-mention" ->
                    let tweets = getTweetsByMention query db
                    mailbox.Sender() <! querySuccess query.UserId query.QueryType tweets query.TimeStamp
                | _ ->
                    mailbox.Sender() <! queryBadRequest query.UserId query.QueryType (new List<Tweet>()) query.TimeStamp
            | false -> 
                mailbox.Sender() <! queryUnauthorized query.UserId query.QueryType (new List<Tweet>()) query.TimeStamp//401
        | _ ->
            mailbox.Sender() <! queryBadRequest null null (new List<Tweet>()) (DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds())//400
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
            // printfn "logOut actor: %A" message
            match box message with 
            | :? Credentials as c ->
                match registeredUser c.UserName c.Password db with
                | true ->
                    match loggedInUser c.UserName db with
                    | true ->
                        match logOut c.UserName db with
                        | true ->
                            userActorRef.Remove c.UserName |> ignore
                            mailbox.Sender() <! logOutSuccess c.UserName c.TimeStamp//200
                        | false ->
                            mailbox.Sender() <! logOutFailed c.UserName c.TimeStamp//200
                    | false ->                            
                        mailbox.Sender() <! logOutNotAcceptable c.UserName c.TimeStamp//406
                | false ->
                        mailbox.Sender() <! logOutUnauthorized c.UserName c.TimeStamp//401
            | _ ->
                mailbox.Sender() <! logOutBadRequest null (DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds())//400
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

// printfn "%A %A %A %A %A %A %A %A" registerAPI1 logInAPI1 tweetAPI1 subscribeAPI1 retweetAPI1 echoAPI1 queryAPI1 logOutAPI1

// Console.ReadLine()

// dotnet fsi --langversion:preview TwitterEngine.fsx