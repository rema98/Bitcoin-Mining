(*
    Distributed Operating Systems - Project 1
    Team mates: Kavya Gopal, Rema Veranna Gowda
    The goal of first project is to use F# and the actor model to build a good solution to this bitcoin mining that runs well on multi-core machines.
    
    Input: The input provided will be, the required number of 0’s of the bitcoin(K) and workload.
    Output: Print the input string(randomly generated), and the corresponding SHA256 hash separated by a TAB, for each of the bitcoins you find.

    Running code on client:
    dotnet fsi –langversion:preview client.fsx <SERVER_IP> <SERVER_PORT>
    Note: This should be done before starting server
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

// Defining data types of messages that are sent between and within clients and servers
type ActorsMessage = 
    | ActorMsg of (int64*int*int64)
    | CompletedMsg of (string)

// Takes server IP and port as command line input
let serverip = fsi.CommandLineArgs.[1] |> string
let s_port = fsi.CommandLineArgs.[2] |>string

let address = "akka.tcp://BitCoin@" + serverip + ":" + s_port+ "/user/Printer"

// Client node configuration
let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            log-config-on-start : on
            stdout-loglevel : DEBUG
            loglevel : ERROR
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                debug : {
                    receive : on
                    autoreceive : on
                    lifecycle : on
                    event-stream : on
                    unhandled : on
                }
            }
            remote {
                helios.tcp {
                    port = 8778
                    hostname = 192.168.0.21 // Client IP
                }
            }
        }")

// Creats actor system with the above configuration
let system = ActorSystem.Create("RemoteBitCoinMining", configuration)

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

// Recieves tasks from RemoteParentActor
// Mines bitcoin by calling mineBitCoin k
// Sends result to server if bitcoin is found
type RemoteChildActor() =
    inherit Actor()
    override x.OnReceive message =
        let receivedMessage : ActorsMessage = downcast message
        let sref = select address system
        match receivedMessage with
        | ActorMsg(startind,k,endind) -> 
            for i in startind .. endind do
                // For each actor, mine bitcoin
                let miningResult = mineBitCoin k
                if (String.length(miningResult) > 1) then
                    sref <! miningResult // send result to server if bitcoin is found
            sref <! "Done"

        | _ ->  printfn "Unknown message"

// Recieves tasks from ParentActor of server node (Server shares workload for which we should mine bitcoins)
// Creates a pool of child actors on client machine
// Assigns tasks to child actors of client machine to mine coins
// Sends result to server if bitcoin is found
// Round robin approach is used to assign sub tasks to child actors
let RemoteParentActor = 
    spawn system "RemoteParentActor"
    <| fun mailbox ->
        let actorsCount = System.Environment.ProcessorCount |> int64
        let totalActors = actorsCount*1L
        let remoteChildActorsPool = 
                [1L .. totalActors]
                |> List.map(fun id -> system.ActorOf(Props(typedefof<RemoteChildActor>)))

        let childenum = [|for rp in remoteChildActorsPool -> rp|]
        let childSystem = system.ActorOf(Props.Empty.WithRouter(Akka.Routing.RoundRobinGroup(childenum)))

        let rec remoteParentoop() =
            actor {
                let! (message:obj) = mailbox.Receive()
                let (startindex,k,endindex) : Tuple<int64,int,int64> = downcast message
                childSystem <! ActorMsg(startindex,k,endindex)
                return! remoteParentoop()
            }
        printf "Remote Parent Started \n" 
        remoteParentoop()

System.Console.ReadLine()