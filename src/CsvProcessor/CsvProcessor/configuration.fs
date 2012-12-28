module public CSV.Configuration

open System
open System.IO
open System.Xml

open CSV.Constants
open CSV.Core.Exceptions
open CSV.Core.Utilities.List
open CSV.Core.Utilities.String
open CSV.Core.Utilities.Xml
open CSV.Core.Model

/// <summary>A task representing reading the input file.
/// FilePath: the path of the file to be read.
/// Splitter: the character used for splitting several values.
/// TrimWhitepsaceStart: if true, each value's whitespace is remove on the left side
/// TrimWhitespaceEnd: if true, each value's whitespace is removed on the right side.</summary>
type public ReadConfiguration = { FilePath: string
                                  Split: char
                                  Quote: char
                                  MetaQuote: char
                                  TrimWhitepsaceStart: bool
                                  TrimWhitespaceEnd: bool
                                  TaskName: string
                                  ReadMultiLine: bool
                                } interface ITaskConfiguration with
                                    member this.TaskName: string = this.TaskName
                                  interface IGeneratorTaskConfiguration

/// <summary>A task representing the printing operation on screen.
/// ColumnMappings: the mapping between incoming columns and outgoing columns.
/// If there is no mapping fo a column it is ignored
/// Split: the character that is used on screen to split several values
/// Quote: quotation character that is used for declaring ranges where
/// the split character has no effect
/// MetaQuote: a met quotation character that enables to quote the
/// character defined as "Quote", e.g. the backslash
/// ColumnMapping: the mapping between source column names and destination
/// FilePath: the path of the resulting file. If the valie is None,
/// the stdout stream is used instead a file
/// TrimWhitepsaceStart: if true, each value's whitespace is remove on the left side
/// TrimWhitespaceEnd: if true, each value's whitespace is removed on the right side
/// PreviousTask: the task that generated the content to be consumed by this task.</summary>
type public WriteConfiguration = { ColumnMappings: ColumnMappings
                                   FilePath: option<string>
                                   FileMode: FileMode
                                   Split: char
                                   Quote: char
                                   MetaQuote: char
                                   TrimWhitespaceStart: bool
                                   TrimWhitespaceEnd: bool
                                   TaskName: string
                                   PreviousTask: option<string>
                                 } interface ITaskConfiguration with
                                    member this.TaskName: string = this.TaskName
                                   interface IConsumerTaskConfiguration with
                                    member this.PreviousTask: option<string> = this.PreviousTask

/// <summary>A configuration class representing configuration settings for
/// generic tasks.
/// LineOperation: an identifier for the used operation that is applied
/// line by line. If it is set to None, DocumentOperation must be set
/// DocumentOperation: an identifier for the used operation that is
/// applied at once for the entire document. If it is set to none,
/// LineOperation muest be set.
/// PrevisouTask: the task that generated the content to be consumed by this task
/// TaskName: the identifier of the actual task.</summary>
type public GenericTaskConfiguration = { LineOperation: option<string>
                                         DocumentOperation: option<string>
                                         PreviousTask: option<string>
                                         TaskName: string
                                       } interface ITaskConfiguration with
                                            member this.TaskName: string = this.TaskName
                                         interface IConsumerTaskConfiguration with
                                            member this.PreviousTask: option<string> = this.PreviousTask
                                         interface IGeneratorTaskConfiguration

/// <summary>Returns the root csv-job element or throwns an ConfigurationException
/// if the element cannot be returned.</summary>
let private getCsvJob(elem: XmlNode) (xnsm: XmlNamespaceManager): XmlNode =
    let nodes: XmlNodeList = elem.OwnerDocument.SelectNodes(XPATH_CSV_JOB, xnsm)
    if nodes.Count <> 1 then
        raise(new ConfigurationException("the configuration must the element csv-job exactly once as root element, but found: " + nodes.Count.ToString()))
    nodes.[0]

