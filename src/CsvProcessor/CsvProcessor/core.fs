﻿module public CSV.Core

open System
open System.Xml
open System.IO

module public Exceptions =
    /// <summary>Should be thrown if the xml configuration for this application is invalid.</summary>
    type public ConfigurationException(message: string, innerException: Exception) =
        inherit Exception(message, innerException)
        new (message: string) = ConfigurationException(message, null)
        new () = ConfigurationException(null, null)

    /// <summary>Should be thrown if any problem is encountered in a task.</summary>
    type public TaskException(message: string, innerException: Exception) =
        inherit Exception(message, innerException)
        new (message: string) = TaskException(message, null)
        new () = TaskException(null, null)

    /// <summary>Should be thrown if a property is thought to be set only once,
    /// but is tried to be set multiple times.</summary>
    type public PropertyAlreadySetException(message: string, innerException: Exception) =
        inherit Exception(message, innerException)
        new (message: string) = PropertyAlreadySetException(message, null)
        new () = PropertyAlreadySetException(null, null)

    /// <summary>Should be thrown if a property is invoked that was not set yet.</summary>
    type public PropertyNotSetException(message: string, innerException: Exception) =
        inherit Exception(message, innerException)
        new (message: string) = PropertyNotSetException(message, null)
        new () = PropertyNotSetException(null, null)

    /// <summary>Should be thrown if an operation of a CSV.Tasks.GenericTask fails
    /// or causes any error, e.g. during registration.</summary>
    type public GenericOperationException(message: string, innerException: Exception) =
        inherit Exception(message, innerException)
        new (message: string) = GenericOperationException(message, null)
        new () = GenericOperationException(null, null)

    /// <summary>Should be thrown if any operation of a CSV.Workflow.Workflow class fails.</summary>
    type public WorkflowException(message: string, innerException: Exception) =
        inherit Exception(message, innerException)
        new (message: string) = WorkflowException(message, null)
        new () = WorkflowException(null, null)

