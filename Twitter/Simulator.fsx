open System.Threading
open System.Collections.Concurrent
#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote" 
#r "nuget: Akka.TestKit" 
#r "C:\\Prajan\\Sem-3\\DOS\\DOS\\Twitter\\bin\\Debug\\netcoreapp3.1\\Twitter.dll"
#r ".\\bin\\Debug\\netcoreapp3.1\\MathNet.Numerics.dll"
#r ".\\bin\\Debug\\netcoreapp3.1\\MathNet.Numerics.FSharp.dll"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Collections.Generic
open DataTypes
open MathNet.Numerics
open System.Text.RegularExpressions

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

let retweetAPI = system.ActorSelection(
                            "akka.tcp://RemoteSystem@localhost:8080/user/retweetActor")

let subscribeAPI = system.ActorSelection(
                            "akka.tcp://RemoteSystem@localhost:8080/user/subscribeActor")

let queryAPI = system.ActorSelection(
                            "akka.tcp://RemoteSystem@localhost:8080/user/queryActor")

let echoAPI = system.ActorSelection(
                            "akka.tcp://RemoteSystem@localhost:8080/user/echoActor")

let logOutAPI = system.ActorSelection(
                            "akka.tcp://RemoteSystem@localhost:8080/user/logOutActor")

printfn "%A %A" registerAPI logInAPI

let zipfDistribution = Distributions.Zipf(1.0, 100)

let numClients = 100

let rand = Random()

let clients = new List<IActorRef>()

let mutable terminate = false

// let clientActor id (mailbox : Actor<'a>) = 
//     let rec loop () = actor {
//         let! message = mailbox.Receive()
//         match box message with
//         | :? Credentials as credentials ->
//             let (response : Response) = (Async.RunSynchronously(registerAPI <? getCredentials id id))
//             match response.Status with
//             | 200 ->
//                 printfn "%A" (Async.RunSynchronously(logInAPI <? getCredentials id id))
//             | _ ->
//                 printfn "here"
//                 ()
//         | :? Follow as follow ->
//             printfn "%A" (Async.RunSynchronously(subscribeAPI <? follow))
//         | :? StartTweeting as startTweeting ->
//             for i in 1 .. startTweeting.NumberOfTweets do
//                 printfn "%A" (Async.RunSynchronously(tweetAPI <? getTweetInfo id ("tweet #" + (string <| rand.Next(1000)) + " of @" + id)))
//         | :? String as s ->
//             match s with
//             | "logIn" ->
//                 printfn "%A" (Async.RunSynchronously(logInAPI <? getCredentials id id)) 
//             | "logOut" ->
//                 printfn "%A" (Async.RunSynchronously(logOutAPI <? getCredentials id id)) 
//             | "queryAndRetweet" ->
//                 let tweetsByMention = (Async.RunSynchronously(queryAPI <? getQuery id "mention" null)) 
//                 let tweetsSubscribedTo = (Async.RunSynchronously(queryAPI <? getQuery id "subscribedTo" null))
//                 printfn "%A" tweetsByMention
//                 printfn "%A" tweetsSubscribedTo
//                 let tweetsCount = tweetsSubscribedTo.Tweets.Count
//                 let mutable retweetFraction = (int) (Math.Ceiling(float tweetsCount * 0.3))
//                 while tweetsCount <> 0 && retweetFraction <> 0 do
//                     let tweetIdToRetweet = tweetsSubscribedTo.Tweets.[rand.Next(tweetsCount)].Id
//                     printfn "%A" (Async.RunSynchronously(retweetAPI <? getRetweet id tweetIdToRetweet)) 
//                     retweetFraction <- retweetFraction - 1
//                 if tweetsCount <> 0 then
//                     let hashTags = Regex(@"#\w+").Matches tweetsSubscribedTo.Tweets.[0].Content
//                     for hashTag in hashTags do
//                         printfn "%A" (Async.RunSynchronously(queryAPI <? getQuery id "hashTag" hashTag.Value))  
//             | "start" ->
//                 let (response : Response) = (Async.RunSynchronously(registerAPI <? getCredentials id id))
//                 match response.Status with
//                 | 200 ->
//                     printfn "%A" (Async.RunSynchronously(logInAPI <? getCredentials id id)) 
//                     // printfn "%A" (Async.RunSynchronously(tweetAPI <? getTweetInfo id ("First tweet #" + id + " of @" + id)))
//                     let rand = Random()
//                     for i in 0 .. (int (100.0 * (zipfDistribution.Probability (id |> int)))) do     
//                         printfn "%A" (Async.RunSynchronously(tweetAPI <? getTweetInfo id ("tweet #" + (string <| rand.Next(1000)) + " of @" + id)))
//                     printfn "%A" (Async.RunSynchronously(subscribeAPI <? getFollow id (string((int id) - 1))))  
//                     for i in 1 .. 3 do   
//                         let tweetId =  (Async.RunSynchronously(echoAPI <? "hi"))
//                         printfn "%A" tweetId
//                         printfn "%A" (Async.RunSynchronously(retweetAPI <? getRetweet id tweetId))  
//                     printfn "%A" (Async.RunSynchronously(queryAPI <? getQuery id "subscribedTo" null))     
//                 | _ -> printfn " not success"         
//             | _ ->
//                 ()
//         | _ ->
//             ()
//         // mailbox.Self.GracefulStop |> ignore
//         return! loop()
//     }
//     loop()

