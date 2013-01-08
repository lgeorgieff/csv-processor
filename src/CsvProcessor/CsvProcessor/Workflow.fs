module public CSV.Workflow

open CSV.Configuration
open CSV.Core.Model
open CSV.Core.Utilities.List
open CSV.Core.Exceptions
open CSV.Tasks

/// <summary>Throws an ConfigurationException when a Task is confifgured to be a
/// predeccors of multiple tasks.</summary>
let private checkForMultipleTaskReferences(taskConfigurations: list<ITaskConfiguration>): Unit =
    List.iter(fun(conf: ITaskConfiguration) ->
        if List.length(List.filter(fun(item: ITaskConfiguration) ->
            item :? IConsumerTaskConfiguration && (item :?> IConsumerTaskConfiguration).PreviousTask.IsSome && (item :?> IConsumerTaskConfiguration).PreviousTask.Value = conf.TaskName) taskConfigurations) > 1 then
                raise(new ConfigurationException("the task " + conf.TaskName + " has multiple successors"))) taskConfigurations

/// <summary>A helper function that enables creating ordered lists of task configurations
/// based on the properties "PreviousTask" and "TaskName".</summary>
let private createTaskChain(taskConfigurations: list<ITaskConfiguration>): list<ITaskConfiguration> =
    checkForMultipleTaskReferences taskConfigurations
    let startTasks: list<ITaskConfiguration> =
        List.filter(fun(taskConf: ITaskConfiguration) -> taskConf :? ReadConfiguration || (taskConf :? IConsumerTaskConfiguration && (taskConf :?> IConsumerTaskConfiguration).PreviousTask.IsNone)) taskConfigurations
    if List.length startTasks <> 1 then
        raise(new ConfigurationException("There must be exactly one task that is used as first task in a workflow"))
    let rec distributeRemainingTasks(remainingTasks: list<ITaskConfiguration>) (taskChain: list<ITaskConfiguration>): list<ITaskConfiguration> =
        if remainingTasks = [] then
            taskChain
        else
            let lastName: string = (Last taskChain).Value.TaskName
            let nextTasks: list<ITaskConfiguration> =
                List.filter(fun(conf: ITaskConfiguration) ->
                    conf :? IConsumerTaskConfiguration && (conf :?> IConsumerTaskConfiguration).PreviousTask.IsSome && (conf :?> IConsumerTaskConfiguration).PreviousTask.Value = lastName) remainingTasks
            if List.length nextTasks <> 1 then
                raise(new ConfigurationException("The task \"" + lastName + "\" requires exactly one successor task, but found: " + (List.length nextTasks).ToString()))
            distributeRemainingTasks (Remove [(List.head nextTasks)] remainingTasks) (taskChain @ nextTasks)
    distributeRemainingTasks (Remove [(List.head startTasks)] taskConfigurations) startTasks

/// <summary>Models a workflow consisting of several tasks.
/// After the workflow was instantiated and all tasks were
/// successfully processed, the results of the final tasks
/// can be requested.</summary>
type public Workflow(configuration: WorkflowConfiguration) =
    let mutable result: option<Lines> = None
    let mutable input: option<Lines> = None
    let mutable taskChain: option<list<ITask>> = None
    new(configFile: string, workflowName: string) = Workflow(WorkflowConfiguration.Parse(configFile, workflowName))

    /// <summary>Can be called explecitly to run all tasks of this workflow.</summary>
    member public this.ProcessTasks(): Unit =
        if result.IsNone then
            let lastTask: option<ITask> = 
                List.fold(fun(prev: option<ITask>) (current: ITask) ->
                    if prev.IsSome then
                        if not((prev.Value) :? IGeneratorTask) then
                            raise(new WorkflowException("The task \"" + prev.Value.TaskName + "\" is used as a generator task but not declared as such"))
                        if not(current :? IConsumerTask) then
                            raise(new WorkflowException("The task \"" + current.TaskName + "\" is used as a consumer task but not declared as such"))
                        (current :?> IConsumerTask).Input <- (prev.Value :?> IGeneratorTask).Output
                    elif prev.IsNone && input.IsSome && not(current :? IConsumerTask) then
                        raise(new WorkflowException("The task \"" + current.TaskName + "\" is used as a consumer task but not declared as such"))
                    elif prev.IsNone && input.IsSome && current :? IConsumerTask then
                        (current :?> IConsumerTask).Input <- input.Value
                    Some current) None this.TaskChain
            if lastTask.IsSome && lastTask.Value :? IGeneratorTask then
                result <- Some((lastTask.Value :?> IGeneratorTask).Output)
            else
                result <- Some([])

    /// <summary>Returns the final result of this workflow. If there is no result,
    /// e.g. the last task is a WriterTask, the result is none.</summary>
    member public this.Output: option<Lines> = this.ProcessTasks()
                                               result

    /// <summary>Instantiates all tasks defined by the passed workflow configuration.</summary>
    member private this.TaskChain: list<ITask> =
        if taskChain.IsSome then
            taskChain.Value
        else
            taskChain <-
                createTaskChain (configuration.Workflow)
                |> List.map(fun(conf: ITaskConfiguration) ->
                    match conf with
                        | :? WriteConfiguration -> new Writer(conf :?> WriteConfiguration) :> ITask
                        | :? ReadConfiguration -> new Reader(conf :?> ReadConfiguration, configuration.ColumnDefinitions) :> ITask
                        | :? GenericTaskConfiguration -> new GenericTask(conf :?> GenericTaskConfiguration) :> ITask
                        | _ -> raise(new WorkflowException("The workflow does not support configurations of the type: " + conf.GetType().ToString())))
                |> Some
            taskChain.Value

    /// <summary>Returns the workflow configuration that this workflow
    /// instance depends on</summary>
    member public this.WorkflowConfiguration: WorkflowConfiguration = configuration

    /// <summary>A getter for requesting the workflow's name.</summary>
    member public this.WorkflowName: string = configuration.Name

    /// <summary>A getter for requesting the workflow predecessors of this workflows.</summary>
    member public this.PreviousWorkflows: list<string> = configuration.PreviousWorkflows

    /// <summary>Realizes a setter that takes the input data of a previous workflows to be processed
    /// by this workflow.
    /// Note: the passed values must all correspond to the column definitions, whereas the
    /// order does not care</summary>
    member public this.Input with set(value: list<Lines>) = if input.IsSome then
                                                                raise(new PropertyAlreadySetException("The property Input can be set only ones"))
                                                            let mergedLines: Lines = MergeLines value
                                                            CheckLinesForColumnDefinitions mergedLines configuration.ColumnDefinitions false
                                                            input <- Some mergedLines

/// <summary>A typedef for a list of workflows.</summary>
type public Workflows = list<Workflow>

/// <summary>a typedef for a list of lists ofr workflows.</summary>
type public WorkflowChain = list<list<Workflow>>

/// <summary>Returns a list of Workflow instances corresponding to the
/// configuration file described by the passed file path.</summary>
let public GetWorkflows(configFilePath: string): Workflows =
    WorkflowConfiguration.Parse configFilePath
    |> List.map(fun(conf: WorkflowConfiguration) -> new Workflow(conf))