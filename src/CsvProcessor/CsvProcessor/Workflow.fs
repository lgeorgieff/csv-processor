module public CSV.Workflow

open CSV.Configuration
open CSV.Core.Model




let rec private createTaskTree(taskConfigurations: list<ITaskConfiguration>) =
    let startTasks: list<ITaskConfiguration> =
        List.filter(fun(taskConf: ITaskConfiguration) -> not(taskConf :? IConsumerTaskConfiguration)) taskConfigurations
    //let reaminingTasks: list<ITaskConfiguration> = 
    //    List.
    
    // TODO: remove CSV.Core.Tree?
    []

/// <summary>Models a workflow consisting of several tasks.
/// After the workflow was instantiated and all tasks were
/// successfully processed, the results of the final tasks
/// can be requested.
/// If several tasks are defined concurrently, they are executed
/// asynchronously. So it's up to you when using CSV.Tasks.GenericTask
/// and generic operations to avoid side-effects!</summary>
type public Workflow(configuration: WorkflowConfiguration) =
    new(configFile: string, workflowName: string) = Workflow(WorkflowConfiguration.Parse(configFile, workflowName))

    

            

    /// <summary>Returns the workflow configuration that this workflow
    /// instance depends on</summary>
    member public this.WorkflowConfiguration: WorkflowConfiguration = configuration
    /// <summary>A getter for requesting the workflow's name.</summary>
    member public this.WorkflowName: string = configuration.Name