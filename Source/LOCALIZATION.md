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
`#kasLOC_00000` - `#kasLOC_98999` namespace unless they are registered in this file.

| Module name                    | Module | Namespace start | Namespace end |
| ------------------------------ | ------ | --------------- | ------------- |
| AbstractJoint                  | 00     | #kasLOC_00000   | #kasLOC_00999 |
| KASLinkSourceInteractive       | 01     | #kasLOC_01000   | #kasLOC_01999 |
| KASLinkSourceBase              | 02     | #kasLOC_02000   | #kasLOC_02999 |
| KASLinkTargetBase              | 03     | #kasLOC_03000   | #kasLOC_03999 |
| KASRendererTelescopicPipe      | 04     | #kasLOC_04000   | #kasLOC_04999 |
| KASJointTowBar                 | 05     | #kasLOC_05000   | #kasLOC_05999 |
| AbstractPipeRenderer           | 07     | #kasLOC_07000   | #kasLOC_07999 |
| KASLinkWinch                   | 08     | #kasLOC_08000   | #kasLOC_08999 |
| KASJointCableBase              | 09     | #kasLOC_09000   | #kasLOC_09999 |
| KASLinkTargetKerbal            | 10     | #kasLOC_10000   | #kasLOC_10999 |
| ControllerWinchRemote          | 11     | #kasLOC_11000   | #kasLOC_11999 |
| KASLinkResourceConnector       | 12     | #kasLOC_12000   | #kasLOC_12999 |
| KASLinkSourcePhysical          | 13     | #kasLOC_13000   | #kasLOC_13999 |
| KASModuleDart                  | 14     | #kasLOC_14000   | #kasLOC_14999 |
| _Next available value_         | 15     | #kasLOC_?????   | #kasLOC_????? |

# Special namespace

Localization space `#99` are reserved for the special strings, used for the global titles and descriptions. Such
strings are used globally between different `KAS` modules and even the third party mods! For better tracking,
all the ranges are listed below.

## Link type descriptions

These strinsg are used to produce a human readable and localized string in the editor. It's highly encoraged for
the third-paty mods to use the predefined sizes/strings instead of inventing own types. It makes the parts across
the mods more compatible.


__Note__. Keep the descriptions short! Their primary susage is editor's info panel. It has limited width.

For the link types the reserved range is: `#kasLOC_99000` - `#kasLOC_99049`:

* _Rigid_ links (struts)
  * _SMALL_ type: `SmStrut`
    * Pipe diameter: `40cm`.
    * English description: `#kasLOC_99000` = `Pipe-40`
  * _MEDIUM_ type: `MdStrut`
    * Pipe diameter: `100cm`.
    * English description: `#kasLOC_99003` = `Pipe-100`
  * _LARGE_ type: `LgStrut`
    * Pipe diameter: `150cm`.
    * English description: `#kasLOC_99006` = `Pipe-150`
* _Cable_ links
  * _SMALL_ type: `SmCable`
    * Pipe diameter: `10mm`.
    * English description: `#kasLOC_99001` = `Cable-10`
  * _MEDIUM_ type: `MdCable`
    * Pipe diameter: `35mm`.
    * English description: `#kasLOC_99004` = `Cable-35`
  * _LARGE_ type: `LgCable`
    * Pipe diameter: `60mm`.
    * English description: `#kasLOC_99007` = `Cable-60`
* _Hose_ links
  * _SMALL_ type: `SmHose`
    * Pipe diameter: `30cm`.
    * English description: `#kasLOC_99002` = `Hose-30`
  * _MEDIUM_ type: `MdHose`
    * Pipe diameter: `70cm`.
    * English description: `#kasLOC_99005` = `Hose-70`
  * _LARGE_ type: `LgHose`
    * Pipe diameter: `100cm`.
    * English description: `#kasLOC_99008` = `Hose-100`
