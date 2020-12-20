#if INTERACTIVE
#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#endif

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Collections.Generic

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
            printfn "%A " a 

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
                                | (_, _, _) as subProblem -> 
                                                            squarePyramid subProblem
                                                            mailbox.Sender() <! "Done"

                                return! loop() }
                            loop())

    let workerActors = workerActorsCreator

    let arr = new List<Async<obj>>()

    for id in 0 .. numActors - 1 do
        let windowStart = max 1 (id * skip - k + 2)
        let windowEnd = min n (id * skip + skip)
        arr.Add((workerActors.Item(id) <? (windowStart, windowEnd, int(k))))
        
    arr
    
let bossActor = 
    spawn system "bossActor"
    <| fun mailbox ->
        let rec loop() = actor {
            let! msg = mailbox.Receive()
            match msg with
            | (_, _, _) as input -> let res = divideWork input
                                    for r in res do
                                        Async.RunSynchronously (r, -1) |> ignore
                                    mailbox.Sender() <! "Done"
            return! loop() }
        loop()

[<EntryPoint>]
let main args =                 
    let asyncRef = bossActor <? (int(args.[0]), int(args.[1]), int(4))
    Async.RunSynchronously (asyncRef, -1) |> ignore
    0
