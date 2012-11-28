module public CSV.Constants

open System

/// <summary>The xml namespace of the configuration file.</summary>
[<LiteralAttribute>]
let public CONFIG_NAMESPACE: string = "http://ztt.fh-worms.de/georgieff/text-analytics/"

/// <summary>The prefix for the xml namespace used in the configuration file.</summary>
[<LiteralAttribute>]
let public CONFIG_NAMESPACE_PREFIX: string = "appns"

/// <summary>The XPATH expression for getting all ReadTask elements.</summary>
let public XPATH_READ_TASKS: string = "/" + CONFIG_NAMESPACE_PREFIX + ":cf-result-viewer/" + CONFIG_NAMESPACE_PREFIX + ":workflow/" + CONFIG_NAMESPACE_PREFIX + ":read-task"

/// <summary>The XPATH expression for getting all WriteTask elements.</summary>
let public XPATH_WRITE_TASKS: string = "/" + CONFIG_NAMESPACE_PREFIX + ":cf-result-viewer/" + CONFIG_NAMESPACE_PREFIX + ":workflow/" + CONFIG_NAMESPACE_PREFIX + ":write-task"

/// <summary>The XPATH expression for getting all GenericTask elements.</summary>
let public XPATH_GENERIC_TASKS: string = "/" + CONFIG_NAMESPACE_PREFIX + ":cf-result-viewer/" + CONFIG_NAMESPACE_PREFIX + ":workflow/" + CONFIG_NAMESPACE_PREFIX + ":generic-task"

/// <summary>The XPATH expression for getting all Task elements.</summary>
let public XPATH_TASKS: string = XPATH_GENERIC_TASKS + " | " + XPATH_READ_TASKS + " | " + XPATH_WRITE_TASKS

/// <summary>The XPATH expression for getting all name attributes from the column-definitions children.</summary>
let public XPATH_COLUMN_DEFINITIONS_NAMES: string = CONFIG_NAMESPACE_PREFIX + ":cf-result-viewer/" + CONFIG_NAMESPACE_PREFIX + ":workflow/" + CONFIG_NAMESPACE_PREFIX + ":column-definitions//" + CONFIG_NAMESPACE_PREFIX + ":column/@name"