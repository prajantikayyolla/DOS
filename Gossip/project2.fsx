#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 


open System
open Akka.Actor
open Akka.FSharp
open System.Collections.Generic
open System.Threading;

// This methods gets random neighbor for full topology
let fullNetwork currentNode totalNodes (exclude : Set<int>) = 
    let rand = System.Random()
    let mutable randomNode = currentNode
    randomNode <- rand.Next(totalNodes)
    while (exclude.Contains(randomNode)) do
        randomNode <- rand.Next(totalNodes)
    randomNode
    
// It returns a list of all neighbors for the current node in a 2D grid network
let grid2D currentNode totalNodes = 
    let rand = System.Random()
    let mutable randomNode = rand.Next(4)
    let rows = (double) totalNodes |> sqrt |> int
    let cols = rows
    let currentNodeRow = currentNode / rows
    let currentNodeCol = currentNode % rows
    let directions = [(0, 1); (1, 0); (0, -1); (-1, 0)]
    let listOfPossibilities = new List<int>()
    for i in 0 .. 3 do
        let randomNodeRow = currentNodeRow + fst (directions.Item(i))
        let randomNodeCol = currentNodeCol + snd (directions.Item(i))
        if (randomNodeRow < 0 || randomNodeRow >= rows || randomNodeCol < 0 || randomNodeCol >= cols) then
            ()
        else 
            listOfPossibilities.Add(randomNodeRow * rows + randomNodeCol)
    listOfPossibilities

// Fetches adjacent neighbours for current node.
let line currentNode totalNodes =

    if (currentNode = 0) then
            1
    elif (currentNode = totalNodes - 1) then
        totalNodes - 2
    else 
        let rand = System.Random()
        let randomNode = rand.Next(2)
        if (randomNode = 0) then  currentNode - 1 else (currentNode + 1)

// Uses 2D grid and full network functionality to get random neighbor.
let grid2DImperfect currentNode totalNodes = 

    let listOfPossibilities = grid2D currentNode totalNodes
    listOfPossibilities.Add(currentNode)
    let randomPossibility = fullNetwork currentNode totalNodes (Set(listOfPossibilities))
    listOfPossibilities.Add(randomPossibility)
    let rand = System.Random()
    let mutable randomNode = listOfPossibilities.Item(rand.Next(listOfPossibilities.Count))
    while (randomNode = currentNode) do
        randomNode <- listOfPossibilities.Item(rand.Next(listOfPossibilities.Count))
    randomNode

// Internally calls specific topology function listed above to get random neighbor.
let random currentNode totalNodes topology = 

    if(topology = "full") then
        fullNetwork currentNode totalNodes (Set.empty.Add(currentNode))

    elif(topology = "2D") then
        let listOfPossibilities = grid2D currentNode totalNodes
        let rand = System.Random()
        listOfPossibilities.Item(rand.Next(listOfPossibilities.Count))

    elif(topology = "line") then
        line currentNode totalNodes
     
    else
        grid2DImperfect currentNode totalNodes

let system = ActorSystem.Create("System")
let gossipActors = new List<IActorRef>()
let completedActors : HashSet<IActorRef> = new HashSet<IActorRef>();
let startedActors : HashSet<IActorRef> = new HashSet<IActorRef>();

// Recursive function which gossips the message to random neighbor until cancellationtoken is sent.
let rec gossip msg (ct : CancellationToken) : Async<unit>=
    async {
        let (currentNode, totalNodes, topology) = msg
        if ct.IsCancellationRequested then
            return ()
        else 
            let randomNode = random currentNode totalNodes topology
            if (randomNode >= 0 && randomNode < totalNodes) then
                gossipActors.Item(randomNode) <! (randomNode, totalNodes, topology)
            return! gossip msg ct
    }

