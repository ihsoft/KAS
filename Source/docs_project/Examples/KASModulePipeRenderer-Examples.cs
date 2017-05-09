// Kerbal Attachment System - Examples
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KAS;
using KSPDev.ConfigUtils;

namespace Examples {

#region KASModulePipeRendererFieldsExample
public class KASModulePipeRendererFieldsExample : KASModulePipeRenderer {
  // This field will be automatically loaded by the KASModulePipeRenderer implementation.
  // The path in the parts config will be:
  //   PART/MODULE[@name="KASModulePipeRendererFieldsExample"]/myField
  [PersistentField("myField", group = PartConfigGroup)]
  public string myCustomStringFromPartsConfig = "";
}
#endregion
  
};  // namespace
