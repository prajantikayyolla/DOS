#r ".\\bin\\Debug\\netcoreapp3.1\\Twitter.dll"

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
registerTable.Columns.Add(new DataColumn("UserId", typeof<string>))
registerTable.Columns.Add(new DataColumn("Password", typeof<string>))
registerTable.Columns.Add(new DataColumn("Status", typeof<string>))
registerTable.PrimaryKey <- [|registerTable.Columns.["UserId"]|]
dataSet.Tables.Add registerTable

let tweetTable = new DataTable("tweetDB")
tweetTable.Columns.Add(new DataColumn("TweetId", typeof<string>))
tweetTable.Columns.Add(new DataColumn("UserId", typeof<string>))
tweetTable.Columns.Add(new DataColumn("Content", typeof<string>))
tweetTable.Columns.Add(new DataColumn("TimeStamp", typeof<int64>))
tweetTable.Columns.Add(new DataColumn("IsRetweetOf", typeof<string>))
// tweetTable.PrimaryKey <- [|tweetTable.Columns.["TweetId"];tweetTable.Columns.["UserId"] |]
let (pk : DataColumn[]) = [|tweetTable.Columns.["TweetId"]; tweetTable.Columns.["UserId"]|]
let (uq : UniqueConstraint) = UniqueConstraint pk  
// tweetTable.Constraints.Add uq
dataSet.Tables.Add tweetTable

let hashTagMentionTable = new DataTable("hashTagMentionDB")
hashTagMentionTable.Columns.Add(new DataColumn("HashTagMention", typeof<string>))
hashTagMentionTable.Columns.Add(new DataColumn("TweetId", typeof<string>))
hashTagMentionTable.PrimaryKey <- [|hashTagMentionTable.Columns.["HashTagMention"]; hashTagMentionTable.Columns.["TweetId"]|]
dataSet.Tables.Add hashTagMentionTable

let followingFollowersTable = new DataTable("followingFollowersDB")
followingFollowersTable.Columns.Add(new DataColumn("UserId", typeof<string>))
followingFollowersTable.Columns.Add(new DataColumn("FollowingId", typeof<string>))
followingFollowersTable.PrimaryKey <- [|followingFollowersTable.Columns.["UserId"]; followingFollowersTable.Columns.["FollowingId"]|]
dataSet.Tables.Add followingFollowersTable

let registerTweetRelation = DataRelation("tweetRegister", registerTable.Columns.["UserId"], tweetTable.Columns.["UserId"])
dataSet.Relations.Add(registerTweetRelation)
let tweetHashTagMentionRelation = DataRelation("tweetHashTagMention", tweetTable.Columns.["TweetId"], hashTagMentionTable.Columns.["TweetId"])
dataSet.Relations.Add(tweetHashTagMentionRelation)
let registerFollowingFollowersRelation = DataRelation("registerFollowingFollowers", registerTable.Columns.["UserId"], followingFollowersTable.Columns.["UserId"])
dataSet.Relations.Add(registerFollowingFollowersRelation)
let registerFollowingFollowersRelation1 = DataRelation("registerFollowingFollowers1", registerTable.Columns.["UserId"], followingFollowersTable.Columns.["FollowingId"])
dataSet.Relations.Add(registerFollowingFollowersRelation1)

let defaultTweet = getTweet null null null (int64 0) null

let addUser userName password db = 
    // printfn "%A %A" userName password
    match db with
    | "map" ->
        registerDB.TryAdd(userName, password) |> ignore
        registerDB.ContainsKey userName 
    | "datatables" ->
        lock(registerTable) (fun () ->
            let a = registerTable.Rows.Add(userName, password, "InActive")
            not (isNull a)
            )
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
        let rows = registerTable.Select(String.Format("UserId = '{0}' AND Status = '{1}'", userName, "Active"))
        rows.Length <> 0
    | _ -> false

let logInUser userName password db = 
    match db with
    | "map" ->
        if registerDB.ContainsKey userName && registerDB.[userName] = password then
            logInDB.Add userName |> ignore
        logInDB.Contains userName
    | "datatables" ->
        let rows = registerTable.Select(String.Format("UserId = '{0}' AND Password = '{1}'", userName, password))
        for row in rows do
            row.[2] <- "Active"
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

let addTweet (tweet : Tweet) db = 
    match db with
    | "map" ->
        tweetDB.TryAdd(tweet.Id, tweet) |> ignore
    | "datatables" ->
        lock(tweetTable) (fun () ->
        tweetTable.Rows.Add(tweet.Id, tweet.UserId, tweet.Content, tweet.TimeStamp, tweet.IsRetweetOf) |> ignore
        )        
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
        let rows = followingFollowersTable.Select(String.Format("UserId = '{0}' AND FollowingId = '{1}'", follow.UserId, follow.FollowId))
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
        let rows = followingFollowersTable.Select(String.Format("UserId = '{0}' AND FollowingId = '{1}'", follow.UserId, follow.FollowId))
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

let addRetweet (tweet : Tweet) db = 
    match db with
    | "map" ->
        tweetDB.TryAdd(tweet.Id, tweet) |> ignore
    | "datatables" ->
        lock(tweetTable) (fun () -> 
        tweetTable.Rows.Add(tweet.Id, tweet.UserId, tweet.Content, tweet.TimeStamp, tweet.IsRetweetOf) |> ignore
        )
    | _ -> ()

