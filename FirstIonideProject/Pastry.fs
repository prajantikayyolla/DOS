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
     RowsInitialized : int;
     InterNodes : List<String> }

type State = {
    NodeId : string;
    RoutingTable : string [,]
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

let getRecord msg nodeId routingTable hops rowsInitialized interNodes = 
    {Msg = msg; NodeId = nodeId; RoutingTable = routingTable; Hops = hops; RowsInitialized = rowsInitialized; InterNodes = interNodes}

let getState nodeId routingTable = 
    {NodeId = nodeId; RoutingTable = routingTable}

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

let isInLeafSet leafSetSmall leafSetLarge nodeId = 
    let leafSet = Array.except ([|"null"|] |> Array.toSeq) (Array.append leafSetSmall leafSetLarge)
    if(leafSet.Length = 0) then
        "null"
    else if(leafSet |> Array.min <= nodeId && nodeId <= (leafSet |> Array.max)) then
        findNearestInLeaf leafSet nodeId
    else 
        "null"   

let getIActorRef nodeId = 
    pastryMapHashCode.GetValueOrDefault(nodeId)

let isInRoutingTable (routingTable : string [,]) destNodeId srcNodeId = 
    let row = min (commonPrefix destNodeId srcNodeId) (rows - 1)
    let column = getValueAtIndex destNodeId row
    if(routingTable.[row, column] = destNodeId) then
        destNodeId
    else 
        "null"

let getNearestNode (routingTable : string [,]) leafSetSmall leafSetLarge nodeId = 
    let mutable min = maxHash
    let mutable nearestNode = "null"
    let mutable distance = bigint.Zero

    let leafSet= Array.except ([|"null"|] |> Array.toSeq) (Array.append leafSetSmall leafSetLarge)

    for i in 0 .. routingTable.GetLength(0) - 1 do 
        for j in 0 .. routingTable.GetLength(1) - 1 do
            if(routingTable.[i, j] <> "null") then                
                distance <- bigint.Abs(bigint.Subtract(routingTable.[i, j] |> bigint.Parse, nodeId |> bigint.Parse))
                // printfn "%A %A %A format" (routingTable.[i, j]) (nodeId) (distance)
                if(bigint.Compare(distance, min) < 0) then
                    min <- distance
                    nearestNode <- routingTable.[i, j]
    
    for i in 0 .. leafSet.Length - 1 do
        distance <- bigint.Abs(bigint.Subtract(leafSet.[i] |> bigint.Parse, nodeId |> bigint.Parse))
        if(bigint.Compare(distance, min) < 0) then
            min <- distance
            nearestNode <- leafSet.[i]
    
    nearestNode      

let updateState (srcRoutingTable : string[,]) (destRoutingTable : string[,]) destNodeId srcNodeId = 
    for i in 0 .. (rows - 1) do
        for j in 0 .. (columns - 1) do 
            let l = commonPrefix destNodeId srcRoutingTable.[i, j]
            if(l >= i && srcRoutingTable.[i, j] <> "null" && destRoutingTable.[i, j] <> destNodeId) then
                if(destRoutingTable.[i, j] = "null") then
                    destRoutingTable.[i, j] <- srcRoutingTable.[i, j]
                else if(bigint.Compare(bigint.Abs(bigint.Subtract(srcRoutingTable.[i, j] |> bigint.Parse, destNodeId |> bigint.Parse)),bigint.Abs(bigint.Subtract(destRoutingTable.[i, j] |> bigint.Parse, destNodeId |> bigint.Parse))) < 0) then
                    destRoutingTable.[i, j] <- srcRoutingTable.[i, j]



let pastryBehavior initialState (mailbox : Actor<'a>) = 
    let mutable nodeId = null
    let routingTable = Array2D.create rows columns "null"
    let leafSetSmall = Array.create (Math.Pow(2.0, float(b - 1)) |> int) "null"
    let leafSetLarge = Array.create (Math.Pow(2.0, float(b - 1)) |> int) "null"
    let rec imp lastState =
        actor {
            let! msg = mailbox.Receive()
            printfn "asking %A with routing table %A" (pastryMap.GetValueOrDefault(mailbox.Self)) (routingTable)
            printfn "%A" msg
            match box msg with
            | :? string as s -> 
                        match s with 
                        | "route" -> 
                            if(lastState = numRequest) then
                                completedActors.Add(nodeId) |> ignore
                        | "join" ->
                            // printfn "%A %A" mailbox.Self (pastryMap.GetValueOrDefault(mailbox.Self))
                            nodeId <- pastryMap.GetValueOrDefault(mailbox.Self)
                            setInitialRoutingtable routingTable nodeId
                            if(joinedActors.Count <> 0) then
                                let proximityNode = getProximityNode() |> pastryMapHashCode.GetValueOrDefault
                                proximityNode <! getRecord "join" nodeId routingTable 0 0 (new List<String>())
                            else joinedActors.Add(nodeId)
                            return! imp (lastState + 1)
                        | _ -> "done" |> ignore
                        return! imp (lastState + 1)
            | :? Record as r ->
                        match r.Msg with
                        | "join" ->
                            let rowsInitialized = setRoutingTable r.NodeId r.RoutingTable nodeId routingTable r.Hops r.RowsInitialized
                            let mutable node = isInLeafSet leafSetSmall leafSetLarge r.NodeId
                            r.InterNodes.Add(nodeId) |> ignore
                            if(node <> "null") then
                                getIActorRef node <! getRecord "arrived" r.NodeId r.RoutingTable (r.Hops + 1) rowsInitialized r.InterNodes
                                return! imp (lastState + 1)
                            node <-  isInRoutingTable routingTable r.NodeId nodeId
                            if(node <> "null") then
                                if(node = nodeId) then
                                    getIActorRef node <! getRecord "arrived" r.NodeId r.RoutingTable (r.Hops + 1) rowsInitialized r.InterNodes
                                else
                                    getIActorRef node <! getRecord r.Msg r.NodeId r.RoutingTable (r.Hops + 1) rowsInitialized r.InterNodes
                                return! imp (lastState + 1)                        
                            node <- getNearestNode routingTable leafSetSmall leafSetLarge r.NodeId                            
                            if(node <> "null") then                                
                                if(node = nodeId) then
                                    getIActorRef node <! getRecord "arrived" r.NodeId r.RoutingTable (r.Hops + 1) rowsInitialized r.InterNodes
                                else
                                    getIActorRef node <! getRecord r.Msg r.NodeId r.RoutingTable (r.Hops + 1) rowsInitialized r.InterNodes
                            return! imp (lastState + 1)
                        | "arrived" ->
                            totalHops <- totalHops + r.Hops
                            totalRequest <- totalRequest + 1
                            joinedActors.Add(r.NodeId)
                            for i in 0 .. r.InterNodes.Count - 1 do
                                getIActorRef (r.InterNodes.Item(i)) <! getState r.NodeId r.RoutingTable
                            return! imp (lastState + 1)
                        | _ ->
                            return! imp (lastState + 1)
            | :? State as s -> 
                updateState s.RoutingTable routingTable nodeId s.NodeId
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
        let actor = spawn system ("pastryActor" + string(i)) (pastryBehavior 0)
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


[<EntryPoint>]
let main args =                 
    numNodes <- int(args.[0])
    numRequest <- int(args.[1])
    let numerator = numNodes |> float |> Math.Log10
    let denominator = b |> float |> (fun x -> Math.Pow(2.0, x)) |> Math.Log10
    rows <- Math.Ceiling(numerator / denominator) |> int
    columns <- b |> float |> (fun x -> Math.Pow(2.0, x)) |> int
    pastryActorsCreator()
    shuffle()
    joinNetwork()
    Console.ReadLine() |> ignore
    printfn "%A" (totalHops / totalRequest)
    0