// let clientParentActor (mailbox : Actor<'a>) = 
//     let rec loop () = actor {
//         let! message = mailbox.Receive()
//         printfn "%A %A %A" message (System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")) (clients.Count)
//         match message with
//         | "start" ->
//             for i in 0 .. numClients - 1 do
//                 clients.[i] <! "start"
//         | "registerLogin" ->
//             for i in 0 .. numClients - 1 do
//                 clients.[i] <! getCredentials (string(i)) (string(i))
//         | "getSubscribersByZipf" ->
//             let mutable first = 0
//             let mutable last = 0
//             let mutable temp = 0
//             for userId in 0 .. numClients - 1 do
//                 let subscribersByZipfDistribution = int (Math.Ceiling((zipfDistribution.Probability (userId + 1)) * float numClients))
//                 first <- last
//                 temp <- first + subscribersByZipfDistribution
//                 last <- Math.Min((first + subscribersByZipfDistribution), (numClients - 1))
//                 printfn "%A %A %A" first last subscribersByZipfDistribution
//                 for j in first .. Math.Min(numClients - 1, last) do
//                     if j <> userId then
//                         clients.[j] <! getFollow (string j) (string userId)
//                 if temp >= numClients then
//                     last <- temp % numClients
//                     printfn "%A %A" 0 last
//                     for j in 0 .. Math.Min(numClients - 1, last) do
//                         clients.[j] <! getFollow (string j) (string userId)
//         | "doTweetsWithIntervals" ->
//             let loggedOut = new HashSet<IActorRef>()
//             let  mutable c = 0
//             while c < 5 do
//                 Thread.Sleep 5000 |> ignore  
//                 for client in loggedOut do
//                     client <! "logIn"
//                 loggedOut.Clear |> ignore
//                 let mutable num = 0
//                 while num < (numClients / 2) do      
//                     let client = clients.[rand.Next(numClients)]
//                     client <! "logOut"
//                     loggedOut.Add client |> ignore    
//                     num <- num + 1
//                 for userId in 0 .. numClients - 1 do
//                     if not (loggedOut.Contains clients.[userId]) then
//                         let numOfTweets = int (Math.Ceiling((zipfDistribution.Probability (userId + 1) * float numClients)))
//                         clients.[userId] <! getStartTweeting numOfTweets
//                 c <- c + 1
//         | "queryAndRetweet" ->
//             for userId in 0 .. numClients - 1 do
//                 clients.[userId] <! "queryAndRetweet"
//         | _ ->
//             ()
//         mailbox.Self.GracefulStop |> ignore
//         return! loop()
//     }
//     loop()

let averageResponseTime = new ConcurrentDictionary<string, TimeSpan>()
let numberOfRequests = new ConcurrentDictionary<string, int>()

let services = ["register"; "logIn"; "logOut"; "subscribe"; "tweet"; "querySubscribedTo"; "queryHashTag"; "queryMention"; "retweet"; "test"]

let totalServices = services.Length

