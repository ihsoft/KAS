# Tag string format

The tag is a concatenation of three values:
1. Literal `#kasLOC_`.
2. The string represenation of a KAS module _number_ formatted as a two-digit decimal value. E.g. literal
   `09` means a decimal value `9`.
3. The number of a string within the module formatted as a three-difit decimal value. E.g. literal `009`
   means a decimal value `9`.

For example, tag `#kasLOC_12345` defines a string `345` (three hundred forty five) in the module `12` (twelve).

# Tag namespace reservation

_Every_ KAS module **must** explicitly reserve a module number here. The descendent modules must
not use the same namespace as the parent. No modules are allowed to define own values for the tags within the
`#kasLOC_00000` - `#kasLOC_99999` namespace unless they are registered in this file.

| Module name                    | Module | Namespace start | Namespace end |
| ------------------------------ | ------ | --------------- | ------------- |
| KASLinkSourceInteractive       | 01     | #kasLOC_01000   | #kasLOC_01999 |
| KASLinkWinch                   | 08     | #kasLOC_08000   | #kasLOC_08999 |
| ControllerWinchRemote          | 11     | #kasLOC_11000   | #kasLOC_11999 |
| KASLinkResourceConnector       | 12     | #kasLOC_12000   | #kasLOC_12999 |
| KASLinkSourcePhysical          | 13     | #kasLOC_13000   | #kasLOC_13999 |
| _Next available value_         | 11     | #kasLOC_?????   | #kasLOC_????? |
