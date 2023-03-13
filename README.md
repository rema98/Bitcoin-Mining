# BitcoinMining

## Problem Statement
Bitcoins are the most popular crypto-currency in common use. At their heart, bitcoins use the
hardness of cryptographic hashing to ensure a limited supply of coins. In particular, the key
component in a bit-coin is an input that, when hashed produces an output smaller than a target
value. In practice, the comparison values have leading 0's, thus the bitcoin is required to have a given
number of leading 0's.The hash you are required to use is SHA-256.
The goal of this project is to use F# and the actor model to build a good solution to this problem
that runs well on multi-core machines.
## Requirements
* Input: The input provided will be, the required number of 0's of the bitcoin(K) and workload(N).
* Output: Print the input string(randomly generated), and the corresponding SHA256 hash separated
by a TAB, for each of the bitcoins you find.
## Solution
 * We implemented a remote actor model using two machines to mine bitcoins.
 * In each machine, the number of actors are created based on the number of cores in that machine.
 * The task, to mine bitcoin in this case, is divided among child actors by the parent actor.
 * If only the server machine is running, all tasks are run by child actors on the server.
 * When a client node is available, the tasks are divided between them.
 * The parent actor in the server divides tasks among the child actors in server and sends tasks to
client machines.
 * The workload is equally distributed among server and client.
 * The results from the client are sent back to the server and are printed at server end.
## Implementation Details
* Takes input from command line(workload and the number of zero's that should pre x the
bitcoin).
* It spawns the parent actor and creates a list of child actors based on the number of processors
in the system.
* The parent actor assigns tasks to child actors in server machine and sends tasks to client
machines.
### Client
* Takes the IP of server and port number as input in command line.
* It spawns remote parent actor and child actors based on the processor count on the machine.
* The remote boss actor divides tasks received from the server among child actors.
* Remote child actors send the results to server parent actors.
### Running the Code
* Client machine:
dotnet fsi client.fsx serverIP serverPort
Note: This should be done before starting server
* Server machine:
dotnet fsi server.fsx N K
## Findings
* The coin with the most 0s you managed to find: 8.
* The largest number of working machines you were able to run your code with: 2.
