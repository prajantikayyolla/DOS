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
    Hops : int;
    Path : List<string>
}

let system = ActorSystem.Create("System")
let pastryActors = new List<IActorRef>()
let joinedActors = new List<string>()
let mutable numNodes = 0
let mutable numRequest = 0
let b = 3
let mutable rows = 0
let mutable columns = 0
let u8 = Encoding.UTF8;
let hash = MD5.Create()
let mutable pastryMapHashCode = Map.empty<string, IActorRef>
let rand = System.Random()
let mutable totalHops = 0
let mutable totalRequest = 0
let maxHash = bigint.Parse("7777777777777777")
let mutable requestHops = 0
let mutable requestCompleted = 0
let mutable maxHops = 0

let getRecord msg nodeId routingTable hops interNodes = 
    {Msg = msg; NodeId = nodeId; RoutingTable = routingTable; Hops = hops; InterNodes = interNodes}

let getState nodeId routingTable leafSetSmall leafSetLarge timestamp  = 
    {NodeId = nodeId; RoutingTable = routingTable; LeafSetSmall = leafSetSmall; LeafSetLarge = leafSetLarge; TimeStamp = timestamp}

let getLeaf nodeId = 
    {NodeId = nodeId}

let getRouteMessage nodeId message hops path = 
    {NodeId = nodeId; Message = message; Hops = hops; Path = path}

let getValueAtIndex (nodeId : string) index = 
    let ch = nodeId.[index] |> char
    if (ch |> Char.IsDigit) then int ch - int '0'
    else int ch - int 'A' + 10

let setInitialRoutingtable (routingTable : string [,]) (nodeId : string) =
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
    let l = commonPrefix destNodeId srcNodeId
    for i in rowsInitialized .. min (rows - 1) l do
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
            if(routingTable.[i, j] <> "null" && ((min (commonPrefix routingTable.[i, j] nodeId) (rows - 1)) >= l)) then                
                distance <- bigint.Abs(bigint.Subtract(routingTable.[i, j] |> bigint.Parse, nodeId |> bigint.Parse))
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

let addToLeaf leaf nodeId destNodeId small = 
    let leafSet = Array.except ([|"null"|] |> Array.toSeq) leaf
    if not (Array.exists (fun x -> x = nodeId) leafSet) then
        if(leafSet.Length < (columns / 2)) then
            leaf.[Array.findIndex (fun x -> x = "null") leaf] <- nodeId
        else
            if small then
                let minValIndex = Array.findIndex (fun x -> x = Array.min leaf) leaf
                if (bigint.Compare(bigint.Abs(bigint.Subtract(nodeId |> bigint.Parse, destNodeId |> bigint.Parse)),bigint.Abs(bigint.Subtract(leafSet.[minValIndex] |> bigint.Parse, destNodeId |> bigint.Parse))) < 0) then
                    leaf.[minValIndex] <- nodeId
            else 
                let maxValIndex = Array.findIndex (fun x -> x = Array.max leaf) leaf
                if (bigint.Compare(bigint.Abs(bigint.Subtract(nodeId |> bigint.Parse, destNodeId |> bigint.Parse)),bigint.Abs(bigint.Subtract(leafSet.[maxValIndex] |> bigint.Parse, destNodeId |> bigint.Parse))) < 0) then
                    leaf.[maxValIndex] <- nodeId