/// <summary>Returns the character contained in the passed xpath.</summary>
let private getCharFromAttribute(xmlNode: XmlNode) (xnsm: XmlNamespaceManager) (xpath: string): char =
    let tmp: XmlNode = xmlNode.SelectSingleNode(xpath, xnsm)
    if tmp = null then
        raise(new ConfigurationException("the node \"" + xmlNode.Name + "\" misses the following characterisitcs: \"" + xpath + "\""))
    if tmp.Value.Length <> 1 then
        raise(new ConfigurationException("The value \"" + xpath + "\" must consist of exactly one character, but is: " + tmp.Value))
    else
        tmp.Value.[0]

/// <summary>Returns a list with all previos workflows of the passed workflow node.</summary>
let private getWorkflowPredecessors(xmlNode: XmlNode) (xnsm: XmlNamespaceManager): list<string> =
    let values: option<string> = GetStringValueOfOptionalAttribute xmlNode "previous-workflows"
    if values.IsNone then
        []
    else
        Array.toList(values.Value.Split([|' '|]))

/// <summary>Returns the character representing the separator between several values.</summary>
let private getSplitChar(xmlNode: XmlNode) (xnsm: XmlNamespaceManager): char =
    getCharFromAttribute xmlNode xnsm (CONFIG_NAMESPACE_PREFIX + ":split/@char")

/// <summary>Returns the character representing the quote of the splitter character.</summary>
let private getQuoteChar(xmlNode: XmlNode) (xnsm: XmlNamespaceManager): char =
    getCharFromAttribute xmlNode xnsm (CONFIG_NAMESPACE_PREFIX + ":quote/@char")

/// <summary>Returns the character representing the meta quote, e.g. the backslash.</summary>
let private getMetaQuoteChar(xmlNode: XmlNode) (xnsm: XmlNamespaceManager): char =
    getCharFromAttribute xmlNode xnsm (CONFIG_NAMESPACE_PREFIX + ":meta-quote/@char")

/// <summary>Returns a boolena value of the matched XPATH expression (must result in an
/// attribute) or false if the XPATH does not match.</summary>
let private getBooleanOrFalse(ownerNode: XmlNode) (xnsm: XmlNamespaceManager) (xpath: string): bool =
    let tmp: XmlNode = ownerNode.SelectSingleNode(xpath, xnsm)
    if tmp = null then
        false
    else
        bool.Parse(tmp.Value)

/// <summary>Returns true or false representing the boolean value of trim-whitespace-start.
/// If this attribute ist not set in xml, false is returned.</summary>
let private getTrimWhitespaceStart(ownerNode: XmlNode) (xnsm: XmlNamespaceManager): bool =
    getBooleanOrFalse ownerNode xnsm (CONFIG_NAMESPACE_PREFIX + ":trim-whitespace-start/@value")

/// <summary>Returns true or false representing the boolean value of trim-whitespace-end.
/// If this attribute ist not set in xml, false is returned.</summary>
let private getTrimWhitespaceEnd(ownerNode: XmlNode) (xnsm: XmlNamespaceManager): bool =
    getBooleanOrFalse ownerNode xnsm (CONFIG_NAMESPACE_PREFIX + ":trim-whitespace-end/@value")

/// <summary>Returns a FileMode instance that says how to open the file.</summary>
let private getFileMode(ownerNode: XmlNode) (xnsm: XmlNamespaceManager): FileMode =
    let node: XmlNode = ownerNode.SelectSingleNode(CONFIG_NAMESPACE_PREFIX + ":file", xnsm)
    if node = null then
        FileMode.CreateNew
    else
        try
            let value:option<string> = GetStringValueOfOptionalAttribute node "mode"
            if value.IsNone then
                FileMode.CreateNew
            else
                Enum.Parse(typeof<FileMode>, value.Value, true) :?> FileMode
        with
            | _-> raise(new ConfigurationException("The value <file mode=" + (GetStringValueOfAttribute node "value") + " could not be parsed!"))

/// <sumamry>Returns the operation identifier of the operation of a
/// GenericTaskConfiguration that is applied line by line.</summary>
let private getLineOperation(ownerNode: XmlNode) (xnsm: XmlNamespaceManager): option<string> =
    let operationNode: XmlNode = ownerNode.SelectSingleNode(CONFIG_NAMESPACE_PREFIX + ":line-operation", xnsm)
    if operationNode = null then
        None
    else
        let attrNode: XmlNode = operationNode.SelectSingleNode("@identifier")
        if attrNode = null then
            raise(new ConfigurationException("The attribute \"identifier\" is missing from " + attrNode.Name))
        Some(attrNode.Value)