let getContent tweetId db = 
    match db with
    | "map" ->
        if tweetDB.ContainsKey tweetId then
            tweetDB.GetOrAdd(tweetId, defaultTweet).Content//********check this
        else 
            String.Empty
    | "datatables" ->
        let rows = tweetTable.Select(String.Format("TweetId = '{0}'", tweetId))
        if rows.Length = 0 then
            String.Empty
        else
            rows.[0].["Content"] |> string//*****************check this
    | _ -> String.Empty

let tweetIdExists tweetId db = 
    match db with
    | "map" ->
        tweetDB.ContainsKey tweetId
    | "datatables" ->
        let rows = tweetTable.Select(String.Format("TweetId = '{0}'", tweetId))
        rows.Length <> 0
    | _ -> false

let getTweetsByMention (query : Query) db = 
    match db with
    | "map" ->
        let tweetsByMention = mentionDB.GetOrAdd("@" + query.UserId, new List<String>())
        let tweets = new List<Tweet>()
        //All tweets
        for tweetId in tweetsByMention do 
            tweets.Add(tweetDB.[tweetId])
        // last 10
        // for i in Math.Max(0, (tweetsByMention.Count - 10)) .. (tweetsByMention.Count - 1) do
        //     tweets.Add(tweetDB.[tweetsByMention.Item(i)])
        tweets
    | "datatables" ->
        let tweetsByMention = hashTagMentionTable.Select(String.Format("HashTagMention = '{0}'", "@" + query.UserId))
        let tweets = new List<Tweet>()
        for row in tweetsByMention do 
            let tweetRow = tweetTable.Select(String.Format("TweetId = '{0}' AND IsRetweetOf = '{1}'", row.["TweetId"], false))
            for row in tweetRow do
                tweets.Add (getTweet (string(row.["TweetId"])) (string(row.["UserId"])) (string(row.["Content"])) (int64 (string (row.["TimeStamp"]))) ((string row.["IsRetweetOf"])))
        tweets
    | _ -> new List<Tweet>()

let getTweetsBySubcribedTo (query : Query) db = 
    match db with
    | "map" ->
        let tweets = new List<Tweet>()
        for user in followingDB.GetOrAdd(query.UserId, new HashSet<string>()) do
            // All tweets
            for tweet in userTweetsDB.GetOrAdd(user, new List<string>()) do
                tweets.Add tweetDB.[tweet]
            //last tweet
            // let userTweets = userTweetsDB.GetOrAdd(user, new List<string>())
            // if userTweets.Count <> 0 then
            //     tweets.Add(tweetDB.[userTweets.Item(userTweets.Count - 1)])
        tweets
    | "datatables" ->
        let tweets = new List<Tweet>()
        for user in followingFollowersTable.Select(String.Format("UserId = '{0}'", query.UserId)) do
            for row in tweetTable.Select(String.Format("UserId = '{0}'", user.["FollowingId"])) do
                tweets.Add (getTweet (string(row.["TweetId"])) (string(row.["UserId"])) (string(row.["Content"])) (int64 (string (row.["TimeStamp"]))) ((string row.["IsRetweetOf"])))
        tweets
    | _ -> new List<Tweet>()

let getTweetsByHashTag (query : Query) db = 
    match db with
    | "map" ->
        let tweetsByHashTag = hashTagDB.GetOrAdd(query.QueryContent, new List<string>())
        let tweets = new List<Tweet>()
        //All tweets
        for tweetId in tweetsByHashTag do 
            tweets.Add(tweetDB.[tweetId])
        // last 10
        // for i in Math.Max(0, (tweetsByHashTag.Count - 10)) .. (tweetsByHashTag.Count - 1) do
        //     tweets.Add(tweetDB.[tweetsByHashTag.Item(i)])

        tweets
    | "datatables" ->
        let tweetsByHashTag = hashTagMentionTable.Select(String.Format("HashTagMention = '{0}'", query.QueryContent))
        let tweets = new List<Tweet>()
        for row in tweetsByHashTag do 
            let tweetRow = tweetTable.Select(String.Format("TweetId = '{0}' AND IsRetweetOf = '{1}'", row.["TweetId"], false))
            for row in tweetRow do
                tweets.Add (getTweet (string(row.["TweetId"])) (string(row.["UserId"])) (string(row.["Content"])) (int64 (string (row.["TimeStamp"]))) ((string row.["IsRetweetOf"])))
        tweets
    | _ -> new List<Tweet>()

let logOut userId db = 
    match db with
    | "map" ->
        logInDB.Remove userId |> ignore
        not(logInDB.Contains userId)
    | "datatables" ->
        let rows = registerTable.Select(String.Format("UserId = '{0}'", userId))
        for row in rows do
            row.[2] <- "InActive"
        let checkedIfLoggedOut = registerTable.Select(String.Format("UserId = '{0}'AND Status = '{1}'", userId, "InActive"))
        checkedIfLoggedOut.Length <> 0
    | _ -> false

let echo () = 
    // let rows = tweetTable.Rows
    // rows.[0].["TweetId"] |> string
    let rand = Random()
    let vale = List tweetDB.Keys
    vale.Item(rand.Next(vale.Count))