module public Utilities =
    module public List =
        /// <summary>Transform each item of the list to a tuple of the item itself and
        /// an index of the item in the list.</summary>
        let public AddIndexes(lst: list<'t>): list<'t * int> =
            List.fold(fun(acc: list<'t * int>) (item: 't) ->
                if acc = [] then
                    [(item, 0)]
                else
                    acc @ [(item, List.length acc)]) [] lst

        /// <summary>Returns a list of each second item of the source list.
        /// if useNext is set ot true, the first item in the resulting list is
        /// the first item in the destination list.
        /// if useNext is set to false, the second item in the source list is the
        /// first item in the resulting list.</summary>
        let public FilterEachSecond(lst: list<'t>) (useNext: bool): list<'t> =
            let rec innerFun(iLst: list<'t>) (iUseNext: bool) (accumulator: list<'t>): list<'t> =
                if iLst = [] then
                    accumulator
                elif iUseNext then
                    innerFun (List.tail iLst) (not iUseNext) (accumulator @ [List.head iLst])
                else
                    innerFun (List.tail iLst) (not iUseNext) accumulator
            innerFun lst useNext []

        /// <summary>Returns the last element of the passed list or None it
        /// the passed list is empty.</summary>
        let public Last(lst: list<'t>): option<'t> =
            if lst = [] then
                None
            else
                Some(List.nth lst (List.length lst - 1))

        /// <summary>Returns the passed list, except the last element.</summary>
        let public RemoveLast(lst: list<'t>): list<'t> =
            let rec innerFun(inList: list<'t>) (outList: list<'t>): list<'t> =
                if inList = [] || List.length inList = 1 then
                    outList
                else
                    innerFun (List.tail inList) (outList @ [List.head inList])
            innerFun lst []

        /// <summary>Returns a list of tuples of the inout list, e.g.
        /// [1; 2; 3; 4; 5] => [(1,2); (2,3); (3,4); (4,5)].
        /// If a list of only one element is passed the result is:
        /// [1] => [(1,1)].</summary>
        let public Tuplize(lst: list<'t>): list<'t * 't> =
            if List.length lst = 1 then
                [(List.head lst, List.head lst)]
            else
                List.fold(fun(acc: list<'t * option<'t>>) (item: 't) ->
                    if acc = [] then
                        [(item, None)]
                    elif (Last acc).IsSome && (snd ((Last acc).Value)).IsNone then
                        (RemoveLast acc) @ [(fst ((Last acc).Value), Some item); (item, None)]
                    else
                        raise(new InvalidOperationException())) [] lst
                |> List.filter(fun(_, (right: option<'t>)) -> right.IsSome)
                |> List.map(fun((left: 't), (right: option<'t>)) -> (left, right.Value))

        /// <summary>A helper function that returns a duplicate free copy of the
        /// passed list. Each element is compared by the function comparison.</summary>
        let rec private removeDuplicates(comparison: 'a -> 'a -> bool) (lst: list<'a>) (accumulator: list<'a>): list<'a> =
            if lst = [] then
                accumulator
            elif List.exists (comparison (List.head lst)) accumulator then
                removeDuplicates comparison (List.tail lst) accumulator
            else
                removeDuplicates comparison (List.tail lst) (accumulator @ [List.head lst])

        /// <summary>Returns a duplicate free copy of the passed list.
        /// Each element is compared by the function comparison.</summary>
        let public RemoveDuplicates(comparison: 'a -> 'a -> bool) (lst: list<'a>): list<'a> =
            removeDuplicates comparison lst []

        /// <summary>Returns a list of elements that are duplicate occurrences
        /// in the passed list. The duplicate filtering is daone by the
        /// function value comparison.</summary>
        let public CheckForDuplicates(comparison: 'a -> 'a -> bool) (lst: list<'a>): list<'a> =
            List.map(fun(left: 'a) ->  let filteredItems: list<'a> = List.filter(fun(right: 'a) -> comparison left right) lst
                                       if List.length filteredItems > 1 then
                                            Some filteredItems
                                       else
                                            None) lst
            |> List.filter Option.isSome
            |> List.map Option.get
            |> List.map List.head
            |> RemoveDuplicates comparison

        /// <summary>Returns a string representing the passed list of the form
        /// [item1; item2; ...; itemN].</summary>
        let public ListToString(lst: list<'a>): string =
            let result: string = List.fold(fun(acc: string) (item: 'a) -> if acc = "" then
                                                                           item.ToString()
                                                                          else
                                                                            acc + "; " + item.ToString()) "" lst
            "[" + result + "]"

        /// <summary>Returns a list containing elements that are available in both passed lists.</summary>
        let public Intersection(list1: list<'a> when 'a : equality) (list2: list<'a> when 'a : equality): list<'a> when 'a : equality =
            List.filter(fun(item1: 'a) -> List.exists(fun(item2: 'a) -> item1 = item2) list1) list2

        /// <summary>Returns a list containing elements that are available in only
        /// one of the passed lists.</summary>
        let public Difference(list1: list<'a> when 'a : equality) (list2: list<'a> when 'a : equality): list<'a> when 'a : equality =
            (List.filter(fun(item1: 'a) -> not(List.exists(fun(item2: 'a) -> item1 = item2) list1)) list2) @
                (List.filter(fun(item1: 'a) -> not(List.exists(fun(item2: 'a) -> item1 = item2) list2)) list1)

        /// <summary>Returns a copy of the passed list without occurrences of the passed item.</summary>
        let public Remove(item: 'a when 'a : equality) (lst: list<'a> when 'a : equality): list<'a> when 'a : equality =
            List.filter(fun(elem: 'a) -> elem <> item) lst

    module public Xml =
        /// <summary>Returns a list of strings that represent the attribute values of the passed
        /// attribute name from each node of the passed XmlNodeList</summary>
        let public GetAttributesFromNodes(elements: XmlNodeList) (attributeName: string): list<string> =
            seq{ for elem in elements do
                    yield elem.SelectSingleNode("@" + attributeName).Value
            } |> Seq.toList

        /// <summary>Returns a list of string pairs that represent the attribute values of the passed
        /// attribute names from each node of the passed XmlNodeList</summary>
        let public GetAttributePairsFromNodes(elements: XmlNodeList) (attributeName1: string) (attributeName2: string): list<string * string> =
            List.zip (GetAttributesFromNodes elements attributeName1) (GetAttributesFromNodes elements attributeName2)

        /// <summary>Returns the string value of the passed xml node that is stored by the
        /// attribute with the name specified by attributeName.</summary>
        let public GetStringValueOfAttribute(xmlNode: XmlNode) (attributeName: string): string =
            let tmp: XmlNode = xmlNode.SelectSingleNode("@" + attributeName)
            if tmp = null then
                raise(new Exceptions.ConfigurationException("The attribute \"" + attributeName + "\" is missing"))
            else
                tmp.Value

        /// <summary>A helper function for mapping XmlNodeLists.</summary>
        let rec private mapXmlNodeList(operation: XmlNode -> 'a) (index: int) (results: list<'a>) (nodes: XmlNodeList): list<'a> =
            if index >= nodes.Count then
                results
            else
                mapXmlNodeList operation (index + 1) (results @ [operation nodes.[index]]) nodes

        /// <summary>Maps the passed XmlNodeList and returns a list of results of the
        /// operation arguments of type 'a.</summary>
        let public MapXmlNodeList(operation: XmlNode -> 'a) (nodes: XmlNodeList): list<'a> =
            mapXmlNodeList operation 0 [] nodes

    module public IO =
        /// <summary>Reads the entire stream line by line and invokes lineProcessor on each line.
        /// Finally, the entire list of results of lineProcessor is returned.</summary>
        let rec private readStream(streamReader: StreamReader) (lineProcessor: string -> bool -> 't) (accumulator: list<'t>): list<'t> =
            if not (streamReader.EndOfStream) then
                readStream streamReader lineProcessor (accumulator @ [lineProcessor(streamReader.ReadLine()) (List.length accumulator = 0)])
            else
                accumulator

        /// <summary>Reads the entire stream line by line and invokes lineProcessor on each line.
        /// Finally, the entire list of results of lineProcessor is returned.</summary>
        let public ReadStream(streamReader: StreamReader) (lineProcessor: string -> bool-> 't): list<'t> =
            readStream streamReader lineProcessor []

    module public String =
        /// <summary>Returns a list of indexes where occurrences of the character
        /// what can be found.</summary>
        let rec private indexesOf(str: string) (what: char) (accumulator: list<int>) (currentIndex: int): list<int> =
            if str = null || str.Length <= currentIndex then
                accumulator
            elif str.[currentIndex] = what then
                indexesOf str what (accumulator @ [currentIndex]) (currentIndex + 1)
            else
                indexesOf str what accumulator (currentIndex + 1)

        /// <summary>Retruns true if the character at the given position is meta-quoted.</summary>
        let private isMetaQuoted(str: string) (index: int) (metaQuote: char): bool =
            let rec innerFun(iIndex: int) (accumulator: bool): bool =
                if str = "" then
                    false
                elif iIndex = 0 && str.[0] = metaQuote then
                    not accumulator
                elif iIndex = 0 && str.[0] <> metaQuote then
                    accumulator
                elif str.[iIndex] = metaQuote then
                    innerFun (iIndex - 1) (not accumulator)
                else
                    accumulator
            if index = 0 then
                false
            else
                innerFun (index - 1) false

        /// <summary>Returns the index of the first matched occurrence of what
        /// that is not placced within quotes. If the string does not contain
        /// any occurrence of the character what, None is returned.</summary>
        let private findNext(str: string) (what: char) (quote: char) (metaQuote: char): option<int> =
            let indexesOfWhat: list<int> = indexesOf str what [] 0
            let quoteRanges: list<int * int> =
                let indexesOfQuotes = indexesOf str what [] 0
                                      |> List.filter(fun(idx: int) -> not(isMetaQuoted str idx metaQuote))
                List.zip(List.FilterEachSecond indexesOfQuotes true)(List.FilterEachSecond indexesOfQuotes false)
            List.tryFind(fun(index: int) ->
                List.exists(fun((left: int), (right: int)) ->
                    index >= left && index <= right) quoteRanges) indexesOfWhat

        /// <summary>Returns the indexes of all occurrences of what
        /// that are not placced within quotes.</summary>
        let private indexesOfUnquoted(str: string) (what: char) (quote: char) (metaQuote: char): list<int> =
            let indexesOfWhat: list<int> = indexesOf str what [] 0
            let quoteRanges: list<int * int> =
                let indexesOfQuotes = indexesOf str quote [] 0
                                      |> List.filter(fun(idx: int) -> not(isMetaQuoted str idx metaQuote))
                List.zip(List.FilterEachSecond indexesOfQuotes true)(List.FilterEachSecond indexesOfQuotes false)
            if quoteRanges = [] then
                indexesOfWhat
            else
                List.filter(fun(index: int) ->
                    not(List.exists(fun((left: int), (right: int)) ->
                            index >= left && index <= right) quoteRanges)) indexesOfWhat

        /// <summary>Returns a list of strings that contains the splitted parts of
        /// the string str.</summary>
        let private splitAtIndexes(str: string) (indexes: list<int>): list<string> =
            if indexes = [] then
                [str]
            else
                if List.head indexes = 0 then
                    if List.nth indexes ((List.length indexes) - 1) = str.Length then
                        indexes
                    else
                        indexes @ [str.Length]
                else
                    if List.nth indexes ((List.length indexes) - 1) = str.Length then
                        0 :: indexes
                    else
                        0 :: indexes @ [str.Length]
                |> List.Tuplize
                |> List.map(fun((left: int), (right: int)) -> str.Substring(left, right - left))

        /// <summary>Treat this string as a CSV line and splits the line by using the splitter
        /// splitter occurrences in quotes are ignored.</summary>
        let private splitLine(line: string) (split: char) (quote: char) (metaQuote: char): list<string> =
            if line = null || line.Length = 0 then
                []
            else
                splitAtIndexes line (indexesOfUnquoted line split quote metaQuote)
                |> List.map(fun(str: string) -> if str.StartsWith(split.ToString()) then
                                                    str.Substring(1)
                                                else
                                                    str)

        type public System.String with
            /// <summary>Treat this string as a CSV line and splits the line by using the splitter
            /// splitter occurrences in quotes are ignored.</summary>
            member public this.SplitLine((split: char), (quote: char), (metaQuote: char)): list<string> =
                splitLine this split quote metaQuote
            /// <summary>Returns the first index of "what" when it is not in a quoted area.</summary>
            member public this.FindNext((what: char), (quote: char), (metaQuote: char)): option<int> =
                findNext this what quote metaQuote
            /// <summary>Returns a list of indexes where occurrences of the character
            /// what can be found.</summary>
            member public this.IndexesOf(what: char): list<int> =
                indexesOf this what [] 0
            /// <summary>Returns a list of indexes where occurrences of the character
            /// what can be found that is not in a quoted area.</summary>
            member public this.IndexesOfUnquoted((what: char), (quote: char), (metaQuote: char)): list<int> =
                indexesOfUnquoted this what quote metaQuote
            /// <summary>Retruns true if the character at the given position is quoted, e.g. backslash.</summary>
            member public this.IsQuoted((index: int), (quote: char)): bool =
                isMetaQuoted this index quote
            /// <summary>Returns a list of strings that contains the splitted parts of
            /// this string instance.</summary>
            member public this.SplitAtIndexes(indexes: list<int>): list<string> =
                splitAtIndexes this indexes
            /// <summary>Trims the whitespace of this string instance depending on the
            /// passed arguments and returns a new string instance.</summary>
            member public this.Trim((trimWhitespaceStart: bool), (trimWhitespaceEnd: bool)): string =
                if trimWhitespaceStart then
                    if trimWhitespaceEnd then
                        this.Trim()
                    else
                        this.TrimStart()
                else
                    if trimWhitespaceEnd then
                        this.TrimEnd()
                    else
                        this
            /// <summary>Returns a quoted version of this string if the condition function
            /// evaluates to true.
            /// The entire string is embraced by a quote pair.
            /// If a quote character is found, it is quoted by the meta quote character.
            /// If a meta quote characgter si found, it is quoted by a met quote character.</summary>
            member public this.Quote((quote: char), (metaQuote: char)): string =
                let tmp = seq{
                                yield quote
                                for character in this do
                                    if character = quote then
                                        yield metaQuote
                                    if character = metaQuote then
                                        yield metaQuote
                                    yield character
                                yield quote
                             }
                (new String(Seq.toArray tmp))

            /// <summary>Returns a quoted version of this string if the condition function
            /// evaluates to true. Otherwise this instance is returned.</summary>
            member public this.QuoteIf((quote: char), (metaQuote: char), (condition: string -> bool)): string =
                if condition this then
                    this.Quote(quote, metaQuote)
                else
                    this

module public Model =
    /// <summary>The basic Interface for task configurations.<summary>
    type public ITaskConfiguration =
        abstract member TaskName: string with get

    /// <summary>The basic interface for task configurations
    /// that gelong to tasks that generates data.</summary>
    type public IGeneratorTaskConfiguration =
        inherit ITaskConfiguration

    /// <summary>The basic interface for task configurations
    /// that belong to tasks that consume data and so depend
    /// on prior tasks.</summary>
    type public IConsumerTaskConfiguration =
        inherit ITaskConfiguration
        abstract member PreviousTask: string with get

    /// <summary>Represents a column definition made of a column name and
    /// its position/index in a line.</summary>
    type public ColumnDefinition = { Name: string; Index: int }

    /// <summary>Realizes a column mapping of a source column and a
    /// target columm. Each column is referenced via its ColumnDefinition name.</summary>
    type public  ColumnMapping = { Source: ColumnDefinition; Target: ColumnDefinition }

    /// <summary>Realizes a list of column mappings of a source column and a
    /// target columm. Each column is referenced via its ColumnDefinition name.</summary>
    type public ColumnMappings = list<ColumnMapping>

    /// <summary>An interface for all cells. It offers only getters
    /// for requesting the cell's value or its name. The setting must
    /// be done in the constructor.</summary>
    type public ICell =
        abstract member Value: string with get
        abstract member Name: string with get

    /// <summary>Represents a single cell of a CVS file.
    /// Name: contains the name of the column this cell belongs to
    /// Value: contains the actual value of the cell.</summary>
    type public Cell = { Name: string; Value: string } with
        interface ICell with
            member this.Value: string = this.Value
            member this.Name: string = this.Name

    /// <summary>Represents a single header cell of a CVS file.
    /// Name: contains the name of the column this cell belongs to
    /// Value: contains the actual value of the cell, in the case of header
    /// cells the name of it.</summary>
    type public HeaderCell = { Name: string; Value: string } with
        interface ICell with
            member this.Value: string = this.Value
            member this.Name: string = this.Name

    /// <summary>A typedef for a list of Cells representing a line.</summary>
    type public Line = list<ICell>

    /// <summary>Returns true if the passed line is a header line.
    /// If allCells is set to true, each cell in the line must be a HeaderCell
    /// otherwise one occurrence of a HederCell in a single line is sufficient
    /// to identifiy a line as heder line.</summary>
    let public IsHeaderLine(line: Line) (allCells: bool): bool =
        let headerCells: Line = List.filter(fun(cell: ICell) -> cell :? HeaderCell) line
        (allCells && List.length headerCells = List.length line) || (not allCells && List.length headerCells > 0)

    /// <summary>A typedef for a list of Lines (list of lists of Cells) representing an entire CSV file.</summary>
    type public Lines = list<Line>

    /// <summary>The basic interface for all implementation of Tasks.</summary>
    type public ITask =             
        /// <summary>The name of this task that can be used as a reference.</summary>
        abstract member TaskName: string with get
        /// <summary>A getter for requesting the configuraiton object of a task.</summary>
        abstract member TaskConfiguration: ITaskConfiguration with get
        
    /// <summary>The baisc interface for all tasks that get an input and process it.</summary>
    type public IConsumerTask =
        inherit ITask
        /// <summary>The name of the previous task which results are used as input
        /// data for this task. The task name is used as a reference.</summary>
        abstract member PreviousTask: string with get
        /// <summary>Realizes a setter that takes the input data of a Task to be processed.
        /// The processing of the inout data is directly started when the 
        /// property is set.</summary>
        abstract member Input: Lines with set

    /// <summary>The basic interface for all tasks that produces data.</summary>
    type public IGeneratorTask =
        inherit ITask
        /// <summary>Realizes a getter that offers the result data of a Task.</summary>
        abstract member Output: Lines with get