(*
    Distributed Operating Systems - Project 1
    Team mates: Kavya Gopal, Rema Veranna Gowda
    The goal of first project is to use F# and the actor model to build a good solution to this bitcoin mining that runs well on multi-core machines.
    
    Input: The input provided will be, the required number of 0’s of the bitcoin(K) and workload.
    Output: Print the input string(randomly generated), and the corresponding SHA256 hash separated by a TAB, for each of the bitcoins you find.

    Running code on Server:
    dotnet fsi –langversion:preview server.fsx N K
    N -> workload
    K -> number of zeros the bitcoin should start with
*)

#time "on"
#r "nuget: Akka"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Security.Cryptography

// Server node Configuration
let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            log-config-on-start : on
            stdout-loglevel : DEBUG
            loglevel : ERROR
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
            remote.helios.tcp {
                transport-protocol = tcp
                port = 8777
                hostname = 192.168.0.129 // Server IP
            }
        }")

let system = ActorSystem.Create("BitCoin", configuration)
let actorsCount = System.Environment.ProcessorCount |> int64
type ActorsMessage = 
    | ActorMsg of (int64*int*int64)
    | CompletionMsg of (string)
    | Input of (int64*int)


// Internal function to mine bitcoing
// Generates random string; hashes it and return the string for which the hashed values starts with K number of zeros
let mineBitCoin k : string =

    // Contains random string and hashed value seperated by tab
    let mutable result = ""

    // creates random string
    let ranStr n = 
        let r = Random()
        let chars = Array.concat([[|'a' .. 'z'|];[|'A' .. 'Z'|];[|'0' .. '9'|]])
        let sz = Array.length chars in
        String(Array.init n (fun _ -> chars.[r.Next sz]))
    let randString = ranStr(20)

    // Internal function to convert bytes to hex - Required since the output of hashing is in bytes and we want in hexadecimal format
    let fpEncode (bytes:byte[]): string =
        let byteToHex (b:byte) = b.ToString("x2")
        Array.map byteToHex bytes |> String.concat ""

    // Convert the random string to hashed value
    // Added gater link ID as prefix as per requirement of project
    let resultInString = fpEncode ((new SHA256Managed()).ComputeHash(System.Text.Encoding.ASCII.GetBytes ("rveerannagowda;"+randString)))
     
    // Calculate the number of zeros the coin should start with using input K  
    let mutable prefixCoin = ""
    for i in 1..k do
        prefixCoin <- prefixCoin + "0"

    // Test if we found bitcoin. finalResultBool is true if found.
    let finalResultBool = 
        match resultInString with
        | resultInString when resultInString.StartsWith(prefixCoin) -> true
        | _ -> false  

    // Random string and corresponding bitcoin is stored in result variable which will be returned 
    if (finalResultBool) then
        result <- randString + "\t" + resultInString
    result


// Recieves results from child actor of client and prints them in server node.
let printClientResult (mailbox:Actor<_>) = 
    let rec printClientResultLoop () = actor {
        let parent = system.ActorSelection("akka.tcp://BitCoin@192.168.0.129:8777/user/parent")

        let! (message:obj) = mailbox.Receive() // recieve results from client

        // When the message is a string - if it is the result, print it.
        // Else if the message is to show the client child actors have completed task, send message to parent about completion
        if (message :? string) then
            let (resultString:String) = downcast message
            if (resultString.StartsWith("Done")) then
                parent <! CompletionMsg("Execution complete by Client")
            else
                printfn "The result from client is  %s" resultString
        return! printClientResultLoop()
    }
    printClientResultLoop()

let printClientResultRef = spawn system "Printer" printClientResult

// Child Actors
// Recieves tasks from ParentActor
// Mines bitcoin by calling mineBitCoin k
// Prints result if bitcoin is found
let ChildActor (mailbox:Actor<_>) =
    let rec childActorLoop () = actor {
        let! (message : ActorsMessage) = mailbox.Receive()
        let parent = mailbox.Sender()
        let messageRecieved : ActorsMessage = message
        match messageRecieved with
        | ActorMsg(beginIndex, k, endIndex) -> 

            for i in beginIndex .. endIndex-1L do
                let miningResult = mineBitCoin k
                if (String.length(miningResult) > 1) then
                    printfn "The result from server is  %s " miningResult
            parent <! CompletionMsg("Execution completed by Client")

        | _ ->  printfn "Unknown message"

        return! childActorLoop()
    }
    childActorLoop()

// Parent Actors
// Takes input from command line and creates the actor pool for server.
// Splits the tasks based on cores count and allocates using Round-Robin fashion
// sends the sub-tasks to the client parent which inturn allocates to it's child actors in the client machine
let ParentActor (mailbox:Actor<_>) = 
    let sref = system.ActorSelection(
                    "akka.tcp://RemoteBitCoinMining@192.168.0.21:8778/user/RemoteParentActor")

    let totalactors = actorsCount*250L
    let childActorsPool = 
            [1L .. totalactors]
            |> List.map(fun id -> spawn system (sprintf "Local_%d" id) ChildActor)
    let childenum = [|for lp in childActorsPool -> lp|]
    let childSystem = system.ActorOf(Props.Empty.WithRouter(Akka.Routing.RoundRobinGroup(childenum)))

    let mutable completed = 0L
    let splits = actorsCount*2L

    let rec parentActorLoop () = actor {
        let! message = mailbox.Receive()
        match message with 
        | Input(n, k) -> 
            let mutable beginIndex = 1L
            let subTaskSize = n/splits
            for i in [1L..splits] do                               
                if i = splits then
                    childSystem <! ActorMsg(beginIndex, k, n)         
                elif i % 2L = 0L then
                    let endIndex = beginIndex + subTaskSize - 1L
                    childSystem <! ActorMsg(beginIndex, k, endIndex)
                    beginIndex <- beginIndex + subTaskSize
                else
                    sref <! (beginIndex, k, (beginIndex + subTaskSize - 1L))
                    beginIndex <- beginIndex + subTaskSize
        | CompletionMsg(complete) ->
            completed <- completed + 1L
            if completed = splits then
                mailbox.Context.System.Terminate() |> ignore 
        | _ ->  printfn "Unknown message"

        return! parentActorLoop()
    }
    parentActorLoop()


let parent = spawn system "parent" ParentActor

// Input from Command Line N-> Workload K-> No of zero's to prefix bitcoin
let N = fsi.CommandLineArgs.[1] |> int64
let K = fsi.CommandLineArgs.[2] |> int
parent <! Input(N, K)

// Wait until all the actors has finished processing
system.WhenTerminated.Wait()