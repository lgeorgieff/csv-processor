namespace CSV.Tasks

open System
open System.IO

open CSV.Configuration
open CSV.Core.Model
open CSV.Core.Utilities
open CSV.Core.Exceptions
open CSV.Core.Utilities.String

/// <summary>Implements the Read task and reads and splits a CVS file into memory.
/// The result can be requested by calling the getter Output</summary>
type public Reader(configuration: ReadConfiguration, columnDefinitions: list<ColumnDefinition>) as self =
    let mutable output: Lines = []

    /// <summary>Parses a CSV line from a single line of a file.</summary>
    let lineProcessor(line: string): Line =
        let parts: list<string> = line.SplitLine(configuration.Split, configuration.Quote, configuration.MetaQuote)
        if parts.Length <> List.length columnDefinitions then
            raise(new TaskException("The number of columns does not match the number of column definitions for this line: " + line))
        List.map(fun(part: string) -> part.Trim(configuration.TrimWhitepsaceStart, configuration.TrimWhitespaceEnd)) parts
        |> List.zip columnDefinitions
        |> List.map(fun(colDef: ColumnDefinition, value: string) ->
                { Cell.Name = colDef.Name
                  Cell.Value = value
                } :> ICell)

    /// <summary>Parses a CSV line from potentially multiple lines of a file.</summary>
    let rec multiLineProcessor(reaminingLines: list<string>) (accumulator: Lines) (currentLine: option<string>): Lines =
        if reaminingLines = [] && currentLine.IsSome then
            accumulator @ [lineProcessor currentLine.Value]
        elif reaminingLines = [] && currentLine.IsNone then
            accumulator
        else
            let nextCurrentLine: string = if currentLine.IsSome then
                                            currentLine.Value + "\n" + (List.head reaminingLines)
                                          else
                                            List.head reaminingLines
            let indexesOfSplits: list<int> =
                nextCurrentLine.IndexesOfUnquoted(configuration.Split, configuration.Quote, configuration.MetaQuote)
            let numberOfQuotes: int =
                nextCurrentLine.IndexesOfUnquoted(configuration.Quote, configuration.MetaQuote).Length
            if indexesOfSplits.Length + 1 > columnDefinitions.Length && numberOfQuotes % 2 = 0 then
                raise(new ParseException("The number of columns does not match the number of column definitions for this line: " + nextCurrentLine))
            elif indexesOfSplits.Length + 1 = columnDefinitions.Length && numberOfQuotes % 2 = 0 then
                multiLineProcessor (List.tail reaminingLines) (accumulator @ [lineProcessor nextCurrentLine]) None
            else
                multiLineProcessor (List.tail reaminingLines) accumulator (Some nextCurrentLine)

    do
        self.ReadFile()

    member private this.ReadFile(): Unit =
        let linesOfFileWithoutHeader: list<string> =
            let tmp: list<string> = File.ReadAllLines(configuration.FilePath) |> List.ofArray
            if tmp.Length = 0 then
                tmp
            else
                List.tail tmp
        output <-
            (try
                if configuration.ReadMultiLine then
                    multiLineProcessor linesOfFileWithoutHeader [] None
                else
                    List.map lineProcessor linesOfFileWithoutHeader
                    |> List.filter(fun(line: Line) -> not(IsHeaderLine line false))
             with
             | _ as err -> raise(new ParseException("A CSV line of the file \"" + configuration.FilePath + "\" could not be parsed", err)))

    interface ITask with
        override this.TaskName: string = configuration.TaskName
        override this.TaskConfiguration: ITaskConfiguration = configuration :> ITaskConfiguration

    interface IGeneratorTask with
        override this.Output: Lines = output