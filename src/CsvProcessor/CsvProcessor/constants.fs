module public CSV.Constants

open System
open System.Reflection

[<assembly: AssemblyVersion("1.0.0.0")>]
[<assembly: AssemblyTitleAttribute("Comma Separated Value Processor Library")>]
[<assembly: AssemblyProductAttribute("Comma Separated Value Processor Library")>]
[<assembly: AssemblyCopyrightAttribute("GNU Lesser General Public License, http://www.gnu.org/licenses/lgpl.html")>]
[<assembly: AssemblyCompanyAttribute("Lukas Georgieff, lukas.georgieff@hotmail.com")>]
do()

/// <summary>The xml namespace of the configuration file.</summary>
[<LiteralAttribute>]
let public CONFIG_NAMESPACE: string = "http://ztt.fh-worms.de/georgieff/csv/"

/// <summary>The prefix for the xml namespace used in the configuration file.</summary>
[<LiteralAttribute>]
let public CONFIG_NAMESPACE_PREFIX: string = "appns"

/// <summary>The XPATH expression for getting the root element, i.e. the csv-job
/// of a configuration file.</summary>
let public XPATH_CSV_JOB: string = "/" + CONFIG_NAMESPACE_PREFIX + ":csv-job"

/// <summary>The XPATH expression for getting all workflow elements that are direct
/// children of the root element csv-module.</summary>
let public XPATH_WORKFLOWS: string = "/" + CONFIG_NAMESPACE_PREFIX + ":csv-job/" + CONFIG_NAMESPACE_PREFIX + ":workflow"

/// <summary>The XPATH expression for getting all column elements from a
/// column-mappings element</summary>
let public XPATH_COLUMN_FROM_COLUMN_MAPPINGS: string = CONFIG_NAMESPACE_PREFIX + ":column"

/// <summary>The XPATH expression for getting all ReadTask elements
/// of a workflow element.</summary>
let public XPATH_READ_TASKS: string = CONFIG_NAMESPACE_PREFIX + ":read-task"

/// <summary>The XPATH expression for getting all WriteTask elements
/// of a workflow element.</summary>
let public XPATH_WRITE_TASKS: string = CONFIG_NAMESPACE_PREFIX + ":write-task"

/// <summary>The XPATH expression for getting all GenericTask elements
/// of a workflow element.</summary>
let public XPATH_GENERIC_TASKS: string = CONFIG_NAMESPACE_PREFIX + ":generic-task"

/// <summary>The XPATH expression for getting all Task elements
/// of a workflow element.</summary>
let public XPATH_TASKS: string = XPATH_GENERIC_TASKS + " | " + XPATH_READ_TASKS + " | " + XPATH_WRITE_TASKS

/// <summary>The XPATH expression for getting all column elements of a
/// column-definitions element.</summary>
let public XPATH_COLUMNS_FROM_COLUMN_DEFINITIONS: string = CONFIG_NAMESPACE_PREFIX + ":column"

/// <summary>The XPATH expression for getting the value attribute of a read-multi-line
/// element of a read task.</summary>
let public XPATH_READ_MULTI_LINE: string = CONFIG_NAMESPACE_PREFIX + ":read-multi-line/@value"

/// <summary>The file name of the xml schmea definition for the configuraiton file.</summary>
[<LiteralAttribute>]
let public CONFIGURATION_SCHEMA_FILE_NAME: string = "csv.config.xsd"