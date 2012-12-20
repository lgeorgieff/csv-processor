namespace CSV.Tasks

open System
open System.IO

open CSV.Configuration
open CSV.Core.Model
open CSV.Core.Utilities
open CSV.Core.Exceptions
open CSV.Core.Utilities.String
open CSV.Core.Utilities.IO

/// <summary>Implements the Read task and reads and splits a CVS file into memory.
/// The result can be requested by calling the getter Output</summary>
type public Reader(configuration: ReadConfiguration, columnDefinitions: list<ColumnDefinition>) as self =
    let mutable output: Lines = []   
    let lineProcessor(line: string) (lineIsHeader: bool): Line =
        let parts: list<string> = line.SplitLine(configuration.Split, configuration.Quote, configuration.MetaQuote)
        if parts.Length <> List.length columnDefinitions then
            raise(new TaskException("The number of columns does not match the number of column definitions for this line: " + line))
        List.map(fun(part: string) -> part.Trim(configuration.TrimWhitepsaceStart, configuration.TrimWhitespaceEnd)) parts
        |> List.zip columnDefinitions
        |> List.map(fun(colDef: ColumnDefinition, value: string) ->
            if lineIsHeader then
                { HeaderCell.Name = colDef.Name
                  HeaderCell.Value = value
                } :> ICell
            else
                { Cell.Name = colDef.Name
                  Cell.Value = value
                } :> ICell)
    do
        self.ReadFile()
    member private this.ReadFile(): Unit =
        use fs: StreamReader = new StreamReader(File.OpenRead(configuration.FilePath))
        output <- ReadStream fs lineProcessor |> List.filter(fun(line: Line) -> not(IsHeaderLine line false))
    interface ITask with
        override this.TaskName: string = configuration.TaskName
        override this.TaskConfiguration: ITaskConfiguration = configuration :> ITaskConfiguration
    interface IGeneratorTask with
        override this.Output: Lines = output