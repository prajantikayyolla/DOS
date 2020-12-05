open System

type Credentials = {
    UserName : string;
    Password : string
}

let getCredentials username password = {
    UserName = username; Password = password
}

type TweetInfo = {
    UserId : string
    Content : string
}

let getTweetInfo userId content = {
    UserId = userId; Content = content
}

type Tweet = {
    Id : string
    UserId : string
    Content : string
    TimeStamp : string
    IsRetweetOf : string
}

let getTweet id userId content timeStamp isRetweetOf = {
    Id = id; UserId = userId; Content = content; TimeStamp = timeStamp; IsRetweetOf = isRetweetOf
}

type Follow = {
    UserId : string
    FollowId : string
}

let getFollow userId followId = {
    UserId = userId; FollowId = followId
}

type Retweet = {
    UserId : string
    TweetId : string
}

let getRetweet userId tweetId = {
    UserId = userId; TweetId = tweetId
}

type Query = {
    UserId : string
    QueryType : string
    QueryContent : string
}

let getQuery userId queryType queryContent = {
    UserId = userId; QueryType = queryType; QueryContent = queryContent
}

type Response = {
    Status : int
    Message : string
    From : string
    UserId : string
}

let getResponse status message from userId = {
    Status = status; Message = message; From = from; UserId = userId
}

type QueryResponse = {
    Status : int
    Message : string
    From : string
    UserId : string
    Tweets : Collections.Generic.List<Tweet>
}

let getQueryResponse status message from userId tweets = {
    Status = status; Message = message; From = from; UserId = userId; Tweets = tweets
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