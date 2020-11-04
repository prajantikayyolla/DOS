#if INTERACTIVE
#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#endif

open System
open System.Text
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Collections.Generic
open System.Security.Cryptography

type Record =
   { Msg : string;
     NodeId : string;
     RoutingTable : string[,];
     Hops : int;
     InterNodes : List<String> }

type State = {
    NodeId : string;
    RoutingTable : string [,];
    LeafSetSmall : string[];
    LeafSetLarge : string[];
    TimeStamp : string;
}

type Leaf = {
    NodeId : string
}

type RouteMessage = {
    NodeId : string;
    Message : string;
    Hops : int
}

let system = ActorSystem.Create("System")
let pastryActors = new List<IActorRef>()
let joinedActors = new List<string>()
let completedActors = new List<string>()
let mutable numNodes = 0
let mutable numRequest = 0
let b = 3
let mutable rows = 0
let mutable columns = 0
let u8 = Encoding.UTF8;
let hash = MD5.Create()
let mutable pastryMap = Map.empty
let mutable pastryMapHashCode = Map.empty<string, IActorRef>
let rand = System.Random()
let mutable totalHops = 0
let mutable totalRequest = 0
let maxHash = bigint.Parse("7777777777777777")
let mutable requestHops = 0
let mutable requestCompleted = 0

let getRecord msg nodeId routingTable hops interNodes = 
    {Msg = msg; NodeId = nodeId; RoutingTable = routingTable; Hops = hops; InterNodes = interNodes}

let getState nodeId routingTable leafSetSmall leafSetLarge timestamp  = 
    {NodeId = nodeId; RoutingTable = routingTable; LeafSetSmall = leafSetSmall; LeafSetLarge = leafSetLarge; TimeStamp = timestamp}

let getLeaf nodeId = 
    {NodeId = nodeId}

let getRouteMessage nodeId message hops = 
    {NodeId = nodeId; Message = message; Hops = hops}

let getValueAtIndex (nodeId : string) index = 
    let ch = nodeId.[index] |> char
    if (ch |> Char.IsDigit) then int ch - int '0'
    else int ch - int 'A' + 10

let setInitialRoutingtable (routingTable : string [,]) (nodeId : string) =
    // printfn "%A in initial" nodeId
    for i in 0 .. rows - 1 do
        let j = getValueAtIndex nodeId i
        Array2D.set routingTable i j nodeId

let getProximityNode () = 
    joinedActors.Item(rand.Next(joinedActors.Count))

let commonPrefix destNodeId (srcNodeId : string) =
    let mutable exit = false
    let mutable i = 0
    while not exit && i < (destNodeId |> String.length) do 
        if destNodeId.[i] <> srcNodeId.[i] then
            exit <- true
        else
            i <- i + 1
    i    

let setRoutingTable destNodeId (destRoutingTable : string[,]) srcNodeId (srcRoutingTable : string[,]) hops rowsInitialized = 
    // printfn "%A %A %A %A %A %A" destNodeId srcNodeId (destRoutingTable.GetLength(0)) (destRoutingTable.GetLength(1)) (srcRoutingTable.GetLength(0)) (srcRoutingTable.GetLength(1))
    let l = commonPrefix destNodeId srcNodeId
    for i in rowsInitialized .. min (rows - 1) l do
        // printfn "%A in here" i
        for j in 0 .. (columns - 1) do 
            if(srcRoutingTable.[i, j] <> "null" && destRoutingTable.[i, j] <> destNodeId) then
                destRoutingTable.[i, j] <- srcRoutingTable.[i, j]
    l

let findNearestInLeaf (leafSet : string[]) (nodeId : string) =
    let mutable min = maxHash
    let mutable nearestNode = "null"
    let mutable distance = bigint.Zero
    for i in 0 .. leafSet.Length - 1 do
        distance <- bigint.Abs(bigint.Subtract(leafSet.[i] |> bigint.Parse, nodeId |> bigint.Parse))
        if(bigint.Compare(distance, min) < 0) then
            min <- distance
            nearestNode <- leafSet.[i]
    nearestNode

