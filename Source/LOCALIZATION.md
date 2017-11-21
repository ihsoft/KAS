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
| KASModuleJointBase             | 0      | #kasLOC_00000   | #kasLOC_00999 |
| KASModuleInteractiveLinkSource | 1      | #kasLOC_01000   | #kasLOC_01999 |
| KASModuleLinkSourceBase        | 2      | #kasLOC_02000   | #kasLOC_02999 |
| KASModuleLinkTargetBase        | 3      | #kasLOC_03000   | #kasLOC_03999 |
| KASModuleTelescopicPipeModel   | 4      | #kasLOC_04000   | #kasLOC_04999 |
| KASModuleTowBarActiveJoint     | 5      | #kasLOC_05000   | #kasLOC_05999 |
| KASModuleCableJoint            | 6      | #kasLOC_06000   | #kasLOC_06999 |
| KASModulePipeRenderer          | 7      | #kasLOC_07000   | #kasLOC_07999 |
| KASModuleWinchNew              | 8      | #kasLOC_08000   | #kasLOC_08999 |
| KASModuleCableJointBase        | 9      | #kasLOC_09000   | #kasLOC_09999 |
| KASModuleKerbalLinkTarget      | 10     | #kasLOC_10000   | #kasLOC_10999 |
| _Next available value_         | 11     | #kasLOC_11000   | #kasLOC_11999 |
