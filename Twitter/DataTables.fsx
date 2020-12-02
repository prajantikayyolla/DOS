#r "C:\\Prajan\\Sem-3\\DOS\\DOS\\Twitter\\bin\\Debug\\netcoreapp3.1\\Twitter.dll"

open System
open System.Data
open System.Collections.Concurrent
open System.Collections.Generic
open DataTypes

let registerDB = new ConcurrentDictionary<string, string>()

let logInDB = new HashSet<string>()

let hashTagDB = new ConcurrentDictionary<string, List<string>>()

let mentionDB = new ConcurrentDictionary<string, List<string>>()

let tweetDB = new ConcurrentDictionary<string, DataTypes.Tweet>()

let userTweetsDB = new ConcurrentDictionary<string, List<string>>()

let followingDB = new ConcurrentDictionary<string, HashSet<string>>()

let followersDB = new ConcurrentDictionary<string, HashSet<string>>()

let stringType = System.Type.GetType("System.String")
let intType = System.Type.GetType("System.Int64")
let booleanType = System.Type.GetType("System.Boolean")
let dateTimeType = System.Type.GetType("System.DateTime")
let dataSet = new DataSet()

let registerTable = new DataTable("registerDB")
registerTable.Columns.Add(new DataColumn("UserId", stringType))
registerTable.Columns.Add(new DataColumn("Password", stringType))
registerTable.Columns.Add(new DataColumn("Status", stringType))
registerTable.PrimaryKey <- [|registerTable.Columns.["UserId"]|]
dataSet.Tables.Add registerTable

let tweetTable = new DataTable("tweetDB")
tweetTable.Columns.Add(new DataColumn("TweetId", stringType))
tweetTable.Columns.Add(new DataColumn("UserId", stringType))
tweetTable.Columns.Add(new DataColumn("Content", stringType))
tweetTable.Columns.Add(new DataColumn("TimeStamp", dateTimeType))
tweetTable.Columns.Add(new DataColumn("IsRetweet", booleanType))
tweetTable.PrimaryKey <- [|registerTable.Columns.["TweetId"]|]
dataSet.Tables.Add tweetTable

let hashTagMentionTable = new DataTable("hashTagMentionDB")
hashTagMentionTable.Columns.Add(new DataColumn("HashTagMention", stringType))
hashTagMentionTable.Columns.Add(new DataColumn("TweetId", stringType))
hashTagMentionTable.PrimaryKey <- [|hashTagMentionTable.Columns.["HashTagMention"]; hashTagMentionTable.Columns.["TweetId"]|]
dataSet.Tables.Add hashTagMentionTable

let followingFollowersTable = new DataTable("followingFollowersDB")
followingFollowersTable.Columns.Add(new DataColumn("UserId", stringType))
followingFollowersTable.Columns.Add(new DataColumn("FollowingId", stringType))
followingFollowersTable.PrimaryKey <- [|followingFollowersTable.Columns.["UserId"]; followingFollowersTable.Columns.["FollowingId"]|]
dataSet.Tables.Add followingFollowersTable

let addUser userName password db = 
    // printfn "%A %A" userName password
    match db with
    | "map" ->
        registerDB.TryAdd(userName, password) |> ignore
        registerDB.ContainsKey userName 
    | "datatables" ->
        let a = registerTable.Rows.Add(userName, password, "Inactive")
        not (isNull a)
    | _ -> false

let registeredUser userName password db = 
    match db with
    | "map" ->
        registerDB.ContainsKey userName
    | "datatables" ->
        let rows = registerTable.Select(String.Format("UserId = '{0}' AND Password = '{1}'", userName, password))
        rows.Length <> 0
    | _ -> false

let loggedInUser userName db = 
    match db with
    | "map" ->
        logInDB.Contains userName
    | "datatables" ->
        let rows = registerTable.Select(String.Format("UserId = '{0}' AND Status = '{2}'", userName, "Active"))
        rows.Length <> 0
    | _ -> false

let logInUser userName password db = 
    match db with
    | "map" ->
        logInDB.Add userName |> ignore
        logInDB.Contains userName
    | "datatables" ->
        let rows = registerTable.Select(String.Format("UserId = '{0}' AND Password = '{1}'", userName, password))
        for row in rows do
            row.["Status"] <- "Active"
        let checkedIfLoggedIn = registerTable.Select(String.Format("UserId = '{0}' AND Password = '{1}' AND Status = '{2}'", userName, password, "Active"))
        checkedIfLoggedIn.Length <> 0
    | _ -> false

let addHashTagMention hashTagMention isHashTag tweetId db = 
    match db with
    | "map" ->
        match isHashTag with
        | true ->
            hashTagDB.GetOrAdd(hashTagMention, new List<string>()).Add tweetId |> ignore
        | false->
            mentionDB.GetOrAdd(hashTagMention, new List<string>()).Add tweetId |> ignore
    | "datatables" ->
        hashTagMentionTable.Rows.Add(hashTagMention, tweetId) |> ignore
    | _ -> ()

let addTweet tweet db = 
    match db with
    | "map" ->
        tweetDB.TryAdd(tweet.Id, tweet) |> ignore
    | "datatables" ->
        tweetTable.Rows.Add(tweet.Id, tweet.UserId, tweet.Content, tweet.TimeStamp, tweet.IsRetweet) |> ignore
    | _ -> ()

let addTweetToUser (tweet : Tweet) db =
    match db with
    | "map" ->
        userTweetsDB.GetOrAdd(tweet.UserId, new List<string>()).Add tweet.Id |> ignore
    | "datatables" ->
        ()
    | _ -> ()

let subscribedUser (follow : Follow) db =
    match db with
    | "map" ->
        followingDB.GetOrAdd(follow.UserId, new HashSet<string>()).Contains follow.FollowId
    | "datatables" ->
        let rows = followingFollowersTable.Select(String.Format("UserId = '{0}' AND FollowingId = '{2}'", follow.UserId, follow.FollowId))
        rows.Length <> 0
    | _ -> false

let addSubscriber (follow : Follow) db =
    match db with
    | "map" ->
        followingDB.GetOrAdd(follow.UserId, new HashSet<string>()).Add follow.FollowId |> ignore
        followersDB.GetOrAdd(follow.FollowId, new HashSet<string>()).Add follow.UserId |> ignore
        followingDB.[follow.UserId].Contains follow.FollowId && followersDB.[follow.FollowId].Contains follow.UserId 
    | "datatables" ->
        followingFollowersTable.Rows.Add(follow.UserId, follow.FollowId) |> ignore
        let rows = followingFollowersTable.Select(String.Format("UserId = '{0}' AND FollowingId = '{2}'", follow.UserId, follow.FollowId))
        rows.Length <> 0
    | _ -> false

let bothRegisteredUser (follow : Follow) db = 
    match db with
    | "map" ->
        registerDB.ContainsKey follow.UserId && registerDB.ContainsKey follow.FollowId
    | "datatables" ->
        let rows = registerTable.Select(String.Format("UserId = '{0}' OR UserId = '{1}'", follow.UserId, follow.FollowId))
        rows.Length = 2
    | _ -> false

let addRetweet tweet db = 
    match db with
    | "map" ->
        ()
    | "datatables" ->
        tweetTable.Rows.Add(tweet.Id, tweet.UserId, tweet.Content, tweet.TimeStamp, tweet.IsRetweet) |> ignore
    | _ -> ()

let echo = [|followingDB; followersDB|]