let isInLeafSet leafSetSmall leafSetLarge nodeId baseNodeId = 
    let leafSet = Array.except ([|"null"|] |> Array.toSeq) (Array.append leafSetSmall leafSetLarge)
    if(leafSet.Length = 0) then
        "null"
    else if(leafSet |> Array.min <= nodeId && nodeId <= (leafSet |> Array.max)) then
        let n = findNearestInLeaf leafSet nodeId
        if(n <> "null" && (commonPrefix n nodeId) > (commonPrefix baseNodeId nodeId)) then
            n
        else
            baseNodeId
    else 
        "null"   

let getIActorRef nodeId = 
    pastryMapHashCode.GetValueOrDefault(nodeId)

let isInRoutingTable (routingTable : string [,]) destNodeId srcNodeId = 
    let row = min (commonPrefix destNodeId srcNodeId) (rows - 1)
    let column = getValueAtIndex destNodeId row
    // if(routingTable.[row, column] = destNodeId) then
    //     destNodeId
    if(routingTable.[row, column] <> "null") then
        routingTable.[row, column]
    else 
        "null"

let getNearestNode (routingTable : string [,]) leafSetSmall leafSetLarge nodeId srcNodeId = 
    let mutable minVal = maxHash
    let mutable nearestNode = "null"
    let mutable distance = bigint.Zero
    let l = min (commonPrefix nodeId srcNodeId) (rows - 1)
    let leafSet= Array.except ([|"null"|] |> Array.toSeq) (Array.append leafSetSmall leafSetLarge)

    for i in 0 .. routingTable.GetLength(0) - 1 do 
        for j in 0 .. routingTable.GetLength(1) - 1 do
            if(routingTable.[i, j] <> "null" && ((min (commonPrefix routingTable.[i, j] srcNodeId) (rows - 1)) >= l)) then                
                distance <- bigint.Abs(bigint.Subtract(routingTable.[i, j] |> bigint.Parse, nodeId |> bigint.Parse))
                // printfn "%A %A %A format" (routingTable.[i, j]) (nodeId) (distance)
                if(bigint.Compare(distance, minVal) < 0) then
                    minVal <- distance
                    nearestNode <- routingTable.[i, j]
    
    for i in 0 .. leafSet.Length - 1 do
        if(min (commonPrefix leafSet.[i] srcNodeId) (rows - 1) >= l) then
            distance <- bigint.Abs(bigint.Subtract(leafSet.[i] |> bigint.Parse, nodeId |> bigint.Parse))
            if(bigint.Compare(distance, minVal) < 0) then
                minVal <- distance
                nearestNode <- leafSet.[i]
    
    nearestNode      

let updateState (srcRoutingTable : string[,]) (destRoutingTable : string[,]) destNodeId srcNodeId = 
    let l = commonPrefix destNodeId srcNodeId
    for i in 0 .. min l (rows - 1) do
        for j in 0 .. (columns - 1) do             
            if(srcRoutingTable.[i, j] <> "null" && srcRoutingTable.[i, j] <> destNodeId) then
                if(destRoutingTable.[i, j] = "null") then
                    destRoutingTable.[i, j] <- srcRoutingTable.[i, j]
                else if(bigint.Compare(bigint.Abs(bigint.Subtract(srcRoutingTable.[i, j] |> bigint.Parse, destNodeId |> bigint.Parse)),bigint.Abs(bigint.Subtract(destRoutingTable.[i, j] |> bigint.Parse, destNodeId |> bigint.Parse))) < 0) then
                    destRoutingTable.[i, j] <- srcRoutingTable.[i, j]

let addToLeaf leaf nodeId small = 
    // printfn "in add leaf set"
    let leafSet = Array.except ([|"null"|] |> Array.toSeq) leaf
    if not (Array.exists (fun x -> x = nodeId) leafSet) then
        if(leafSet.Length < (columns / 2) - 1) then
            leaf.[Array.findIndex (fun x -> x = "null") leaf] <- nodeId
        else
            if small then
                leaf.[Array.findIndex (fun x -> x = Array.min leaf) leaf] <- nodeId
            else 
                leaf.[Array.findIndex (fun x -> x = Array.max leaf) leaf] <- nodeId

