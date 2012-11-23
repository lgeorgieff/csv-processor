module public Main

open System
open System.IO
open System.Xml

open CSV.Configuration
open CSV.Tasks
open CSV.Core.Model
open CSV.Core.Utilities


[<EntryPointAttribute>]
let public main(args: array<string>): int =
    if args.Length <> 1 then
        raise(new ArgumentException("expected one argument defining the configuration file"))
    
    let configuration: Configuration = Configuration.Parse args.[0]

    let readTask: Reader = new Reader((List.find(fun(taskConf: ITaskConfiguration) -> taskConf :? ReadConfiguration) configuration.Workflow) :?> ReadConfiguration, configuration.ColumnDefinitions)
    Console.WriteLine((readTask :> IGeneratorTask).Output.Length)
    
    
    Console.WriteLine("press ENTER to quit")
    Console.ReadLine() |> ignore
    0