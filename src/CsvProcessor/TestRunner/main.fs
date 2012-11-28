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
    
    let genericTask: GenericTask = new GenericTask((List.find(fun(taskConf: ITaskConfiguration) -> taskConf :? GenericTaskConfiguration) configuration.Workflow) :?> GenericTaskConfiguration)
    let genericOperation(line :Line): option<Line> =
        if (List.tryFind(fun(cell: ICell) -> cell.Name = "country" && cell.Value.ToLower() = "usa") line).IsSome then
            Some line
        else
            None
    GenericTask.RegisterOperation "country = usa" genericOperation
    (genericTask :> IConsumerTask).Input <- (readTask :> IGeneratorTask).Output

    let writeTask: Writer= new Writer((List.find(fun(taskConf: ITaskConfiguration) -> taskConf :? WriteConfiguration) configuration.Workflow) :?> WriteConfiguration)
    (writeTask :> IConsumerTask).Input <- (genericTask :> IGeneratorTask).Output

    

    Console.WriteLine("press ENTER to quit")
    Console.ReadLine() |> ignore
    0