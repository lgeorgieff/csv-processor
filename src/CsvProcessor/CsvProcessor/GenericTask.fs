namespace CSV.Tasks

open System
open System.Collections.Generic

open CSV.Configuration
open CSV.Core.Model
open CSV.Core.Exceptions

/// <summary>This module contains all registered operations that can be used
/// by GenericTasks instances.</summary>
module private GenericTaskOperations =
    /// <summary>A dicitonary that contains a string identifier bound to a function
    /// representing an operation for a GenericTask.</summary>
    let public RegisteredOperations: Dictionary<string, Line -> option<Line>>  = new Dictionary<string, Line -> option<Line>>()

/// <summary>Implements a generic task and invokes predefined and
/// registered operations on the set input. Before setting the input of
/// a GenericTask object, it is necessary to register the corresponding
/// operation, otherwise a GenericOperationException is thrown.</summary>
type public GenericTask(configuration: GenericTaskConfiguration) =
    let mutable input: option<Lines> = None
    let mutable output: option<Lines> = None
    
    // all methods that are needed for registering, derigestering and
    // requesting an operation
    static member public IsRegistered(identifier: string): bool =
        GenericTaskOperations.RegisteredOperations.ContainsKey(identifier)
    static member public IsRegistered(operation: Line -> option<Line>) =
        GenericTaskOperations.RegisteredOperations.ContainsValue(operation)
    static member public RegisterOperation(identifier: string) (operation: Line -> option<Line>): Unit =
        if GenericTask.IsRegistered identifier then
            raise(new GenericOperationException("An operation for the identifier " + identifier + " is already registered!"))
        if GenericTask.IsRegistered operation then
            raise(new GenericOperationException("This operation is already registered!"))
        GenericTaskOperations.RegisteredOperations.Add(identifier, operation)
    static member public GetOperation(identifier: string): Line -> option<Line> =
        GenericTaskOperations.RegisteredOperations.[identifier]

    /// <summary>A helper method for applying the registered operation
    /// of this task on all lines.</summary>
    member private this.Operate(identifier: string) (lines: Lines): Lines = 
        let operation: Line -> option<Line> = GenericTask.GetOperation identifier
        let rec operateOnLines(iLines: Lines) (accumulator: Lines): Lines =
            match iLines with
                | [] -> accumulator
                | _ -> let lineResult: option<Line> = operation (List.head iLines)
                       if lineResult.IsNone then
                          operateOnLines (List.tail iLines) accumulator
                       else
                          operateOnLines (List.tail iLines) (accumulator @ [lineResult.Value])
        operateOnLines lines []
    interface IConsumerTask with
        member this.PreviousTask: string = configuration.PreviousTask
        member this.Input with set(value: Lines) = if input.IsSome then
                                                    raise(new PropertyAlreadySetException("The property Input can be set only ones"))
                                                   input <- Some value
                                                   output <- Some(this.Operate configuration.Operation (input.Value))
    interface IGeneratorTask with
        member this.Output: Lines = if output.IsNone then
                                        raise(new PropertyNotSetException("The property Output was not set yet. Set the Input property before requesting the Output!"))
                                    else
                                        output.Value
    interface ITask with
        member this.TaskConfiguration: ITaskConfiguration = configuration :> ITaskConfiguration
        member this.TaskName: string = configuration.TaskName