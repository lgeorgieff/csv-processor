namespace Tasks

open System
open System.IO

open CSV.Configuration
open CSV.Core.Model
open CSV.Core.Utilities
open CSV.Core.Exceptions

type public Writer(configuration: WriteConfiguration, columnDefinitions: list<ColumnDefinition>) =
    let mutable output: Lines = []
    let mutable input: option<Lines> = None


    interface ITask with        
        override this.TaskName: string = configuration.TaskName
    interface IConsumerTask with
        override this.PreviousTask: string = configuration.PreviousTask
        override this.Input with set(value: Lines) = if input.IsSome then
                                                        raise(new PropertyAlreadySetException("The property Input can be set only ones"))
                                                     input <- Some value