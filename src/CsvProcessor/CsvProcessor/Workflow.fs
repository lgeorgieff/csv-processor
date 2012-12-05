module public CSV.Workflow

open CSV.Configuration
open CSV.Core.Model
open CSV.Core.Utilities.List
open CSV.Core.Exceptions
open CSV.Tasks

/// <summary>Throws an ConfigurationExcdetion when a Task is confifgured to be a
/// predeccors of multiple tasks.</summary>
let private checkForMultipleTaskReferences(taskConfigurations: list<ITaskConfiguration>): Unit =
    List.iter(fun(conf: ITaskConfiguration) ->
        if List.length(List.filter(fun(item: ITaskConfiguration) ->
            item :? IConsumerTaskConfiguration && (item :?> IConsumerTaskConfiguration).PreviousTask = conf.TaskName) taskConfigurations) > 1 then
                raise(new ConfigurationException("the task " + conf.TaskName + " has multiple successors"))) taskConfigurations

/// <summary>A helper function that enables creating ordered lists of task configurations
/// based on the properties "PreviousTask" and "TaskName".</summary>
let private createTaskChain(taskConfigurations: list<ITaskConfiguration>): list<ITaskConfiguration> =
    checkForMultipleTaskReferences taskConfigurations
    let startTasks: list<ITaskConfiguration> =
        List.filter(fun(taskConf: ITaskConfiguration) -> not(taskConf :? IConsumerTaskConfiguration)) taskConfigurations
    if List.length startTasks <> 1 then
        raise(new ConfigurationException("There must be exactly one task that is used as first task in a workflow"))
    let rec distributeRemainingTasks(remainingTasks: list<ITaskConfiguration>) (taskChain: list<ITaskConfiguration>): list<ITaskConfiguration> =
        if remainingTasks = [] then
            taskChain
        else
            let lastName: string = (Last taskChain).Value.TaskName
            let nextTasks: list<ITaskConfiguration> =
                List.filter(fun(conf: ITaskConfiguration) ->
                    conf :? IConsumerTaskConfiguration && (conf :?> IConsumerTaskConfiguration).PreviousTask = lastName) remainingTasks
            if List.length nextTasks <> 1 then
                raise(new ConfigurationException("The task \"" + lastName + "\" requires exactly one successor task, but found: " + (List.length nextTasks).ToString()))
            distributeRemainingTasks (Remove (List.head nextTasks) remainingTasks) (taskChain @ nextTasks)
    distributeRemainingTasks (Remove (List.head startTasks) taskConfigurations) startTasks

/// <summary>Models a workflow consisting of several tasks.
/// After the workflow was instantiated and all tasks were
/// successfully processed, the results of the final tasks
/// can be requested.</summary>
type public Workflow(configuration: WorkflowConfiguration) =
    let mutable result: option<Lines> = None
    let mutable input: option<list<Lines>> = None
    new(configFile: string, workflowName: string) = Workflow(WorkflowConfiguration.Parse(configFile, workflowName))

    /// <summary>Can be called explecitly to run all tasks of this workflow.</summary>
    member private this.ProcessTasks(): Unit =
        let lastTask: option<ITask> = 
            List.fold(fun(prev: option<ITask>) (current: ITask) ->
                if prev.IsSome then
                    if not((prev.Value) :? IGeneratorTask) then
                        raise(new WorkflowException("The task \"" + prev.Value.TaskName + "\" is used as a generator task but not declared as such"))
                    if not(current :? IConsumerTask) then
                        raise(new WorkflowException("The task \"" + current.TaskName + "\" is used as a consumer task but not declared as such"))
                    (current :?> IConsumerTask).Input <- (prev.Value :?> IGeneratorTask).Output
                Some current) None this.TaskChain
        if lastTask.IsSome && lastTask.Value :? IGeneratorTask then
            result <- Some((lastTask.Value :?> IGeneratorTask).Output)

    /// <summary>Returns the final result of this workflow. If there is no result,
    /// e.g. the last task is a WriterTask, the result is none.</summary>
    member public this.Output = if result.IsNone then
                                    this.ProcessTasks()
                                result

    /// <summary>Instantiates all tasks defined by the passed workflow configuration.</summary>
    member private this.TaskChain: list<ITask> =
        createTaskChain (configuration.Workflow)
        |> List.map(fun(conf: ITaskConfiguration) ->
            match conf with
                | :? WriteConfiguration -> new Writer(conf :?> WriteConfiguration) :> ITask
                | :? ReadConfiguration -> new Reader(conf :?> ReadConfiguration, configuration.ColumnDefinitions) :> ITask
                | :? GenericTaskConfiguration -> new GenericTask(conf :?> GenericTaskConfiguration) :> ITask
                | _ -> raise(new WorkflowException("The workflow does not support configurations of the type: " + conf.GetType().ToString())))

    /// <summary>Returns the workflow configuration that this workflow
    /// instance depends on</summary>
    member public this.WorkflowConfiguration: WorkflowConfiguration = configuration

    /// <summary>A getter for requesting the workflow's name.</summary>
    member public this.WorkflowName: string = configuration.Name

    /// <summary>A getter for requesting the workflow predecessors of this workflows.</summary>
    member public this.PreviousWorkflows: list<string> = configuration.PreviousWorkflows

    /// <summary>Realizes a setter that takes the input data of a previous workflows to be processed
    /// by this workflow. The processing of the input data is directly started when the 
    /// property is set.</summary>
    member public this.Input with set(value: list<Lines>) = if input.IsSome then
                                                                raise(new PropertyAlreadySetException("The property Input can be set only ones"))
                                                            input <- Some value
                                                            this.ProcessTasks()

/// <summary>Returns a list of Workflow instances corresponding to the
/// configuration file described by the passed file path.</summary>
let public GetWorkflows(configFilePath: string): list<Workflow> =
    WorkflowConfiguration.Parse configFilePath
    |> List.map(fun(conf: WorkflowConfiguration) -> new Workflow(conf))