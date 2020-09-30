#if INTERACTIVE
#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#endif

open System
open System.Collections.Generic

let fullNetwork currentNode totalNodes (exclude : Set<int>) = 
    let rand = System.Random()
    let mutable randomNode = currentNode
    randomNode <- rand.Next(totalNodes + 1)
    while (exclude.Contains(randomNode)) do
        randomNode <- rand.Next(totalNodes)
    randomNode

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

let line currentNode totalNodes =
    if (currentNode = 0) then
            1
    elif (currentNode = totalNodes - 1) then
        totalNodes - 2
    else 
        let rand = System.Random()
        let randomNode = rand.Next(2)
        if (randomNode = 0) then  currentNode - 1 else (currentNode + 1)

let grid2DImperfect currentNode totalNodes = 
    let listOfPossibilities = grid2D currentNode totalNodes
    listOfPossibilities.Add(currentNode)
    let randomPossibility = fullNetwork currentNode totalNodes (Set(listOfPossibilities))
    listOfPossibilities.Add(randomPossibility)
    let rand = System.Random()
    let mutable randomNode = rand.Next(listOfPossibilities.Count)
    while (randomNode = currentNode) do
        randomNode <- rand.Next(listOfPossibilities.Count)
    randomNode

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



[<EntryPoint>]
let main args =                 
    let res1 = random 0 9 "full"
    let res2 = random 0 9 "2D"
    let res3 = random 0 9 "line"
    let res4 = random 0 9 "dfgh"
    printfn "%d %d %d %d" res1 res2 res3 res4
    Console.ReadLine() |> ignore
    0   




        



