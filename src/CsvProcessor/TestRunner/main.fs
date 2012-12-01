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
    
    let configurations: list<WorkflowConfiguration> = WorkflowConfiguration.Parse args.[0]
    let configuration: WorkflowConfiguration = List.head configurations

    let readTask: Reader = new Reader((List.find(fun(taskConf: ITaskConfiguration) -> taskConf :? ReadConfiguration) configuration.Workflow) :?> ReadConfiguration, configuration.ColumnDefinitions)
    Console.WriteLine((readTask :> IGeneratorTask).Output.Length)
    
    let genericTask1: GenericTask = new GenericTask((List.find(fun(taskConf: ITaskConfiguration) -> taskConf.TaskName = "usa filter") configuration.Workflow) :?> GenericTaskConfiguration)
    let genericTask2: GenericTask = new GenericTask((List.find(fun(taskConf: ITaskConfiguration) -> taskConf.TaskName = "even row filter") configuration.Workflow) :?> GenericTaskConfiguration)
    let lineOperation(line :Line): option<Line> =
        if (List.tryFind(fun(cell: ICell) -> cell.Name = "country" && cell.Value.ToLower() = "usa") line).IsSome then
            Some line
        else
            None
    let documentOperation(lines: Lines): Lines =
        CSV.Core.Utilities.List.FilterEachSecond (List.filter(fun(line: Line) -> not(IsHeaderLine line true)) lines) true

    GenericTask.RegisterOperation("country = usa", lineOperation)
    GenericTask.RegisterOperation("line position % 2 = 0", documentOperation)
    (genericTask1 :> IConsumerTask).Input <- (readTask :> IGeneratorTask).Output
    (genericTask2 :> IConsumerTask).Input <- (genericTask1 :> IGeneratorTask).Output

    let writeTask: Writer= new Writer((List.find(fun(taskConf: ITaskConfiguration) -> taskConf :? WriteConfiguration) configuration.Workflow) :?> WriteConfiguration)
    (writeTask :> IConsumerTask).Input <- (genericTask2 :> IGeneratorTask).Output

    

    Console.WriteLine("press ENTER to quit")
    Console.ReadLine() |> ignore
    0