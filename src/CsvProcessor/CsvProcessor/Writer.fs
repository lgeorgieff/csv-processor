namespace CSV.Tasks

open System
open System.IO

open CSV.Configuration
open CSV.Core.Model
open CSV.Core.Utilities
open CSV.Core.Exceptions
open CSV.Core.Utilities.String

/// <summary>Implements the Write task and writes the passed data into a stream.
/// The input can be set by calling the setter Input</summary>
type public Writer(configuration: WriteConfiguration) =
    let mutable input: option<Lines> = None

    /// <summary>Writes the data into the actual destination.</summary>
    member private this.Write(destination: option<string>): Unit =
        if input.IsSome then
            use destinationWriter: StreamWriter = if destination.IsNone then
                                                    new StreamWriter(Console.OpenStandardOutput())
                                                  else
                                                    new StreamWriter(File.Open(destination.Value, configuration.FileMode))                                                
            let rec write(lines: Lines): Unit =
                if lines <> [] then
                    let resultingLine: string = this.LineToString(List.head lines)
                    if resultingLine <> "" then // ignore empty lines
                        destinationWriter.WriteLine(resultingLine)
                    write (List.tail lines)
            let headerLine: string = this.GenerateHeaderLine configuration.ColumnMappings
            if headerLine <> "" then
                // write the new heder line corresponding to the column mapping
                destinationWriter.WriteLine(headerLine)
            write (input.Value)

    /// <summary>Generates a header line based on the column mappings and returns it's
    /// textual representation.</summary>
    member private this.GenerateHeaderLine(columnMappings: ColumnMappings): string =
        let headerLine: Line = List.sortBy(fun(mapping: ColumnMapping) -> mapping.Target.Index) columnMappings
                               |> List.map(fun(mapping: ColumnMapping) -> { HeaderCell.Name = mapping.Target.Name
                                                                            HeaderCell.Value = mapping.Target.Name
                                                                          } :> ICell )
        Writer.Stringfy(headerLine, configuration.Split, configuration.Quote, configuration.MetaQuote, configuration.TrimWhitespaceStart, configuration.TrimWhitespaceEnd)
    
    /// <summary>Generates a string from the passed line that corresponds to this
    /// instance's configuration.</summary>
    static member private Stringfy((line: Line), (split: char), (quote: char), (metaQuote: char), (trimWhitespaceStart: bool), (trimWhitespaceEnd: bool)): string =
        List.fold(fun(constructedLine: string) (cell: ICell) ->
            let valueToAdd: string = cell.Value.Trim(trimWhitespaceStart, trimWhitespaceEnd).QuoteIf(quote, metaQuote, fun(item: string) -> item.Contains(split.ToString()))
            if constructedLine = "" then
                valueToAdd
            else
                constructedLine + split.ToString() + valueToAdd) "" line

    /// <summary>Returns a string representing the passed instance of Line.</summary>
    static member public LineToString((line: Line), (split: char), (quote: char), (metaQuote: char), (trimWhitespaceStart: bool), (trimWhitespaceEnd: bool), (columnMappings: ColumnMappings)): string =
        let constructtedLine: Line =
            List.filter(fun(cell: ICell) ->
                List.exists(fun(mapping: ColumnMapping) ->
                        mapping.Source.Name = cell.Name) columnMappings) line
                |> List.sortBy(fun(cell: ICell) ->
                    (List.find(fun(mapping: ColumnMapping) ->
                        mapping.Source.Name = cell.Name) columnMappings).Target.Index)
        Writer.Stringfy(constructtedLine, split, quote, metaQuote, trimWhitespaceStart, trimWhitespaceEnd)

    /// <summary>Returns a string representing the passed instance of Line. All arguments, such as
    /// quote, split, meta-quote and columnMappings are taken of this object's configuration.</summary>
    member public this.LineToString(line: Line): string =
        Writer.LineToString(line, configuration.Split, configuration.Quote, configuration.MetaQuote, configuration.TrimWhitespaceStart, configuration.TrimWhitespaceEnd, configuration.ColumnMappings)
    interface ITask with        
        override this.TaskName: string = configuration.TaskName
        override this.TaskConfiguration: ITaskConfiguration = configuration :> ITaskConfiguration
    interface IConsumerTask with
        override this.PreviousTask: string = configuration.PreviousTask
        override this.Input with set(value: Lines) = if input.IsSome then
                                                        raise(new PropertyAlreadySetException("The property Input can be set only ones"))
                                                     input <- Some value
                                                     this.Write(configuration.FilePath)