let hashTags = ["#ipl";"#cricket";"#viratkohli";"#dream";"#msdhoni";"#csk";"#rohitsharma";"#rcb";"#mumbaiindians";"#dhoni";"#india";"#chennaisuperkings";"#memes";"#kkr";"#mi";"#indiancricketteam";"#iplt";"#klrahul";"#royalchallengersbangalore";"#srh";"#abdevilliers";"#love";"#icc";"#indiancricket";"#hardikpandya";"#iplmemes";"#teamindia";"#t";"#indianpremierleague";"#bhfyp";"#virat";"#bcci";"#delhicapitals";"#cricketfans";"#msd";"#cricketmerijaan";"#mahi";"#kxip";"#cricketlovers";"#cricketer";"#instagram";"#sachintendulkar";"#rajasthanroyals";"#cskfans";"#sunrisershyderabad";"#kolkataknightriders";"#lovecricket";"#playbold";"#kingkohli";"#iplupdates";"#vivoipl";"#davidwarner";"#sureshraina";"#rr";"#jaspritbumrah";"#hitman";"#trending";"#whistlepodu";"#cricketlover";"#cricketmemes";
"#election";"#trump";"#vote";"#maga";"#democrats";"#donaldtrump";"#voteblue";"#resist";"#biden";"#politics";"#coronavirus";"#covid";"#republican";"#kag";"#joebiden";"#liberal";"#america";"#usa";"#democrat";"#guncontrol";"#trumpsucks";"#bluewave";"#qanon";"#gop";"#berniesanders";"#moscowmitch";"#impotus";"#bernie";"#trumptreason";"#bhfyp";"#elections";"#recession";"#jerryfalwelljr";"#mikehuckabee";"#sarahhuckabeesanders";"#bebest";"#jimbakkershow";"#impeachkavanaugh";"#sharpiegate";"#racistpresident";"#evangelicals";"#racistinchief";"#trumpvoters";"#tbn";"#conservative";"#president";"#votebluenomatterwho";"#impeached";"#huckabeeontbn";"#getoutthevote";"#paulawhiteministries";"#notmypresident";"#votebluetosaveamerica";"#goplies";"#andrewyang";"#votebluetoendthisnightmare";"#fattrump";
"#yanggang";"#keepamericagreat";"#yang";"#coronavirus";"#covid";"#corona";"#stayhome";"#quarantine";"#lockdown";"#staysafe";"#socialdistancing";"#love";"#pandemic";"#stayathome";"#virus";"#quedateencasa";"#cuarentena";"#o";"#rus";"#a";"#instagram";"#coronav";"#pandemia";"#memes";"#instagood";"#like";"#follow";"#s";"#india";"#n";"#dirumahaja";"#d";"#bhfyp";"#yomequedoencasa";"#photography";"#art";"#news";"#health";"#repost";"#meme";"#cov";"#quarantinelife";"#music";"#tiktok";"#italia";"#likeforlikes";"#photooftheday";"#stayhealthy";"#life";"#fiqueemcasa";"#iorestoacasa";"#mask";"#viral";"#brasil";"#fitness";"#indonesia";"#coronamemes";"#funny";"#followforfollowback";"#fashion";"#usa";"#salud";"#m";"#premierleague";"#football";"#soccer";"#fifa";"#championsleague";"#liverpool";"#epl";"#laliga";
"#manchesterunited";"#chelsea";"#arsenal";"#england";"#messi";"#futbol";"#pl";"#seriea";"#bundesliga";"#liverpoolfc";"#mufc";"#ronaldo";"#mancity";"#cr";"#chelseafc";"#realmadrid";"#lfc";"#ynwa";"#manutd";"#ucl";"#cfc";"#bhfyp";"#sport";"#fut";"#like";"#ligue";"#futebol";"#follow";"#tottenham";"#europaleague";"#goal";"#calcio";"#manchester";"#spurs";"#neymar";"#uefa";"#manunited";"#juventus";"#ggmu";"#sports";"#champions";"#london";"#f";"#facup";"#everton";"#anfield";"#championship";"#ktbffh";"#arsenalfc";"#bhfyp";"#nike";"#transfer"]

let hashTagsCount = hashTags.Length

let subscribersList = new ConcurrentDictionary<int, List<int>>()

let completedSimulators = new BlockingCollection<IActorRef>()