/// <sumamry>Returns the operation identifier of the operation of a
/// GenericTaskConfiguration that is applied once on an entire document.</summary>
let private getDocumentOperation(ownerNode: XmlNode) (xnsm: XmlNamespaceManager): option<string> =
    let operationNode: XmlNode = ownerNode.SelectSingleNode(CONFIG_NAMESPACE_PREFIX + ":document-operation", xnsm)
    if operationNode = null then
        None
    else
        let attrNode: XmlNode = operationNode.SelectSingleNode("@identifier")
        if attrNode = null then
            raise(new ConfigurationException("The attribute \"identifier\" is missing from " + attrNode.Name))
        Some(attrNode.Value)


/// <summary>Returns a string representing a task or a workflow name.
/// If this value is an emtpy string, a ConfigurationExcetppijn is thrown.</summary>
let private getName(xmlNode: XmlNode) (xnsm: XmlNamespaceManager): string =
    let value: string = GetStringValueOfAttribute xmlNode "name"
    if value.Trim() = "" then
        raise(new ConfigurationException("Task and workflow names must not be empty strings, but found in " + xmlNode.ToString()))
    else
        value.Trim()

/// <summary>Returns the value of the attribute "value" of a read-multi-line element
/// of the passed xml node. If this value is not present, false is returned.</summary>
let private getReadMultiLine(xmlNode: XmlNode) (xnsm: XmlNamespaceManager): bool =
    let value: XmlNode = xmlNode.SelectSingleNode(XPATH_READ_MULTI_LINE, xnsm)
    if value = null then
        false
    else
        try
            Boolean.Parse(value.Value)
        with
        | _ as err -> raise(new ConfigurationException("The read-multi-line value \"" + value.Value + "\" could not be parsed to a boolean", err))
   
/// <summary>Creates a ReadTaskConfiguration object from an xmlNode.</summary>
let private parseReadTask(xmlNode: XmlNode) (xnsm: XmlNamespaceManager): ReadConfiguration =
    try
        { ReadConfiguration.FilePath = xmlNode.SelectSingleNode(CONFIG_NAMESPACE_PREFIX + ":file/@path", xnsm).Value
          ReadConfiguration.Split = getSplitChar xmlNode xnsm
          ReadConfiguration.Quote = getQuoteChar xmlNode xnsm
          ReadConfiguration.MetaQuote = getMetaQuoteChar xmlNode xnsm
          ReadConfiguration.TrimWhitepsaceStart = getTrimWhitespaceStart xmlNode xnsm
          ReadConfiguration.TrimWhitespaceEnd = getTrimWhitespaceEnd xmlNode xnsm
          ReadConfiguration.TaskName = getName xmlNode xnsm
          ReadConfiguration.ReadMultiLine = getReadMultiLine xmlNode xnsm
        }
    with
    | _ as err -> raise(new ConfigurationException("a read task could not be parsed", err))

/// <summary>Returns an option with a string that contains the attribute value of the
/// element "file" containing the attribute "path". If such a value is not available
/// None is returned.</summary>
let private getFilePath(taskNode: XmlNode) (xnsm: XmlNamespaceManager): option<string> =
    let tmp: XmlNode = taskNode.SelectSingleNode(CONFIG_NAMESPACE_PREFIX + ":file/@path", xnsm)
    if tmp <> null then
        Some(tmp.Value)
    else
        None

/// <summary>Reads the xml configuration file fpor this application
/// and returns a list of tuples that expresses the column name
/// and their position.</summary>
let private getColumnDefinitions(workflow: XmlNode) (xnsm: XmlNamespaceManager): list<ColumnDefinition> =
    try
        let defName: string = GetStringValueOfAttribute workflow "column-definitions"
        let columnDefitionsNodes: XmlNodeList = (getCsvJob workflow xnsm).SelectNodes("/" + CONFIG_NAMESPACE_PREFIX + ":csv-job/" + CONFIG_NAMESPACE_PREFIX + ":column-definitions[@name='" + defName + "']", xnsm)
        if columnDefitionsNodes.Count <> 1 then
            raise(new ConfigurationException("The element \"column-definitions\" with the attribute \"name\" set to the value \"" + defName + "\" must be existent exactly once, but is: " + columnDefitionsNodes.Count.ToString()))
        columnDefitionsNodes.[0].SelectNodes(XPATH_COLUMN_DEFINITIONS_NAMES, xnsm)
        |> MapXmlNodeList(fun(node: XmlNode) -> node.Value)
        |> AddIndexes
        |> List.map(fun((name: string), (index: int)) -> { ColumnDefinition.Name = name; ColumnDefinition.Index = index })
    with
    | _ as err -> raise(new ConfigurationException("The column-definitions could not be parsed", err))

