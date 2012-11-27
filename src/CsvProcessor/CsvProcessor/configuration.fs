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
                                } interface ITaskConfiguration with
                                    member this.TaskName: string = this.TaskName

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
                                   Split: char
                                   Quote: char
                                   MetaQuote: char
                                   TrimWhitespaceStart: bool
                                   TrimWhitespaceEnd: bool
                                   TaskName: string
                                   PreviousTask: string
                                   FileMode: FileMode
                                 } interface ITaskConfiguration with
                                    member this.TaskName: string = this.TaskName

/// <summary>Reads the xml configuration file fpor this application
/// and returns a list of tuples that expresses the column name
/// and their position.</summary>
let private getColumnDefinitions(dom: XmlDocument) (xnsm: XmlNamespaceManager): list<ColumnDefinition> =    
    try
        let colNames: XmlNodeList = dom.SelectNodes(XPATH_COLUMN_DEFINITIONS_NAMES, xnsm)
        seq{ for col in colNames do
                yield col.Value
        } |> Seq.toList
        |> AddIndexes
        |> List.map(fun((name: string), (index: int)) -> { ColumnDefinition.Name = name; ColumnDefinition.Index = index })
    with
    | _ as err -> raise(new ConfigurationException("the column-definitions could not be parsed", err))

/// <summary>Returns the character contained in the passed xpath.</summary>
let private getCharFromAttribute(xmlNode: XmlNode) (xnsm: XmlNamespaceManager) (xpath: string): char =
    let tmp: XmlNode = xmlNode.SelectSingleNode(xpath, xnsm)
    if tmp = null then
        raise(new ConfigurationException("the node \"" + xmlNode.Name + "\" misses the following characterisitcs: \"" + xpath + "\""))
    if tmp.Value.Length <> 1 then
        raise(new ConfigurationException("The value \"" + xpath + "\" must consist of exactly one character, but is: " + tmp.Value))
    else
        tmp.Value.[0]

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
    let node: XmlNode = ownerNode.SelectSingleNode(CONFIG_NAMESPACE_PREFIX + ":file-mode", xnsm)
    if node = null then
        FileMode.CreateNew
    else
        try
            Enum.Parse(typeof<FileMode>, GetStringValueOfAttribute node "value", true) :?> FileMode
        with
            | _-> raise(new ConfigurationException("The value <filemode value=" + (GetStringValueOfAttribute node "value") + " could not be parsed!"))
   
/// <summary>Creates a ReadTask object from an xmlNode.</summary>
let private parseReadTask(xmlNode: XmlNode) (xnsm: XmlNamespaceManager): ReadConfiguration =
    try
        { ReadConfiguration.FilePath = xmlNode.SelectSingleNode(CONFIG_NAMESPACE_PREFIX + ":file/@path", xnsm).Value
          ReadConfiguration.Split = getSplitChar xmlNode xnsm
          ReadConfiguration.Quote = getQuoteChar xmlNode xnsm
          ReadConfiguration.MetaQuote = getMetaQuoteChar xmlNode xnsm
          ReadConfiguration.TrimWhitepsaceStart = getTrimWhitespaceStart xmlNode xnsm
          ReadConfiguration.TrimWhitespaceEnd = getTrimWhitespaceEnd xmlNode xnsm
          ReadConfiguration.TaskName = GetStringValueOfAttribute xmlNode "task-name"
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

/// <summary>Returns a list of ColumnMapping instances contained in the owner node.</summary>
let private getColumnMappings(ownerNode: XmlNode) (xnsm: XmlNamespaceManager): ColumnMappings =
    AddIndexes(GetAttributePairsFromNodes (ownerNode.SelectNodes(CONFIG_NAMESPACE_PREFIX + ":column", xnsm)) "ref" "as")
    |> List.map(fun(((source: string), (target: string)), (index: int)) -> { ColumnMapping.Source = { ColumnDefinition.Name = source
                                                                                                      ColumnDefinition.Index = index
                                                                                                    }
                                                                             ColumnMapping.Target = { ColumnDefinition.Name = target
                                                                                                      ColumnDefinition.Index = index
                                                                                                    }
                                                                           })

/// <summary>Creates a WriteTask object from an xmlNode.</summary>
let private parseWriteTask(xmlNode: XmlNode) (xnsm: XmlNamespaceManager): WriteConfiguration =
    try
        { WriteConfiguration.ColumnMappings = getColumnMappings xmlNode xnsm
          WriteConfiguration.FilePath = getFilePath xmlNode xnsm
          WriteConfiguration.Split = getSplitChar xmlNode xnsm
          WriteConfiguration.Quote = getQuoteChar xmlNode xnsm
          WriteConfiguration.MetaQuote = getMetaQuoteChar xmlNode xnsm
          WriteConfiguration.TrimWhitespaceStart = getTrimWhitespaceStart xmlNode xnsm
          WriteConfiguration.TrimWhitespaceEnd = getTrimWhitespaceEnd xmlNode xnsm
          WriteConfiguration.TaskName = GetStringValueOfAttribute xmlNode "task-name"
          WriteConfiguration.PreviousTask = GetStringValueOfAttribute xmlNode "previous-task"
          WriteConfiguration.FileMode = getFileMode xmlNode xnsm
        }
    with
    | _ as err -> raise(new ConfigurationException("a write task could not be parsed", err))

/// <summary>Transforms all xml nodes representing tasks to the corresponding record types.</summary>
let private getTasks(dom: XmlDocument) (xnsm: XmlNamespaceManager): list<ITaskConfiguration> =
    let children: XmlNodeList = dom.SelectNodes(XPATH_TASKS, xnsm)
    seq { for taskNode in children do
            yield match (taskNode.LocalName, taskNode.NamespaceURI) with
                    | ("write", CONFIG_NAMESPACE) -> (parseWriteTask taskNode xnsm) :> ITaskConfiguration
                    | ("read", CONFIG_NAMESPACE) -> (parseReadTask taskNode xnsm) :> ITaskConfiguration
                    | _ -> raise(new ConfigurationException("Element " + taskNode.Name + " not allowed here"))
    } |> Seq.toList


/// <summary>Represents the entire application configuration.</summary>
type public Configuration = { ColumnDefinitions: list<ColumnDefinition>
                              Workflow: list<ITaskConfiguration>
                            } with 
                                /// <summary>Returns a Configuration instance from the passed
                                /// xml configuration file.</summary>
                                static member public Parse(filePath: string) = let dom: XmlDocument = new XmlDocument()
                                                                               let xnsm: XmlNamespaceManager = new XmlNamespaceManager(dom.NameTable)
                                                                               xnsm.AddNamespace("appns", CONFIG_NAMESPACE)
                                                                               dom.Load(filePath)
                                                                               { Configuration.ColumnDefinitions = getColumnDefinitions dom xnsm
                                                                                 Configuration.Workflow = getTasks dom xnsm
                                                                               }