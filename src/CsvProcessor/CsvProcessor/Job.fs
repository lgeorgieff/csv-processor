module public CSV.Job

open CSV.Workflow

/// <summary>This class covers the entire csv job that is described
/// via the configuration defined by the passed file path.</summary>
type public Job(configurationFilePath: string) =
    let workflows: list<Workflow> = GetWorkflows configurationFilePath