let simulatorActor id (mailbox : Actor<'a>) = 
    let mutable startTime = DateTime.Now
    let mutable response = null
    let mutable responseTime = startTime - startTime
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match box message with 
        | :? string as service ->
            if service <> "tweet" && service <> "retweet" then                              
                match service with
                | "register" ->
                    startTime <- DateTime.Now 
                    response <- (Async.RunSynchronously(registerAPI <? getCredentials id id))
                    responseTime <- DateTime.Now - startTime
                | "logIn" ->
                    startTime <- DateTime.Now  
                    response <- (Async.RunSynchronously(logInAPI <? getCredentials id id))
                    responseTime <- DateTime.Now - startTime
                | "logOut" ->
                    startTime <- DateTime.Now  
                    response <- (Async.RunSynchronously(logOutAPI <? getCredentials id id)) 
                    responseTime <- DateTime.Now - startTime                    
                | "querySubscribedTo" ->   
                    startTime <- DateTime.Now  
                    response <- (Async.RunSynchronously(queryAPI <? getQuery id "subscribedTo" null))
                    responseTime <- DateTime.Now - startTime
                | "queryHashTag" ->
                    startTime <- DateTime.Now  
                    response <- (Async.RunSynchronously(queryAPI <? getQuery id "hashTag" (hashTags.Item(rand.Next(hashTagsCount)))))
                    responseTime <- DateTime.Now - startTime
                | "queryMention" ->
                    startTime <- DateTime.Now  
                    response <- (Async.RunSynchronously(queryAPI <? getQuery id "mention" null))
                    responseTime <- DateTime.Now - startTime 
                | "subscribe" ->
                    startTime <- DateTime.Now  
                    response <- (Async.RunSynchronously(subscribeAPI <? getFollow id (string (subscribersList.[int id].Item(rand.Next(subscribersList.[int id].Count))))))
                    responseTime <- DateTime.Now - startTime
                | "stop" ->
                    completedSimulators.Add mailbox.Self
                    mailbox.Sender() <! "Done"
                    mailbox.Self.GracefulStop |> ignore                    
                    return! loop()
                | _ ->
                    return! loop()                        
                averageResponseTime.[service] <- averageResponseTime.[service] + responseTime
                numberOfRequests.[service] <- numberOfRequests.[service] + 1
                printfn "%A Response: %A" service response
            else 
                responseTime <- startTime - startTime// making response time 0 and adding all times for num requets and finally adding to dictionary
                match service with
                | "tweet" ->                    
                    let numOfTweets = int (Math.Ceiling((zipfDistribution.Probability (int id + 1) * float numClients)))
                    for i in 1 ..numOfTweets do
                        startTime <- DateTime.Now 
                        response <- (Async.RunSynchronously(tweetAPI <? getTweetInfo id ("tweet of " + (hashTags.Item(rand.Next(hashTagsCount))) + " and mentioning @" + (string (rand.Next(numClients))))))
                        responseTime <- responseTime + (DateTime.Now - startTime)    
                        printfn "%A Response: %A and time is %A" service response responseTime
                    printfn "total time %A and request %A and avg %A and %A %A" responseTime numOfTweets (responseTime.Milliseconds / numOfTweets) averageResponseTime.[service] numberOfRequests.[service]
                    averageResponseTime.[service] <- averageResponseTime.[service] + responseTime
                    numberOfRequests.[service] <- numberOfRequests.[service] + numOfTweets
                | "retweet" ->
                    let tweetsSubscribedTo = (Async.RunSynchronously(queryAPI <? getQuery id "subscribedTo" null))
                    let tweetsCount = tweetsSubscribedTo.Tweets.Count
                    let mutable retweetFraction = (int) (Math.Ceiling(float tweetsCount * 0.3))// making 30% retweets from tweets fetched by query of subscribed to
                    let temp = retweetFraction
                    while tweetsCount <> 0 && retweetFraction <> 0 do
                        let tweetIdToRetweet = tweetsSubscribedTo.Tweets.[rand.Next(tweetsCount)].Id
                        startTime <- DateTime.Now
                        response <- (Async.RunSynchronously(retweetAPI <? getRetweet id tweetIdToRetweet))
                        responseTime <- responseTime + (DateTime.Now - startTime)   
                        printfn "%A Response: %A" service response 
                        retweetFraction <- retweetFraction - 1
                    averageResponseTime.[service] <- averageResponseTime.[service] + responseTime 
                    numberOfRequests.[service] <- numberOfRequests.[service] + temp
                | _ ->
                    return! loop()
        | _ ->
            return! loop()
        return! loop()
    }
    loop()

