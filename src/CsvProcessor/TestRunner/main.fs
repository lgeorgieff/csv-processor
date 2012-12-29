module public Main

open System
open System.IO
open System.Xml

open CSV.Configuration
open CSV.Tasks
open CSV.Core.Model
open CSV.Core.Utilities
open CSV.Job
open CSV.Core.Utilities.List
open CSV.Core.Utilities.String

let private makeUpperCase(line: Line): option<Line> =
    List.map(fun(cell: ICell) -> { Cell.Name = cell.Name; Cell.Value = cell.Value.ToUpper() } :> ICell) line
    |> Some

let private maskSpace(document: Lines): Lines =
    List.map(fun(line: Line) ->
        List.map(fun(cell: ICell) ->
            { Cell.Name = cell.Name; Cell.Value = cell.Value.Replace(' ', '_') } :> ICell) line) document


[<EntryPointAttribute>]
let public main(args: array<string>): int =
    if args.Length <> 1 then
        raise(new ArgumentException("expected one argument defining the configuration file"))

    GenericTask.RegisterOperation("upper-case-transform", makeUpperCase)
    GenericTask.RegisterOperation("space-transform", maskSpace)

    let job: Job = new Job(args.[0])
    Console.WriteLine("\n\n\n" + job.Results.ToString())

    Console.WriteLine("press ENTER to quit")
    Console.ReadLine() |> ignore
    0