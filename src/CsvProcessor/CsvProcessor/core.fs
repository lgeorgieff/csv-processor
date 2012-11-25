module public CSV.Core

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

    /// <summary>Sjould be thrown if a property is thought to be set only once,
    /// but is tried to be set multiple times.</summary>
    type public PropertyAlreadySetException(message: string, innerException: Exception) =
        inherit Exception(message, innerException)
        new (message: string) = PropertyAlreadySetException(message, null)
        new () = PropertyAlreadySetException(null, null)

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

    module public IO =
        /// <summary>Reads the entire stream line by line and invokes lineProcessor on each line.
        /// Finally, the entire list of results of lineProcessor is returned.</summary>
        let rec private readStream(streamReader: StreamReader) (lineProcessor: string -> 't) (accumulator: list<'t>): list<'t> =
            if not (streamReader.EndOfStream) then
                readStream streamReader lineProcessor (accumulator @ [lineProcessor(streamReader.ReadLine())])
            else
                accumulator

        /// <summary>Reads the entire stream line by line and invokes lineProcessor on each line.
        /// Finally, the entire list of results of lineProcessor is returned.</summary>
        let public ReadStream(streamReader: StreamReader) (lineProcessor: string -> 't): list<'t> =
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

module public Model =
    /// <summary>Represnet a single cell of a CVS file.</summary>
    type public Cell = { Name: string; Value: string }

    /// <summary>A typedef for a list of Cells representing a line.</summary>
    type public Line = list<Cell>

    /// <summary>A typedef for a list of Lines (list of lists of Cells) representing an entire CSV file.</summary>
    type public Lines = list<Line>

    /// <summary>The basic type for all implementation of Tasks.</summary>
    type public ITask =             
        /// <summary>The name of this task that can be used as a reference.</summary>
        abstract member TaskName: string with get
        
    type public IConsumerTask =
        /// <summary>The name of the previous task which results are used as input
        /// data for this task. The task name is used as a reference.</summary>
        abstract member PreviousTask: string with get
        /// <summary>Realizes a setter that takes the input data of a Task to be processed.</summary>
        abstract member Input: Lines with set

    type public IGeneratorTask =
        /// <summary>Realizes a getter that offers the result data of a Task.</summary>
        abstract member Output: Lines with get   