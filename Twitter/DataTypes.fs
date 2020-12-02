open System

type Credentials = {
    UserName : string;
    Password : string
}

let getCredentials username password = {
    UserName = username; Password = password
}

type Tweet = {
    Id : string
    UserId : string
    Content : string
    TimeStamp : DateTime
    IsRetweet : string
}

let getTweet id userId content timeStamp isRetweet = {
    Id = id; UserId = userId; Content = content; TimeStamp = timeStamp; IsRetweet = isRetweet
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

let SUCCESS = 200
let BADREQUEST = 400
let UNAUTHORIZED = 401
let NOTACCEPTABLE = 406
let FAILED = 500
let HASHMAP = "map"
let DATATABLES = "datatables"