let simulatorParentActor (mailbox : Actor<'a>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with 
        | "start" ->
            let mutable numRequests = 100
            for client in clients do
                client <! "register"
            for client in clients do
                client <! "logIn"
                client <! "tweet"
            while numRequests <> 0 do
                clients.Item(rand.Next(numClients)) <! services.Item(rand.Next(totalServices))
                numRequests <- numRequests - 1
            mailbox.Sender() <! "Done"
        | "stop" ->
            Thread.Sleep 1000
            for client in clients do
                client <! PoisonPill.Instance  
                // client <? "stop" |> ignore
            // Thread.Sleep 10000
            // for service in services do 
            //     printfn "The average response Time for %A is %A %A %A" service (averageResponseTime.[service].TotalMilliseconds / float numberOfRequests.[service]) averageResponseTime.[service] numberOfRequests.[service]
            // terminate <- true       
            mailbox.Self.GracefulStop |> ignore
            mailbox.Sender() <! "Done"
        | _ ->
            ()
        return! loop()
    }
    loop()

let simulatorPerformanceActor (mailbox : Actor<'a>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with 
        | "start" ->
            let mutable startTime = DateTime.Now
            for i in [0 .. numClients - 1] do
                (Async.RunSynchronously(registerAPI <? getCredentials (i |> string) (i |> string))) |> ignore
            averageResponseTime.["register"] <- (DateTime.Now - startTime)
            numberOfRequests.["register"] <- numClients
            startTime <- DateTime.Now
            for i in [0 .. numClients - 1] do
                (Async.RunSynchronously(logInAPI <? getCredentials (i |> string) (i |> string)))|> ignore
            averageResponseTime.["logIn"] <- (DateTime.Now - startTime)
            numberOfRequests.["logIn"] <- numClients
            startTime <- DateTime.Now
            let mutable totalRequest = 0
            for i in [0 .. numClients - 1] do
                let numOfTweets = int (Math.Ceiling((zipfDistribution.Probability (int i + 1) * float numClients)))
                totalRequest <- totalRequest + numOfTweets
                for i in 1 ..numOfTweets do
                    (Async.RunSynchronously(tweetAPI <? getTweetInfo (string i) ("tweet of " + (hashTags.Item(rand.Next(hashTagsCount))) + " and mentioning @" + (string (rand.Next(numClients)))))) |> ignore
            averageResponseTime.["tweet"] <- (DateTime.Now - startTime)
            numberOfRequests.["tweet"] <- totalRequest
            startTime <- DateTime.Now
            for i in [0 .. numClients - 1] do
                for j in Math.Max(i - 5, 0) .. Math.Min(i + 5, 0) do
                    (Async.RunSynchronously(subscribeAPI <? getFollow (string i) (string j))) |> ignore
            averageResponseTime.["subscribe"] <- (DateTime.Now - startTime)
            numberOfRequests.["subscribe"] <- numClients * 10
            startTime <- DateTime.Now
            for i in [0 .. numClients - 1] do
                (Async.RunSynchronously(queryAPI <? getQuery (string i) "subscribedTo" null)) |> ignore
            averageResponseTime.["querySubscribedTo"] <- (DateTime.Now - startTime)
            numberOfRequests.["querySubscribedTo"] <- numClients
            startTime <- DateTime.Now
            for i in [0 .. numClients - 1] do
                (Async.RunSynchronously(queryAPI <? getQuery (string i) "hashTag" (hashTags.Item(rand.Next(hashTagsCount))))) |> ignore
            averageResponseTime.["queryHashTag"] <- (DateTime.Now - startTime)
            numberOfRequests.["queryHashTag"] <- numClients
            startTime <- DateTime.Now
            for i in [0 .. numClients - 1] do
                (Async.RunSynchronously(queryAPI <? getQuery (string i) "mention" null)) |> ignore
            averageResponseTime.["queryMention"] <- (DateTime.Now - startTime)
            numberOfRequests.["queryMention"] <- numClients   
            startTime <- DateTime.Now
            let mutable totalTime = startTime - startTime
            for i in [0 .. numClients - 1] do
                let tweetId = Async.RunSynchronously(echoAPI <? "getTweetId")
                startTime <- DateTime.Now
                (Async.RunSynchronously(retweetAPI <? getRetweet (string i) tweetId)) |> ignore
                totalTime <- totalTime + (DateTime.Now - startTime)
            averageResponseTime.["retweet"] <- totalTime
            numberOfRequests.["retweet"] <- numClients
            mailbox.Sender() <! "Done"
        | _ -> 
            ()
        return! loop()
    }
    loop()

