#r ".\\bin\\Debug\\netcoreapp3.1\\Suave.dll"
#r ".\\bin\\Debug\\netcoreapp3.1\\Twitter.dll"
#r ".\\bin\\Debug\\netcoreapp3.1\\FSharp.Json.dll"
#r ".\\bin\\Debug\\netcoreapp3.1\\Newtonsoft.Json.FSharp.dll"
#r ".\\bin\\Debug\\netcoreapp3.1\\Twitter.dll"

open System.Net.WebSockets
open System.Threading
open System
open DataTypes
open FSharp.Json
open Newtonsoft.Json
open System.Collections
open Suave

let socket = new ClientWebSocket()
let cts = new CancellationTokenSource()
let uri = Uri("ws://localhost:8080/websocket")

let connection = socket.ConnectAsync(uri, cts.Token)

while not (connection.IsCompletedSuccessfully) do
    Thread.Sleep(100)

let mutable logInStatus = false
let mutable logInStatusUpdated = false
let mutable (userId : string) = null
let mutable userPassword = null

// let cred = getCredentials "3" "3"

// let jsonCred = "POST /api/Register\n" + "Body:" + Json.serialize cred

// printfn "%A" jsonCred

// let byteCred = 
//           jsonCred
//           |> System.Text.Encoding.ASCII.GetBytes
//           |> ArraySegment

let task (socket : WebSocket) = 
    async {        
        while true do
            let rcvBytes: byte [] = Array.zeroCreate 1280
            let rcvBuffer = new ArraySegment<byte>(rcvBytes) 
            let! res = (socket.ReceiveAsync(rcvBuffer, cts.Token))
            let responseString = (UTF8.toString (rcvBuffer.ToArray())).Trim([|' '; (char) 0|])
            let splits = responseString.Split(",")
            // printfn "%A %A %A %A" splits splits.[2] splits.[4] responseString
            // printfn "%A %A" responseString (responseString.Contains("IsRetweetOf"))
            // if(responseString.Contains("Tweets")) then
            //     let data = Json.deserialize<QueryResponse> responseString
            //     printfn "\n\t\t%s" (data.From + " " + data.Message + " for " + data.UserId)
            //     let mutable res = null   
            //     for tweet in data.Tweets do
            //         res <- res + Json.serialize tweet + ","
            //     printfn "\n\t\t%s" (res.Substring(0, res.Length - 1))
            // else if(responseString.Contains("IsRetweetOf")) then
            //     let data = Json.deserialize<LiveTweet> responseString
            //     printfn "%A" data
            //     if(userId <> data.UserId) then
            //         printfn "\n\t\t%s%s%s%s" "LiveTweet: " data.Content data.UserId data.IsRetweetOf
            //         printf "\tTwitterEngine>"
            //     else
            //         printfn "\n\t\t%s%s%s" data.Content data.UserId data.IsRetweetOf
            // else
            //     let data = Json.deserialize<Response> responseString
            //     printfn "\n\t\t%s" (data.From + " " + data.Message + " for " + data.UserId)
            //     if data.From = "LogIn" then
            //         if data.Message = "SUCCESS" then
            //             logInStatus <- true
            //         logInStatusUpdated <- true
            //     else if data.From = "LogOut" then
            //         if data.Message = "SUCCESS" then
            //             logInStatus <- false





            if(splits.[2].StartsWith("\"From")) then
                printfn "\n\t\t%s" ((splits.[2].Split(":").[1]) + " " + splits.[1].Split(":").[1] + " for " + splits.[3].Split(":").[1])
                if(splits.[4].StartsWith("\"Tweets")) then
                    let startIndex = responseString.IndexOf("\"Tweets") + 9
                    let endIndex = responseString.LastIndexOf("\"TimeStamp")
                    printfn "\n\t\t%s" (responseString.Substring(startIndex, endIndex - startIndex - 1))
                else if(splits.[2].Split(":").[1] = "\"LogIn\"") then
                    if(splits.[1].Split(":").[1] = "\"SUCCESS\"") then
                        logInStatus <- true
                    logInStatusUpdated <- true
                else if(splits.[2].Split(":").[1] = "\"LogOut\"") then
                    if(splits.[1].Split(":").[1] = "\"SUCCESS\"") then
                        logInStatus <- false
                        socket.CloseAsync(WebSocketCloseStatus.Empty,"completed", cts.Token) |> ignore
            else 
                let tweet = splits.[2].Split(":").[1] + " - "
                let user = splits.[1].Split(":").[1]
                let retweetStatus = ", retweet of: " + splits.[4].Split(":").[1].[..34].Replace("}", "")                
                if(user <> ("\"" + userId + "\"")) then
                    printfn "\n\t\t%s%s%s%s" "LiveTweet: " tweet user retweetStatus
                    printf "\tTwitterEngine>"
                else
                    printfn "\n\t\t%s%s%s" tweet user retweetStatus

                // printfn "\n\t\t%s\n" ((splits.[2].Split(":").[1]) + "-" + (splits.[1].Split(":").[1]) + ", retweet of: " + (splits.[4].Split(":").[1].[..34]))
            // printfn "%s %d" splits (splits.Length)
            // Thread.Sleep 3000
    }

Async.StartImmediateAsTask(task socket)

let getBytes data requestAPI requestType = 
    let header = "RequestType: " + requestType + "\n" + "RequestAPI: " + requestAPI + "\n" + "Body: "
    let fullJson = header + Json.serialize data
    fullJson
    |> System.Text.Encoding.ASCII.GetBytes
    |> ArraySegment

