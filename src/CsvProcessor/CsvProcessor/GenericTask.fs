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
    let public RegisteredLineOperations: Dictionary<string, Line -> option<Line>>  = new Dictionary<string, Line -> option<Line>>()
    let public RegisteredDocumentOperations: Dictionary<string, Lines -> Lines> = new Dictionary<string, Lines -> Lines>()

/// <summary>Implements a generic task and invokes predefined and
/// registered operations on the set input. Before setting the input of
/// a GenericTask object, it is necessary to register the corresponding
/// operation, otherwise a GenericOperationException is thrown.</summary>
type public GenericTask(configuration: GenericTaskConfiguration) =
    let mutable input: option<Lines> = None
    let mutable output: option<Lines> = None
    
    // all methods that are needed for registering, derigestering and
    // requesting an operation
    ///<summary>Returns true if the passed identifier is registered as a LineOpertion
    /// or as a DocumentOpertion. Otherwise the return value is false.</summary>
    static member public IsRegistered(identifier: string): bool =
        GenericTaskOperations.RegisteredLineOperations.ContainsKey(identifier) || GenericTaskOperations.RegisteredDocumentOperations.ContainsKey(identifier)
    /// <summary>Returns true if the passed operation is registered as a
    /// line operation. Otherwise the return value is false.</summary>
    static member public IsRegistered(lineOperation: Line -> option<Line>): bool =
        GenericTaskOperations.RegisteredLineOperations.ContainsValue(lineOperation)
    /// <summary>Returns true if the passed operation is registered as a
    /// document operation. Otherwise the return value is false.</summary>
    static member public IsRegistered(documentOperation: Lines -> Lines): bool =
        GenericTaskOperations.RegisteredDocumentOperations.ContainsValue(documentOperation)
    /// <summary>Registers the passed operation as a line operation.</summary>
    static member public RegisterOperation((identifier: string),(lineOperation: Line -> option<Line>)): Unit =
        if GenericTask.IsRegistered identifier then
            raise(new GenericOperationException("An operation for the identifier " + identifier + " is already registered!"))
        if GenericTask.IsRegistered lineOperation then
            raise(new GenericOperationException("This operation is already registered!"))
        GenericTaskOperations.RegisteredLineOperations.Add(identifier, lineOperation)
    /// <summary>Registers the passed operation as a document operation.</summary>
    static member public RegisterOperation((identifier: string), (documentOperation: Lines -> Lines)): Unit =
        if GenericTask.IsRegistered identifier then
            raise(new GenericOperationException("An operation for the identifier " + identifier + " is already registered!"))
        if GenericTask.IsRegistered documentOperation then
            raise(new GenericOperationException("This operation is already registered!"))
        GenericTaskOperations.RegisteredDocumentOperations.Add(identifier, documentOperation)
    /// <summary>Returns a registered line operation.</summary>
    static member public GetLineOperation(identifier: string): Line -> option<Line> =
        GenericTaskOperations.RegisteredLineOperations.[identifier]
    /// <summary>Returns a registered document operation.</summary>
    static member public GetDocumentOperation(identifier: string): Lines -> Lines =
        GenericTaskOperations.RegisteredDocumentOperations.[identifier]

    /// <summary>Returns true if this task operates on lines. Otherwise, if this
    /// task operates on the entire document, the return values is false.</summary>
    member public this.IsLineOperation: bool = configuration.LineOperation.IsSome

    /// <summary>A helper method for applying the registered operation
    /// of this task line by line.</summary>
    member private this.OperateLineByLine(identifier: string) (lines: Lines): Lines = 
        let operation: Line -> option<Line> = GenericTask.GetLineOperation identifier
        let rec operateOnLines(iLines: Lines) (accumulator: Lines): Lines =
            match iLines with
                | [] -> accumulator
                | _ -> let lineResult: option<Line> = operation (List.head iLines)
                       if lineResult.IsNone then
                          operateOnLines (List.tail iLines) accumulator
                       else
                          operateOnLines (List.tail iLines) (accumulator @ [lineResult.Value])
        operateOnLines lines []
    /// <summary>A helper method for applying the registered operation
    /// of this task on hte entire document at once.</summary>
    member private this.OperationEntireDocument(identifier: string) (lines: Lines): Lines =
        (GenericTask.GetDocumentOperation identifier) lines

    interface IConsumerTask with
        member this.PreviousTask: string = configuration.PreviousTask
        member this.Input with set(value: Lines) = if input.IsSome then
                                                    raise(new PropertyAlreadySetException("The property Input can be set only ones"))
                                                   input <- Some value
                                                   output <- Some(if this.IsLineOperation then
                                                                    this.OperateLineByLine configuration.LineOperation.Value (input.Value)
                                                                  else
                                                                    this.OperationEntireDocument configuration.DocumentOperation.Value (input.Value))
    interface IGeneratorTask with
        member this.Output: Lines = if output.IsNone then
                                        raise(new PropertyNotSetException("The property Output was not set yet. Set the Input property before requesting the Output!"))
                                    else
                                        output.Value
    interface ITask with
        member this.TaskConfiguration: ITaskConfiguration = configuration :> ITaskConfiguration
        member this.TaskName: string = configuration.TaskName