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
            use destinationWriter: StreamWriter = this.OpenDestination destination
            let rec write(lines: Lines): Unit =
                if lines <> [] then
                    let resultingLine: string = this.LineToString(List.head lines)
                    if resultingLine <> "" then // ignore empty lines
                        destinationWriter.WriteLine(resultingLine)
                    write (List.tail lines)
            let headerLine: string = this.GenerateHeaderLine configuration.ColumnMappings
            if headerLine <> "" then
                // write the new header line corresponding to the column mapping
                destinationWriter.WriteLine(headerLine)
            write (input.Value)

    /// <summary>Opens either the destination file or the std stream and
    /// wrapps it in a StreamWriter instance.</summary>
    member private this.OpenDestination(destination: option<string>): StreamWriter =
        if destination.IsNone then
            new StreamWriter(Console.OpenStandardOutput())
        else
            let cfc: bool * option<string> = this.CheckFileModeConstraints()
            if fst cfc then
                new StreamWriter(File.Open(destination.Value, configuration.FileMode))                                                
            else
                raise(new ConfigurationException("The results cannot be appended to the destination since the existing header \"" + (snd cfc).Value + "\" does not match the current header \"" + (this.GenerateHeaderLine configuration.ColumnMappings) + "\""))

    /// <summary>Generates a header line based on the column mappings and returns it's
    /// textual representation.</summary>
    member private this.GenerateHeaderLine(columnMappings: ColumnMappings): string =
        let headerLine: Line = List.sortBy(fun(mapping: ColumnMapping) -> mapping.Target.Index) columnMappings
                               |> List.map(fun(mapping: ColumnMapping) -> { HeaderCell.Name = mapping.Target.Name
                                                                            HeaderCell.Value = mapping.Target.Name
                                                                          } :> ICell )
        Writer.Stringfy(headerLine, configuration.Split, configuration.Quote, configuration.MetaQuote, configuration.TrimWhitespaceStart, configuration.TrimWhitespaceEnd)

    /// <summary>Returns the header line as string of the file identified by the passed
    /// path. If the file does not exist, is empty or contains only whitespace
    /// None is returned.</summary>
    member private this.FileContainsHeader(filePath: string): option<string> =
        if File.Exists(filePath) then
            let reader: StreamReader = new StreamReader(filePath)
            let rec readHeaderLine(): option<string> =
                if reader.EndOfStream then
                    None
                else
                    let line: string = reader.ReadLine().Trim()
                    if line.Length = 0 then
                        readHeaderLine()
                    else
                        Some line
            readHeaderLine()
        else
            None

    /// <summary>The method returns a tuple containing a boolean and a string within an option.
    /// If the file that already exists and that the text will be appended to, corresponds
    /// to the defined header file of this writer, the result is (true, None). Otherwise
    /// the result is (false, Some<Header line of the existing file>).
    /// Note, if the file does not exists, is empty or contains only whitespace
    /// the result is also (true, None).
    /// Note, if the configured file mode is not set to Append, the result is (true, None).</summary>
    member private this.CheckFileModeConstraints(): bool * option<string> =
        if configuration.FileMode = FileMode.Append && configuration.FilePath.IsSome && (this.FileContainsHeader configuration.FilePath.Value).IsSome then
            if (this.FileContainsHeader configuration.FilePath.Value).Value = (this.GenerateHeaderLine configuration.ColumnMappings).Trim() then
                (true, None)
            else
                (false, this.FileContainsHeader configuration.FilePath.Value)
        else
            (true, None)
    
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