let updateLeaf (srcLeaf : string[]) srcNodeId (destLeaf : string[]) destNodeId small = 
    for i in 0 .. srcLeaf.Length - 1 do
        if srcLeaf.[i] <> "null" && srcLeaf.[i] <> destNodeId then
            if small && (bigint.Compare(bigint.Parse(srcLeaf.[i]), bigint.Parse(destNodeId)) < 0) then
                // printfn "in small leaf"
                addToLeaf destLeaf srcLeaf.[i] small
            else if not small && (bigint.Compare(bigint.Parse(srcLeaf.[i]), bigint.Parse(destNodeId)) > 0) then
                // printfn "in large leaf"
                addToLeaf destLeaf srcLeaf.[i] small

            


let pastryBehavior initialState nodeId (mailbox : Actor<'a>) =
    let routingTable = Array2D.create rows columns "null"
    let leafSetSmall = Array.create (columns / 2) "null"
    let leafSetLarge = Array.create (columns / 2) "null"
    let mutable map = Map.empty<IActorRef, string>
    let mutable lastUpdatedTime = System.DateTime.Now.ToString("HH:mm:ss.ffffff")
    let rec imp lastState =
        actor {
            let! msg = mailbox.Receive()
            
            let timestamp = System.DateTime.Now.ToString("HH:mm:ss.ffffff")
            match box msg with
            | :? string as s -> 
                        match s with 
                        | "route" -> 
                            if(lastState = numRequest) then
                                completedActors.Add(nodeId) |> ignore
                        | "join" ->
                            // printfn "%A %A" mailbox.Self (pastryMap.GetValueOrDefault(mailbox.Self))
                            // nodeId <- pastryMap.GetValueOrDefault(mailbox.Self)
                            
                            setInitialRoutingtable routingTable nodeId
                            if(joinedActors.Count <> 0) then
                                let proximityNode = getProximityNode() |> pastryMapHashCode.GetValueOrDefault
                                proximityNode <! getRecord "join" nodeId routingTable 0 (new List<String>())
                            else joinedActors.Add(nodeId)
                        | _ -> "done" |> ignore
                        return! imp (lastState + 1)
            | :? Record as r ->
                        match r.Msg with
                        | "join" ->     
                            // printfn "asking %A with routing table %A" (pastryMap.GetValueOrDefault(mailbox.Self)) (routingTable)
                            // printfn "with leaf %A" leafSetSmall
                            // printfn "with leaf %A" leafSetLarge
                            // printfn "%A" msg   

                            getIActorRef r.NodeId <! getState nodeId routingTable leafSetSmall leafSetLarge timestamp
                            getIActorRef r.NodeId <! getLeaf nodeId

                            map <- map.Add (getIActorRef r.NodeId, timestamp)

                            r.InterNodes.Add(nodeId) |> ignore

                            // if(bigint.Compare(bigint.Parse(r.NodeId), bigint.Parse(nodeId)) < 0 && r.NodeId <> nodeId) then
                            //     addToLeaf leafSetSmall r.NodeId
                            // else if(bigint.Compare(bigint.Parse(r.NodeId), bigint.Parse(nodeId)) > 0 && r.NodeId <> nodeId) then
                            //     addToLeaf leafSetLarge r.NodeId

                            let mutable node = isInLeafSet leafSetSmall leafSetLarge r.NodeId nodeId
                            let foundInLeaf = node <> "null"
                            if(node = "null") then
                                node <- isInRoutingTable routingTable r.NodeId nodeId
                                if(node = "null") then
                                    node <- getNearestNode routingTable leafSetSmall leafSetLarge r.NodeId nodeId
                            
                            // printfn "%A node is" node
                            if(node <> "null") then
                                if(node = nodeId) then
                                    getIActorRef r.NodeId <! getRecord "arrived" r.NodeId r.RoutingTable (r.Hops + 1) r.InterNodes
                                else 
                                    printfn "here"
                                    getIActorRef node <! getRecord r.Msg r.NodeId r.RoutingTable (r.Hops + 1) r.InterNodes
                            else 
                                getIActorRef r.NodeId <! getRecord "arrived" r.NodeId r.RoutingTable (r.Hops + 1) r.InterNodes

                        | "arrived" ->
                            totalHops <- totalHops + r.Hops
                            // printfn "in arrived hops: %A for %A" r.Hops r.NodeId
                            // totalRequest <- totalRequest + 1
                            joinedActors.Add(r.NodeId)
                            // for i in 0 .. r.InterNodes.Count - 1 do
                            //     getIActorRef (r.InterNodes.Item(i)) <! getState nodeId routingTable leafSetSmall leafSetLarge timestamp
                            for i in 0 .. rows - 1 do
                                for j in 0 .. columns - 1 do 
                                    if(routingTable.[i, j] <> "null") then
                                        getIActorRef routingTable.[i, j] <! getState nodeId routingTable leafSetSmall leafSetLarge (System.DateTime.MinValue.ToString("HH:mm:ss.ffffff"))

                        | _ ->
                            "done" |> ignore
                        return! imp (lastState + 1)
            | :? State as s -> 
                // printfn "zzzzzzzzzzzz %A %A" s.LeafSetSmall s.LeafSetLarge
                if(System.DateTime.MinValue.ToString("HH:mm:ss.ffffff") = s.TimeStamp) then
                    updateState s.RoutingTable routingTable nodeId s.NodeId
                    updateLeaf s.LeafSetSmall s.NodeId leafSetSmall nodeId true
                    updateLeaf s.LeafSetLarge s.NodeId leafSetLarge nodeId false
                    lastUpdatedTime <- s.TimeStamp
                else
                    if map.ContainsKey(mailbox.Sender()) then
                        if(System.DateTime.Compare(System.DateTime.Parse (map.GetValueOrDefault (mailbox.Sender())), System.DateTime.Parse s.TimeStamp) < 0) then
                            updateState s.RoutingTable routingTable nodeId s.NodeId
                            updateLeaf s.LeafSetSmall s.NodeId leafSetSmall nodeId true
                            updateLeaf s.LeafSetLarge s.NodeId leafSetLarge nodeId false
                            lastUpdatedTime <- s.TimeStamp
                        if(System.DateTime.Compare(System.DateTime.Parse (map.GetValueOrDefault (mailbox.Sender())), System.DateTime.Parse lastUpdatedTime) < 0) then
                            mailbox.Sender() <! getState nodeId routingTable leafSetSmall leafSetLarge  (System.DateTime.MinValue.ToString("HH:mm:ss.ffffff"))
                    else 
                        updateState s.RoutingTable routingTable nodeId s.NodeId
                        updateLeaf s.LeafSetSmall s.NodeId leafSetSmall nodeId true
                        updateLeaf s.LeafSetLarge s.NodeId leafSetLarge nodeId false
                        lastUpdatedTime <- s.TimeStamp                    
                // printfn "after update %A %A" leafSetSmall leafSetLarge
                return! imp lastState
            | :? Leaf as l -> 
                        if(bigint.Compare(bigint.Parse(l.NodeId), bigint.Parse(nodeId)) < 0) then
                            addToLeaf leafSetSmall l.NodeId true
                        else if(bigint.Compare(bigint.Parse(l.NodeId), bigint.Parse(nodeId)) > 0) then
                            addToLeaf leafSetLarge l.NodeId false
                        return! imp lastState
            | :? RouteMessage as r -> 
                    
                    match r.Message with
                    | "route" ->
                                    let mutable node = isInLeafSet leafSetSmall leafSetLarge r.NodeId nodeId
                                    let foundInLeaf = node <> "null"
                                    if(node = "null") then
                                        node <- isInRoutingTable routingTable r.NodeId nodeId
                                        if(node = "null") then
                                            node <- getNearestNode routingTable leafSetSmall leafSetLarge r.NodeId nodeId
                                    
                                    if(node <> "null") then
                                        if(node = nodeId) then
                                            getIActorRef nodeId <! getRouteMessage r.NodeId "completed" (r.Hops + 1)
                                        else 
                                            getIActorRef node <! getRouteMessage r.NodeId "route" (r.Hops + 1)
                                    else 
                                        getIActorRef nodeId <! getRouteMessage r.NodeId "completed" (r.Hops + 1)
                                    printfn "routing for %A from %A to %A with %A hops" r.NodeId nodeId node r.Hops
                    | "completed" ->
                                    requestHops <- requestHops + r.Hops
                                    requestCompleted <- requestCompleted + 1
                    | _           ->
                                    "done" |> ignore
                    return! imp lastState
            | _ -> return! imp (lastState + 1)
        }

    imp initialState  

let getHashCode (s : string) =    
    s.ToCharArray()
    |> u8.GetBytes
    |> hash.ComputeHash
    |> Array.map (fun (x : byte) -> System.String.Format("{0:X2}", x))
    |> String.concat System.String.Empty

let getHash () =
    let mutable hash = "" 
    let range = Math.Pow(2.0, b |> double) |> int
    for i in 0 .. 15 do 
        hash <- hash + (rand.Next(range) |> string)
    hash

let pastryActorsCreator () = 
    printfn "creating actors"
    let a = ["1234567890123456"; "1230456789123456"; "1203456789123456"; "1023456789123456"]
    let b = ["1234567653123456"]
    for i in 0 .. numNodes - 1 do        
        // printfn "%A" actor
        // let nodeId = actor.Path |> string |> getHashCode
        // let nodeId = getHash()
        let mutable nodeId = "null"
        if(0 <= i && i < a.Length) then
            nodeId <- a.[i]
        else if(i = numNodes - 1) then
            nodeId <- b.[0]
        else 
            nodeId <- getHash()

        let actor = spawn system ("pastryActor" + string(i)) (pastryBehavior 0 nodeId)
          
        printfn "%A" nodeId
        pastryActors.Add(actor)        
        pastryMap <- pastryMap.Add(actor, nodeId)
        pastryMapHashCode <- pastryMapHashCode.Add(nodeId, actor)
        
let shuffle () = 
    printfn "shuffling"
    for i in 0 .. numNodes - 1 do
        let randomIndex1 = rand.Next(numNodes)
        let randomIndex2 = rand.Next(numNodes)
        let temp = pastryActors.Item(randomIndex1)
        //try to modify this
        pastryActors.Insert(randomIndex1, pastryActors.Item(randomIndex2))
        pastryActors.RemoveAt(randomIndex1 + 1)
        pastryActors.Insert(randomIndex2, temp)
        pastryActors.RemoveAt(randomIndex2 + 1)

let joinNetwork () = 
    printfn "joining network"
    for i in 0 .. numNodes - 1 do
        while joinedActors.Count < i do
            null |> ignore
        pastryActors.Item(i) <! "join"

let route() = 
    for i in 0 .. numNodes - 1 do
        for j in 0 .. numRequest - 1 do 
            pastryActors.[i] <! getRouteMessage (getHash()) "route" 0



[<EntryPoint>]
let main args =                 
    numNodes <- int(args.[0])
    numRequest <- int(args.[1])
    let numerator = numNodes |> float |> Math.Log10
    let denominator = b |> float |> (fun x -> Math.Pow(2.0, x)) |> Math.Log10
    rows <- Math.Ceiling(numerator / denominator) |> int
    columns <- b |> float |> (fun x -> Math.Pow(2.0, x)) |> int
    pastryActorsCreator()
    // shuffle()
    joinNetwork()
    route()
    Console.ReadLine() |> ignore
    printfn "joining %A %A" (totalHops / numNodes) totalHops
    printfn "%A" joinedActors.Count
    printfn "routing %A %A %A" (requestHops / (numNodes * numRequest)) requestHops requestCompleted
    0

// how to overcome join one by one
// do not update state in other actors send reply(state) from each intermidiate node to original actor
// once its joined, send its state to all other path, leaf, routing, neighbor nodes
// 
// add to leaf set after joining