let initialize() =  

    for service in services do
        let time = DateTime.Now
        averageResponseTime.TryAdd(service, time - time) |> ignore
        numberOfRequests.TryAdd(service, 0) |> ignore

    for i in 0 .. numClients - 1 do
        clients.Add(spawn system ("simulatorActor" + string(i)) (simulatorActor (string(i))))
        // clients.Item(i) <! "register"
    
    for i in 0 .. numClients - 1 do
        subscribersList.[i] <- new List<int>()

    let mutable first = 0
    let mutable last = 0
    let mutable temp = 0
    for userId in 0 .. numClients - 1 do
        let subscribersByZipfDistribution = int (Math.Ceiling((zipfDistribution.Probability (userId + 1)) * float numClients))
        first <- last
        temp <- first + subscribersByZipfDistribution
        last <- Math.Min((first + subscribersByZipfDistribution), (numClients - 1))
        for j in first .. Math.Min(numClients - 1, last) do
            if j <> userId then
                subscribersList.[j].Add(userId)
        if temp >= numClients then
            last <- temp % numClients
            for j in 0 .. Math.Min(numClients - 1, last) do
                subscribersList.[j].Add(userId)

let start() = 
    let simulator = spawn system "simulatorParentActor" simulatorParentActor
    Async.RunSynchronously(simulator <? "start")
    // while not terminate do
    //     printfn "here"
    Thread.Sleep 20000
    simulator <! "stop"
    Thread.Sleep numClients
    
// for i in 1 .. 20 do
//     let c = spawn system ("actor" + string(i)) (clientActor (string(i)))
//     c <! "start"  

// let timer = new Timers.Timer(5000.)
// let event = Async.AwaitEvent (timer.Elapsed) |> Async.Ignore

// printfn "%A" DateTime.Now
// timer.Start()
// printfn "%A" "A-OK"
// while true do
//     Async.RunSynchronously event
//     printfn "%A" DateTime.Now

// for i in 1 .. 30 do
//     printfn "%A" (int(100.0 * (zipfDistribution.Probability (i))))


// let beginSimulator() = 
//     for i in 1 .. numClients do
//         clients.Add(spawn system ("clientActor" + string(i)) (clientActor (string(i))))
//     let c = spawn system "clientParent" clientParentActor

//     c <! "registerLogin"
//     Async.Sleep 5000 |> ignore  
//     c <! "getSubscribersByZipf"
//     c <! "doTweetsWithIntervals"
    // c <! "queryAndRetweet"

// let test() = 
//     for i in 1 .. numClients do
//         clients.Add(spawn system ("clientActor" + string(i)) (clientActor (string(i))))
//     let c = spawn system "clientParent" clientParentActor

//     c <! "start"

(Async.RunSynchronously(registerAPI <? getCredentials "0" "0")) |> ignore
// beginSimulator()
// test()
initialize()
// start()

//use this for actual
// let simulator = spawn system "simulatorParentActor" simulatorParentActor
// Async.RunSynchronously(simulator <? "start")
// Async.RunSynchronously(simulator <? "stop")

let simulatorPerformanceActorRef = spawn system "simulatorPerformanceActor" simulatorPerformanceActor
Async.RunSynchronously(simulatorPerformanceActorRef <? "start")



Thread.Sleep 15000
for service in services do 
    printfn "The average response Time for %A is %A %A %A" service (averageResponseTime.[service].TotalMilliseconds / float numberOfRequests.[service]) averageResponseTime.[service] numberOfRequests.[service]

printfn "completed" 
// Console.ReadLine() |> ignore

// dotnet fsi --langversion:preview Simulator.fsx