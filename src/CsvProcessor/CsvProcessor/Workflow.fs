module public CSV.Workflow

open CSV.Configuration


/// <summary>Models a workflow consisting of several tasks.
/// After the workflow was instantiated and all tasks were
/// successfully processed, the results of the final tasks
/// can be requested.
/// If several tasks are defined concurrently, they are executed
/// asynchronously. So it's up to you when using CSV.Tasks.GenericTask
/// and generic operations to avoid side-effects!</summary>
type public Workflow(configuration: WorkflowConfiguration) =

    /// <summary>Returns the workflow configuration that this workflow
    /// instance depends on</summary>
    member public this.WorkflowConfiguration: WorkflowConfiguration = configuration