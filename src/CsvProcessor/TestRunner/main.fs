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


[<EntryPointAttribute>]
let public main(args: array<string>): int =
    if args.Length <> 1 then
        raise(new ArgumentException("expected one argument defining the configuration file"))

    GenericTask.RegisterOperation("country = usa", (fun(line: Line) ->
        if (List.tryFind(fun(cell: ICell) -> cell.Name = "country" && cell.Value.ToLower() = "usa") line).IsSome then
            Some line
        else
            None))
    GenericTask.RegisterOperation("line position % 2 = 0", (fun(lines: Lines) ->
        FilterEachSecond (List.filter(fun(line: Line) -> not(IsHeaderLine line true)) lines) true))

    let workflows: list<Workflow> = GetWorkflows args.[0]
    (List.head workflows).ProcessTasks()

    Console.WriteLine("press ENTER to quit")
    Console.ReadLine() |> ignore
    0