/// <summary>Returns a list of ColumnMapping instances contained in the owner node.</summary>
let private getColumnMappings(ownerNode: XmlNode) (xnsm: XmlNamespaceManager): ColumnMappings =
    try
        let mappingsName: string = GetStringValueOfAttribute ownerNode "column-mappings"
        let columnMappingsNodes: XmlNodeList = (getCsvJob ownerNode xnsm).SelectNodes("/" + CONFIG_NAMESPACE_PREFIX + ":csv-job/" + CONFIG_NAMESPACE_PREFIX + ":column-mappings[@name='" + mappingsName + "']", xnsm)
        if columnMappingsNodes.Count <> 1 then
            raise(new ConfigurationException("The element \"column-mappings\" with the attribute \"name\" set to the value \"" + mappingsName + "\" must be existent exactly once, but is: " + columnMappingsNodes.Count.ToString()))
        columnMappingsNodes.[0].SelectNodes(XPATH_COLUMN_FROM_COLUMN_MAPPINGS, xnsm)
        |> Core.Utilities.Xml.MapXmlNodeList(fun(node: XmlNode) ->
            let sourceName: string = GetStringValueOfAttribute node "ref"
            let targetName: option<string> = GetStringValueOfOptionalAttribute node "as"
            (sourceName, (if targetName.IsNone then sourceName else targetName.Value)))
        |> AddIndexes
        |> List.map(fun(((sourceName: string), (targetName: string)), (index: int)) ->
            { ColumnMapping.Source = { ColumnDefinition.Name = sourceName
                                       ColumnDefinition.Index = index
                                     }
              ColumnMapping.Target = { ColumnDefinition.Name = targetName
                                       ColumnDefinition.Index = index
                                     }
            })
    with
    | _ as err -> raise(new ConfigurationException("The column-mappings could not be parsed", err))
    
/// <summary>Creates a WriteTaskConfiguration object from an xmlNode.</summary>
let private parseWriteTask(xmlNode: XmlNode) (xnsm: XmlNamespaceManager): WriteConfiguration =
    try
        { WriteConfiguration.ColumnMappings = getColumnMappings xmlNode xnsm
          WriteConfiguration.FilePath = getFilePath xmlNode xnsm
          WriteConfiguration.FileMode = getFileMode xmlNode xnsm
          WriteConfiguration.Split = getSplitChar xmlNode xnsm
          WriteConfiguration.Quote = getQuoteChar xmlNode xnsm
          WriteConfiguration.MetaQuote = getMetaQuoteChar xmlNode xnsm
          WriteConfiguration.TrimWhitespaceStart = getTrimWhitespaceStart xmlNode xnsm
          WriteConfiguration.TrimWhitespaceEnd = getTrimWhitespaceEnd xmlNode xnsm
          WriteConfiguration.TaskName = getName xmlNode xnsm
          WriteConfiguration.PreviousTask = GetStringValueOfOptionalAttribute xmlNode "previous-task"
        }
    with
    | _ as err -> raise(new ConfigurationException("a write task could not be parsed", err))

