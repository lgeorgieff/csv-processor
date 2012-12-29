module public CSV.Job

open CSV.Workflow
open CSV.Core.Model
open CSV.Core.Exceptions
open CSV.Core.Utilities.List

/// <summary>Returns a workflow from the passed workflow chain within an option
/// that matches the passed workflow name or None if no such workflow can be
/// found.</summary>
let private tryFindWorkflow(name: string) (workflows: WorkflowChain): option<Workflow> =
    List.fold(fun(lst: Workflows) (item: Workflows) -> lst @ item) [] workflows
    |> List.tryFind(fun(wf: Workflow) -> wf.WorkflowName = name)
        
/// <summary>Returns a workflow list containing the workflows that are
/// only dependent on the previous workflows and so can be used
/// as next chain link.</summary>
let private findNextWorkflows(remainingWorkflows: Workflows) (previousWorkflows: WorkflowChain): Workflows =
    List.filter(fun(wf: Workflow) ->
        List.map(fun(wfName: string) -> tryFindWorkflow wfName previousWorkflows) wf.PreviousWorkflows
        |> List.filter Option.isSome
        |> List.length = List.length wf.PreviousWorkflows) remainingWorkflows

/// <summary>Returns an ordered list of lists containing all workflows
/// organized as a chain of workflows, i.e. the first inner list contains
/// workflows that are not dependent on another workflow, the second
/// inner list contains workflows that depends on workflows contained
/// in the first inner list, ...</summary>
let rec private generateWorkflowChain(workflows: Workflows) (previousWorkflows: WorkflowChain): WorkflowChain =
    match workflows, previousWorkflows with
    | ([], _) -> previousWorkflows
    | (_, []) -> let initialWorkflows: Workflows =
                    List.filter(fun(workflow: Workflow) -> workflow.PreviousWorkflows = []) workflows
                 if initialWorkflows.Length = 0 then
                    raise(new WorkflowException("No workflows found as initial workflows, i.e. not depending on other workflows!"))
                 generateWorkflowChain (Remove initialWorkflows workflows) [initialWorkflows]
    | _ -> let nextWorkflows: Workflows = findNextWorkflows workflows previousWorkflows
           if nextWorkflows.Length = 0 then
            raise(new WorkflowException("The reamining workflows cannot be correctly inserted to the workflow chain of this job, maybe invalid (cross) references exist?\nRemaining workflows: " + ListToString(List.map(fun(wf: Workflow) -> wf.WorkflowName) workflows)))
           generateWorkflowChain (Remove nextWorkflows workflows) (previousWorkflows @ [nextWorkflows])

/// <summary>This class covers the entire csv job that is described
/// via the configuration defined by the passed file path.</summary>
type public Job(configurationFilePath: string) =
    let workflowChain: WorkflowChain = generateWorkflowChain (GetWorkflows configurationFilePath) []
    let mutable results: option<list<string * option<Lines>>> = None

    /// <summary>Executes each workflows by order (depends on the workflow chain)
    /// and sets the corresponding outputs as inouts to the next level
    /// workflows.</summary>
    member private this.ProcessWorkflows(): unit =
        results <-
            List.fold(fun(allResults: list<string * option<Lines>>) (currentWorkflows: Workflows) ->
                List.iter(fun(wf: Workflow) ->
                    if wf.PreviousWorkflows <> [] then
                        wf.Input <- List.filter(fun((resultName: string), _) ->
                                        List.exists(fun(wfName: string) -> wfName = resultName) wf.PreviousWorkflows) allResults
                                    |> List.map(fun(_, (result: option<Lines>)) -> result)
                                    |> List.filter Option.isSome
                                    |> List.map Option.get) currentWorkflows
                List.map(fun(wf: Workflow) -> async { wf.ProcessTasks() }) currentWorkflows
                |> Async.Parallel
                |> Async.RunSynchronously
                |> ignore
                allResults @ (List.map(fun(wf: Workflow) -> (wf.WorkflowName, wf.Output)) currentWorkflows)) [] workflowChain
            |> Some

    /// <summary>Returns a list of tuples contianing the workflow name
    /// and the workflow result.</summary>
    member public this.Results: list<string * option<Lines>> = if results.IsNone then
                                                                   this.ProcessWorkflows()
                                                               results.Value

    /// <summary>Returns true fi the passed string is a valid name
    /// of a workflow contained in this job instance.</summary>
    member public this.ContainsWorkflow(workflowName: string): bool =
        (tryFindWorkflow workflowName workflowChain).IsSome

    /// <summary>Returns the result of the workflow with the passed
    /// name within an option. If no workflow with the given name
    /// does not exist in this job, e WorkflowExcetpion is thrown.</summary>
    member public this.GetResult(workflowName: string): option<Lines> =
        if this.ContainsWorkflow workflowName then
            if results = None then
                this.ProcessWorkflows()
            (tryFindWorkflow workflowName workflowChain).Value.Output
        else
            raise(new CSV.Core.Exceptions.WorkflowException("The workflow \"" + workflowName + "\" does not exist in this job"))