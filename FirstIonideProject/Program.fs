#if INTERACTIVE
#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#endif

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp

let squarePyramid subProblem =

    let windowStart, windowEnd, k = subProblem

    let isPerfect num : bool=
        (num |> sqrt) - (num |> sqrt |> floor) = 0.0
       
    let sumOfSquares num =
        ((num) * (num + bigint.One) * (bigint(2) * num + bigint.One)) / bigint(6)

    for a = windowStart to windowEnd - k + 1 do
        let sumOfSquaresTillLast = (bigint(a) + bigint(k : int) - bigint.One) |> sumOfSquares
        let sumOfSquaresTillFirst = (bigint(a) - bigint.One) |> sumOfSquares
        let sumOfSquaresDiff = sumOfSquaresTillLast - sumOfSquaresTillFirst
        if(isPerfect (sumOfSquaresDiff |> float)) then
            printfn "%i " a 
    

let system = ActorSystem.Create("System")

let divideWork input =
   
    let n, k, numActors = input
    
    let skip = int (n / numActors)

    let workerActorsCreator = 
        [1 .. numActors]
        |> List.map(fun id ->   
                        spawn system ("actor" + string(id))
                        <| fun mailbox ->
                            let rec loop() = actor {
                                let! msg = mailbox.Receive()
                                match msg with
                                | (_, _, _) as subProblem -> squarePyramid subProblem

                                return! loop() }
                            loop())

    let workerActors = workerActorsCreator


    for id in 0 .. numActors - 1 do
        let windowStart = max 1 (id * skip - k + 2)
        let windowEnd = min n (id * skip + skip)
        workerActors.Item(id) <! (windowStart, windowEnd, int(k))
        

let bossActor = 
    spawn system "bossActor"
    <| fun mailbox ->
        let rec loop() = actor {
            let! msg = mailbox.Receive()
            match msg with
            | (_, _, _) as input -> divideWork input
            | _ -> printfn "Required 3 arrguments but given < 3"
            return! loop() }
        loop()

[<EntryPoint>]
let main args =
    printfn "Arguments passed to function : %A" args
    bossActor <! "hi"
    bossActor <! (int(args.[0]), int(args.[1]), int(4))
    printfn "%A" bossActor
    System.Console.ReadLine() |> ignore
    0

//bigint , actorOf , id for actors given tuple as input