let pageMargin = "+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+\n"
let homePageHeader = "\n\tSee what’s happening in the world right now.\n\tJoin Twitter Engine today.\n\t\t1. Register\n\t\t2. LogIn\n\t\t3. Exit\n\n\tSelect service: "
let registerHeader = "\tFollow your interests.\n\tHear what people are talking about.\n\tJoin the conversation.\n\n"
let registerName = "\t\tRegister\n"
let promptUserName = "\t\tUsername: "
let promptPassword = "\t\tPassword: "
let logInName = "\t\tLogIn\n"
let timelineHeader = "\tusage: twitter [option] ... [arg]\n\tOptions and arguments: \n"
let timelineTweet = "\t-tweet            \t: to make tweet, should be passed with content as argument\n"
let timelineSubscribe = "\t-subscribe        \t: to follow a user, should be passed with follower id as argument\n"
let timelineRetweet = "\t-retweet          \t: to retweet a tweet, should be passed with tweet id as argument\n"
let timelineQuerySubscribedTo = "\t-querySubscribedTo\t: to get tweets posted by followed users, no argument is required\n"
let timelineQueryHashtag = "\t-queryHashTag     \t: to get tweets by hash tag, should be passed with hash tag as argument\n"
let timelineQueryMention = "\t-queryMention     \t: to get tweets by your mention, no argument is required\n"
let timelineLogOut = "\t-logOut           \t: to logout from twitter engine, no arguments required\n"
let timelineName = "\t\tTimeline\n"

let helper option argument = 
    let mutable bytes = ArraySegment.Empty
    match option with 
    | "subscribe" ->
        bytes <- getBytes (getFollow userId argument) "Subscribe" "POST"
    | "tweet" ->
        bytes <- getBytes (getTweetInfo userId argument) "Tweet" "POST"
    | "retweet" ->
        bytes <- getBytes (getRetweet userId argument) "Retweet" "POST"
    | "querySubscribedTo" ->
        bytes <- getBytes (getQuery userId "Query-subscribedTo" argument) "Query-subscribedTo" "GET"        
    | "queryHashTag" ->
        bytes <- getBytes (getQuery userId "Query-hashTag" argument) "Query-hashTag" "GET"
    | "queryMention" ->
        bytes <- getBytes (getQuery userId "Query-mention" argument) "Query-mention" "GET"
    | "logOut" ->
        bytes <- getBytes (getCredentials userId userPassword) "LogOut" "POST"
    | _ ->
        printfn "\tInvalid command"
    if bytes <> ArraySegment.Empty then
        socket.SendAsync(bytes, WebSocketMessageType.Text, true, cts.Token) |> ignore
    else
        ()

let timeline() =
    printfn "%s" (pageMargin + timelineName)
    printfn "%s" (timelineHeader + timelineSubscribe + timelineTweet + timelineRetweet + timelineQuerySubscribedTo + timelineQueryHashtag + timelineQueryMention + timelineLogOut)
    while logInStatus do
        printf "\tTwitterEngine>"
        let line = Console.ReadLine()
        let trimmed = line.Replace("twitter", "").Trim().[1..]
        let splits = trimmed.Split(" ")
        let option = splits.[0]
        let mutable argument = null
        // printfn "%A %A" splits option
        if splits.Length >= 2 then 
            argument <- trimmed.Replace(option, "").Trim()
        if (option = "subscribe" || option = "tweet" || option = "retweet" || option = "queryHashTag") then
            if splits.Length < 2 then
                printfn "\tInvalid command"
                ()
            else 
                helper option argument
        else
            helper option argument  
        Thread.Sleep 500  


let register() =
    printfn "%s" (pageMargin + registerHeader + registerName)
    printf "%s" promptUserName
    let username = Console.ReadLine()
    printf "%s" promptPassword
    let password = Console.ReadLine()
    let bytes = getBytes (getCredentials username password) "Register" "POST"
    socket.SendAsync(bytes, WebSocketMessageType.Text, true, cts.Token)

let logIn() = 
    logInStatusUpdated <- false
    printfn "%s" pageMargin
    printfn "%s" logInName
    printf "%s" promptUserName
    let username = Console.ReadLine()
    printf "%s" promptPassword
    let password = Console.ReadLine()
    let bytes = getBytes (getCredentials username password) "LogIn" "POST"
    socket.SendAsync(bytes, WebSocketMessageType.Text, true, cts.Token) |> ignore
    while not logInStatusUpdated do
        Thread.Sleep 500
    if logInStatus then
        userId <- username
        userPassword <- password
        timeline()

let twitterEngine = "+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|W e l c o m e   t o   T w i t t e r   E n g i n e ! ! !|
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+"
printfn "%s" twitterEngine


while true do       
    printf "%s" pageMargin
    printf "%s" homePageHeader
    let line = Console.ReadLine()
    match line with 
    | "1" ->
        register() |> ignore
    | "2" ->
        logIn() |> ignore
    | "3" ->
        Environment.Exit(0)
    | _ ->
        ()
    Thread.Sleep 500
// let response = socket.SendAsync(byteCred, WebSocketMessageType.Text, true, cts.Token)

// Console.ReadLine() |> ignore

// let a = ArraySegment<byte>[|byte('1'); byte('2'); byte('3')|]
// let aaa = socket.SendAsync (a, WebSocketMessageType.Text, true, cts.Token)
// if aaa.IsCompletedSuccessfully then
//     socket.CloseAsync (WebSocketCloseStatus.Empty, "", cts.Token) |> ignore