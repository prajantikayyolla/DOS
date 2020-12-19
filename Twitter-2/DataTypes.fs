open System

type Credentials = {
    UserName : string
    Password : string
    TimeStamp : int64
}

let getCredentials username password = {
    UserName = username; Password = password; TimeStamp = DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds()
}

type TweetInfo = {
    UserId : string
    Content : string
    TimeStamp : int64
}

let getTweetInfo userId content = {
    UserId = userId; Content = content; TimeStamp = DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds()
}

type Tweet = {
    Id : string
    UserId : string
    Content : string
    TimeStamp : int64
    IsRetweetOf : string
}

let getTweet id userId content timeStamp isRetweetOf = {
    Id = id; UserId = userId; Content = content; TimeStamp = timeStamp; IsRetweetOf = isRetweetOf
}

type Follow = {
    UserId : string
    FollowId : string
    TimeStamp : int64
}

let getFollow userId followId = {
    UserId = userId; FollowId = followId; TimeStamp = DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds()
}

type Retweet = {
    UserId : string
    TweetId : string
    TimeStamp : int64
}

let getRetweet userId tweetId = {
    UserId = userId; TweetId = tweetId; TimeStamp = DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds()
}

type Query = {
    UserId : string
    QueryType : string
    QueryContent : string
    TimeStamp : int64
}

let getQuery userId queryType queryContent = {
    UserId = userId; QueryType = queryType; QueryContent = queryContent; TimeStamp = DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds()
}

type Response = {
    Status : int
    Message : string
    From : string
    UserId : string
    TimeStamp : int64
}

let getResponse status message from userId timstamp = {
    Status = status; Message = message; From = from; UserId = userId; TimeStamp = timstamp
}

type QueryResponse = {
    Status : int
    Message : string
    From : string
    UserId : string
    Tweets : Collections.Generic.List<Tweet>
    TimeStamp : int64
}

let getQueryResponse status message from userId tweets timestamp = {
    Status = status; Message = message; From = from; UserId = userId; Tweets = tweets; TimeStamp = timestamp
}

type LiveTweet = {
    Id : string
    UserId : string
    Content : string
    TimeStamp : int64
    IsRetweetOf : string
}

let getLiveTweet id userId content timeStamp isRetweetOf = {
    Id = id; UserId = userId; Content = content; TimeStamp = timeStamp; IsRetweetOf = isRetweetOf
}

type StartTweeting = {
    NumberOfTweets : int
}

let getStartTweeting numberOfTweets = {NumberOfTweets = numberOfTweets}

let SUCCESS = 200
let BADREQUEST = 400
let UNAUTHORIZED = 401
let NOTACCEPTABLE = 406
let FAILED = 500
let HASHMAP = "map"
let DATATABLES = "datatables"