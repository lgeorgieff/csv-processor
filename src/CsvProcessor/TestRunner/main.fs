module public Main

open System
open System.IO
open System.Xml

open CSV.Configuration
open CSV.Tasks
open CSV.Core.Model
open CSV.Core.Utilities
open CSV.Workflow
open CSV.Core.Utilities.List
open CSV.Core.Utilities.String


[<EntryPointAttribute>]
let public main(args: array<string>): int =
    if args.Length <> 1 then
        raise(new ArgumentException("expected one argument defining the configuration file"))

    let workflows: list<Workflow> = GetWorkflows args.[0]
    (List.head workflows).ProcessTasks()

    Console.WriteLine("press ENTER to quit")
    Console.ReadLine() |> ignore
    0