let updateLeaf (srcLeaf : string[]) srcNodeId (destLeaf : string[]) destNodeId small = 
    for i in 0 .. srcLeaf.Length - 1 do
        if srcLeaf.[i] <> "null" && srcLeaf.[i] <> destNodeId then
            if small && (bigint.Compare(bigint.Parse(srcLeaf.[i]), bigint.Parse(destNodeId)) < 0) then
                addToLeaf destLeaf srcLeaf.[i] destNodeId small
            else if not small && (bigint.Compare(bigint.Parse(srcLeaf.[i]), bigint.Parse(destNodeId)) > 0) then
                addToLeaf destLeaf srcLeaf.[i] destNodeId small

            


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
                        | "join" ->
                            setInitialRoutingtable routingTable nodeId
                            if(joinedActors.Count <> 0) then
                                let proximityNode = getProximityNode() |> pastryMapHashCode.GetValueOrDefault
                                proximityNode <! getRecord "join" nodeId routingTable 0 (new List<String>())
                            else joinedActors.Add(nodeId)
                        | "print" -> 
                                    printfn "Node id : %A" nodeId
                                    printfn "Routing table : %A" routingTable
                                    printfn "LeafSet small : %A" leafSetSmall
                                    printfn "LeafSet Large : %A" leafSetLarge
                        | _ -> "done" |> ignore
                        return! imp (lastState + 1)
            | :? Record as r ->
                        match r.Msg with
                        | "join" ->     
                            getIActorRef r.NodeId <! getState nodeId routingTable leafSetSmall leafSetLarge timestamp
                            getIActorRef r.NodeId <! getLeaf nodeId

                            map <- map.Add (getIActorRef r.NodeId, timestamp)

                            r.InterNodes.Add(nodeId) |> ignore
                            let mutable node = isInLeafSet leafSetSmall leafSetLarge r.NodeId nodeId
                            let foundInLeaf = node <> "null"
                            if(node = "null") then
                                node <- isInRoutingTable routingTable r.NodeId nodeId
                                if(node = "null") then
                                    node <- getNearestNode routingTable leafSetSmall leafSetLarge r.NodeId nodeId
                            if(node <> "null") then
                                if(node = nodeId) then
                                    getIActorRef r.NodeId <! getRecord "arrived" r.NodeId r.RoutingTable (r.Hops + 1) r.InterNodes
                                else 
                                    getIActorRef node <! getRecord r.Msg r.NodeId r.RoutingTable (r.Hops + 1) r.InterNodes
                            else 
                                getIActorRef r.NodeId <! getRecord "arrived" r.NodeId r.RoutingTable (r.Hops + 1) r.InterNodes

                        | "arrived" ->
                            totalHops <- totalHops + r.Hops
                            joinedActors.Add(r.NodeId)
                            for i in 0 .. r.InterNodes.Count - 1 do
                                getIActorRef (r.InterNodes.Item(i)) <! getState nodeId routingTable leafSetSmall leafSetLarge timestamp
                                getIActorRef (r.InterNodes.Item(i)) <! getLeaf nodeId

                            for i in 0 .. rows - 1 do
                                for j in 0 .. columns - 1 do 
                                    if(routingTable.[i, j] <> "null" && routingTable.[i, j] <> nodeId) then
                                        getIActorRef routingTable.[i, j] <! getState nodeId routingTable leafSetSmall leafSetLarge (System.DateTime.MinValue.ToString("HH:mm:ss.ffffff"))
                                        getIActorRef routingTable.[i, j] <! getLeaf nodeId

                            for i in 0 .. leafSetSmall.Length - 1 do
                                if(leafSetSmall.[i] <> "null") then
                                    getIActorRef leafSetSmall.[i] <! getState nodeId routingTable leafSetSmall leafSetLarge (System.DateTime.MinValue.ToString("HH:mm:ss.ffffff"))
                                    getIActorRef leafSetSmall.[i] <! getLeaf nodeId
                                if(leafSetLarge.[i] <> "null") then
                                    getIActorRef leafSetLarge.[i] <! getState nodeId routingTable leafSetSmall leafSetLarge (System.DateTime.MinValue.ToString("HH:mm:ss.ffffff"))
                                    getIActorRef leafSetLarge.[i] <! getLeaf nodeId

                        | _ ->
                            "done" |> ignore
                        return! imp (lastState + 1)
            | :? State as s -> 
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
                            map <- map.Add((mailbox.Sender()), timestamp)
                            lastUpdatedTime <- s.TimeStamp
                    else 
                        updateState s.RoutingTable routingTable nodeId s.NodeId
                        updateLeaf s.LeafSetSmall s.NodeId leafSetSmall nodeId true
                        updateLeaf s.LeafSetLarge s.NodeId leafSetLarge nodeId false
                        lastUpdatedTime <- s.TimeStamp   
                return! imp lastState
            | :? Leaf as l -> 
                        if(bigint.Compare(bigint.Parse(l.NodeId), bigint.Parse(nodeId)) < 0) then
                            addToLeaf leafSetSmall l.NodeId nodeId true
                        else if(bigint.Compare(bigint.Parse(l.NodeId), bigint.Parse(nodeId)) > 0) then
                            addToLeaf leafSetLarge l.NodeId nodeId false
                        return! imp lastState
            | :? RouteMessage as r -> 
                    
                    match r.Message with
                    | "route" ->    
                                    r.Path.Add(nodeId) |> ignore
                                    let mutable node = isInLeafSet leafSetSmall leafSetLarge r.NodeId nodeId
                                    let foundInLeaf = node <> "null"
                                    if(node = "null") then
                                        node <- isInRoutingTable routingTable r.NodeId nodeId
                                        if(node = "null") then
                                            node <- getNearestNode routingTable leafSetSmall leafSetLarge r.NodeId nodeId
                                    
                                    if(node <> "null") then
                                        if(node = nodeId) then
                                            getIActorRef nodeId <! getRouteMessage r.NodeId "completed" (r.Hops + 1) r.Path
                                        else 
                                            getIActorRef node <! getRouteMessage r.NodeId "route" (r.Hops + 1) r.Path
                                    else 
                                        getIActorRef nodeId <! getRouteMessage r.NodeId "completed" (r.Hops + 1) r.Path
                                    
                    | "completed" ->
                                    
                                    requestHops <- requestHops + r.Hops
                                    requestCompleted <- requestCompleted + 1
                                    maxHops <- max maxHops r.Hops
                                    
                                    
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