type Async with
    static member Isolate(f : (int * int * string) -> CancellationToken -> Async<'T>) (msg : int * int * string) : Async<'T> =
        async {
            let! ct = Async.CancellationToken
            let isolatedTask = Async.StartAsTask(f msg ct)
            return! Async.AwaitTask isolatedTask
        }
//This is the behaviour of gossip actors
let actorOfGossip initialState (mailbox : Actor<'a>) =
    let mutable cts = new CancellationTokenSource()
    let rec imp lastState =
        actor {
            let! msg = mailbox.Receive()
            match box msg with
            | :? ReceiveTimeout ->
                printfn "%A recieved timeout" mailbox.Context.Self
                completedActors.Add(mailbox.Context.Self) |> ignore
                cts.Cancel()
            | _ ->
                let (currentNode, totalNodes, topology) = msg
                if(lastState = 0) then             
                    startedActors.Add(gossipActors.Item(currentNode)) |> ignore
                    Async.Start(Async.Isolate gossip msg, cancellationToken = cts.Token)
                let newState = lastState + 1
                if(newState = 10) then
                    completedActors.Add(gossipActors.Item(currentNode)) |> ignore
                    cts.Cancel()  
                mailbox.Context.SetReceiveTimeout(System.Nullable <| TimeSpan.FromSeconds(float 2))  
                return! imp newState
        }

    imp initialState  

// It creates the gossip actors for given n.
let gossipActorsCreator n = 
    for i in 0 .. n - 1 do
        let actor = spawn system ("gossipActor" + string(i)) (actorOfGossip 0)
        gossipActors.Add(actor)

// Anti Entropy with pull for gossiping
let pull totalNodes topology = 
    async{
        while true do
            for i in 0 .. totalNodes - 1 do 
                let mutable randomNode = random i totalNodes topology
                while (randomNode < 0 || randomNode >= totalNodes) do
                    randomNode <- random i totalNodes topology
                if(startedActors.Contains(gossipActors.Item(randomNode))) then
                    gossipActors.Item(i) <! (i, totalNodes, topology)
    }

// let display totalNodes = 
//     printfn ""
//     for i in 0 .. totalNodes - 1 do
//         if completedActors.Contains(gossipActors.Item(i)) then
//             printf "C"
//         else if startedActors.Contains(gossipActors.Item(i)) then
//             printf "S"
//         else 
//             printf "X"
//         if (i + 1) % 31 = 0 then
//             printfn ""

let isPerfect num : bool=
    (num |> sqrt) - (num |> sqrt |> floor) = 0.0

let nextPerfect n = 
    let mutable num = n
    while not (isPerfect (num |> float)) do
        num <- num - 1
    num
    
let mutable termActor = 0
let pushsumActors = new List<IActorRef>()

let helper totalNodes topology =
    if(topology = "2D") then
         int(float(totalNodes) * 0.25)
    else 
        int(float(totalNodes) * 0.796812)
    
let actorofPushSum (sum : float) (weight : float) count (mailbox : Actor<'a>) = 
    let rec imp (newSum : float) (newWeight : float) lastCount =
        actor{            
            let! msg = mailbox.Receive()
            let swRatio = newSum/newWeight
            let (currentNode, totalNodes, topology, receivedSum, receivedWeight) = msg
            let newActorSum = newSum + receivedSum
            let newActorWeight = newWeight + receivedWeight
            let propSum = newActorSum/2.0
            let propWeight = newActorWeight/2.0
            let newswRatio = propSum/propWeight
            let diff = swRatio - newswRatio |> abs
            let mutable newCount = 0

            if (lastCount = -1) then 
                startedActors.Add(pushsumActors.Item(currentNode)) |> ignore
                mailbox.Context.SetReceiveTimeout(Nullable<TimeSpan>(TimeSpan.FromSeconds 2.0))
                let mutable randomNode = random currentNode totalNodes topology
                while  (randomNode < 0 || randomNode >= totalNodes) do 
                    randomNode <- random currentNode totalNodes topology
                pushsumActors.Item(randomNode) <! (randomNode, totalNodes, topology, propSum, propWeight)
            else 
                if (diff < 10.0**(-10.0)) then
                    newCount <- lastCount + 1
                else 
                    newCount <- 0
                if newCount = 3 then
                    termActor <- termActor+1
                    completedActors.Add(pushsumActors.Item(currentNode)) |> ignore
                let mutable randomNode = random currentNode totalNodes topology
                let lop = grid2D currentNode totalNodes
                let len = lop.Count
                let mutable list1 = []
                let mutable exit= 0
                while (randomNode < 0 ||exit =1 || randomNode >= totalNodes || completedActors.Contains(pushsumActors.Item(randomNode)) ) do
                    if list1.Length = len then
                        exit <- 1
                    randomNode <- random currentNode totalNodes topology
                    if (List.exists(fun elem -> elem =randomNode)list1) then 
                        list1 <- list1 @ [randomNode] 
                if exit =1 then 
                    let rand1= System.Random()
                    let mutable xNode=rand1.Next(totalNodes)
                    while completedActors.Contains(pushsumActors.Item(xNode)) do
                        xNode <- rand1.Next(totalNodes)
                    pushsumActors.Item(xNode) <! (xNode, totalNodes, topology, receivedSum, receivedWeight)
                else 
                    pushsumActors.Item(randomNode) <! (randomNode, totalNodes, topology, propSum, propWeight)
            return! imp propSum propWeight newCount
        }
    imp sum weight count

let pushsumActorsCreator n =
    for i in 0 .. n - 1 do 
        let x= float(i)
        let actor = spawn system ("pushsumActor" + string(i)) (actorofPushSum x 1.0 (-1))
        pushsumActors.Add(actor)

    
  
let mutable totalNodes = string(fsi.CommandLineArgs.GetValue 1) |> int
let topology = string (fsi.CommandLineArgs.GetValue 2)
let algorithm = string (fsi.CommandLineArgs.GetValue 3)
let mutable exit = false    
if(topology = "2D" || topology = "imp2D") then
    totalNodes <- nextPerfect totalNodes
let stopWatch = System.Diagnostics.Stopwatch.StartNew()
if(algorithm = "gossip") then
    gossipActorsCreator totalNodes
    let rand = System.Random()
    let randomNode = rand.Next(totalNodes)        
    gossipActors.Item(randomNode) <! (randomNode, totalNodes, topology) 
    Async.Start(pull totalNodes topology)
    while not exit do
        //printfn "%d %d %d" completedActors.Count startedActors.Count totalNodes
        if completedActors.Count >= (helper totalNodes topology) then
            exit <- true
    //printfn "converged : %d" completedActors.Count
else 
    pushsumActorsCreator totalNodes
    pushsumActors.Item(0) <! (0, totalNodes, topology, 0.0, 0.0)
    while not exit do
        if completedActors.Count >= 1 then
            exit <- true
stopWatch.Stop()
printfn "%f" stopWatch.Elapsed.TotalMilliseconds
    
    