/// <summary>Creates a GenericTaskConfiguration object from an xmlNode.</summary>
let private parseGenericTask(xmlNode: XmlNode) (xnsm: XmlNamespaceManager): GenericTaskConfiguration =
    try
        let lineOperation: option<string> = getLineOperation xmlNode xnsm
        let documentOperation: option<string> = getDocumentOperation xmlNode xnsm
        if lineOperation.IsNone && documentOperation.IsNone then
            raise(new ConfigurationException("only one of <line-operation> and <document-operation> must be set"))
        if lineOperation.IsSome && documentOperation.IsSome then
            raise(new ConfigurationException("one of <line-operation> and <document-operation> must be set"))
        { GenericTaskConfiguration.LineOperation = lineOperation
          GenericTaskConfiguration.DocumentOperation = documentOperation
          GenericTaskConfiguration.PreviousTask = GetStringValueOfOptionalAttribute xmlNode "previous-task"
          GenericTaskConfiguration.TaskName = getName xmlNode xnsm
        }
    with
    | _ as err -> raise(new ConfigurationException("a generic task could not be parsed", err))

/// <summary>Transforms all xml nodes representing tasks to the corresponding record types.</summary>
let private getTasks(workflow: XmlNode) (xnsm: XmlNamespaceManager): list<ITaskConfiguration> =
    let children: XmlNodeList = workflow.SelectNodes(XPATH_TASKS, xnsm)
    let results: list<ITaskConfiguration> =
        seq { for taskNode in children do
                yield match (taskNode.LocalName, taskNode.NamespaceURI) with
                        | ("write-task", CONFIG_NAMESPACE) -> (parseWriteTask taskNode xnsm) :> ITaskConfiguration
                        | ("read-task", CONFIG_NAMESPACE) -> (parseReadTask taskNode xnsm) :> ITaskConfiguration
                        | ("generic-task", CONFIG_NAMESPACE) -> (parseGenericTask taskNode xnsm) :> ITaskConfiguration
                        | _ -> raise(new ConfigurationException("Element " + taskNode.Name + " not allowed here"))
            } |> Seq.toList
    let duplicates: list<ITaskConfiguration> =
        CheckForDuplicates (fun(left: ITaskConfiguration) (right: ITaskConfiguration) -> left.TaskName = right.TaskName) results
    if List.length duplicates > 0 then
        raise(new ConfigurationException("Task must have unique names, but found the following duplicates: " + ListToString duplicates))
    results

/// <summary>Represents the entire application configuration.</summary>
type public WorkflowConfiguration = 
    { ColumnDefinitions: list<ColumnDefinition>
      Workflow: list<ITaskConfiguration>
      Name: string
      PreviousWorkflows: list<string>
    } with
    /// <summary>parses a workflow element and returns a corresponding
    /// WorkflowConfiguration object.</summary>
    static member private parseWorkflowConfiguration(workflowNode: XmlNode) (xnsm: XmlNamespaceManager): WorkflowConfiguration =
        { WorkflowConfiguration.ColumnDefinitions = getColumnDefinitions workflowNode xnsm
          WorkflowConfiguration.Workflow = getTasks workflowNode xnsm
          WorkflowConfiguration.Name = getName workflowNode xnsm
          WorkflowConfiguration.PreviousWorkflows = getWorkflowPredecessors workflowNode xnsm
        }
    /// <summary>Returns a Configuration instance from the passed
    /// xml configuration file for each existing configuration element.</summary>
    static member public Parse(filePath: string): list<WorkflowConfiguration> =
        let (dom, xnsm): XmlDocument * XmlNamespaceManager = LoadXmlIntoDocument filePath
        MapXmlNodeList(fun (workflowNode: XmlNode) ->
            WorkflowConfiguration.parseWorkflowConfiguration workflowNode xnsm) (dom.SelectNodes(XPATH_WORKFLOWS, xnsm))
    /// <summary>Returns a Configuration instance from the passed xml configuration
    /// file for the workflow element declaring the passed workflow name.</summary>
    static member public Parse((filePath: string), (workflowName: string)): WorkflowConfiguration =
        let (dom, xnsm): XmlDocument * XmlNamespaceManager = LoadXmlIntoDocument filePath
        let results: list<WorkflowConfiguration> =
            MapXmlNodeList(fun (workflowNode: XmlNode) ->
                WorkflowConfiguration.parseWorkflowConfiguration workflowNode xnsm) (dom.SelectNodes(XPATH_WORKFLOWS + "[@name=\"" + workflowName + "\"]", xnsm))
        if List.length results <> 1 then
            raise(new ConfigurationException("The configuration file must contain exactly one workflow element with the name \"" + workflowName + "\", but contains: " + (List.length results).ToString()))
        else
            List.head results