let getHashRoute () = 
    let mutable hash = "" 
    let range = Math.Pow(2.0, b |> double) |> int
    for i in 0 .. 14 do 
        hash <- hash + (rand.Next(range) |> string)
    hash

let pastryActorsCreator () = 
    let m = Math.Pow(2.0, b |> float) |> int
    for i in 0 .. numNodes - 1 do 
        let mutable nodeId = "null"
        nodeId <- string(i % m) + getHashRoute()

        let actor = spawn system ("pastryActor" + string(i)) (pastryBehavior 0 nodeId)
        pastryActors.Add(actor)        
        pastryMapHashCode <- pastryMapHashCode.Add(nodeId, actor)

let joinNetwork () = 
    for i in 0 .. numNodes - 1 do
        while joinedActors.Count < i do
            null |> ignore
        pastryActors.Item(i) <! "join"

let printStates() = 
    for i in 0 .. numNodes - 1 do
        pastryActors.[i] <! "print"
        Threading.Thread.Sleep(100)
        

let route() = 
    let m = Math.Pow(2.0, b |> float) |> int
    for i in 0 .. numRequest - 1 do
        Threading.Thread.Sleep(1000)
        for j in 0 .. numNodes - 1 do 
            pastryActors.[i] <! getRouteMessage (string(j % m) + getHashRoute()) "route" 0 (new List<string>())

let getTotalRequests() = 
    totalRequest

[<EntryPoint>]
let main args =                 
    numNodes <- int(args.[0])
    numRequest <- int(args.[1])
    let numerator = numNodes |> float |> Math.Log10
    let denominator = b |> float |> (fun x -> Math.Pow(2.0, x)) |> Math.Log10
    rows <- Math.Ceiling(numerator / denominator) |> int
    columns <- b |> float |> (fun x -> Math.Pow(2.0, x)) |> int
    pastryActorsCreator()
    joinNetwork()
    route()
    let mutable exit = false
    let tr = numRequest * numNodes
    System.Threading.Thread.Sleep(numRequest)
    printfn "joining with avg hops of %A and total hops registered for joining is %A" (float totalHops / float numNodes) totalHops
    printfn "Average Hops: %A" (float requestHops / float (requestCompleted))

    
    0
