// This is an intermediate module for methods and classes that are considred as candidates for
// KSPDev Utilities. Ideally, this module is always empty but there may be short period of time
// when new functionality lives here and not in KSPDev.

using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using UnityEngine;

namespace KSPDev {

/// <summary>Generic interface for the modules that implement a UI context menu.</summary>
/// <seealso href="https://kerbalspaceprogram.com/api/class_game_events.html#ae6daaa6f39473078514543a2f8485513">
/// KPS: GameEvents.onPartActionUICreate</seealso>
/// <seealso href="https://kerbalspaceprogram.com/api/class_game_events.html#a7ccbd16e2aee0176a4431f0b5f9d63e5">
/// KPS: GameEvents.onPartActionUIDismiss</seealso>
public interface IHasContextMenu {
  /// <summary>
  /// A callback that is called every time the module's conetxt menu items need to update. 
  /// </summary>
  /// <remarks>
  /// It's very implementation dependent when and why the update is needed. However, at the very
  /// least this callback must be called on the parts load to let the module to update the state and
  /// the titles.
  /// </remarks>
  void UpdateContextMenu();
}

}  // namespace

namespace KSPDev.GUIUtils {

/// TODO: Drop it.
public static class A {
  /// TODO: Merge into KSPDev.GUIUtils.Messages
  public static string Format(this Message msg) {
    return (string) msg;
  }
}

}  // namepsace

namespace KSPDev.ResourceUtils {

/// <summary>
/// A helper class that holds string and ID defintions for all the game stock resources. 
/// </summary>
/// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Resource">KSP Wiki: Resource</seealso>
public static class StockResourceNames {
  /// <summary>Electric charge resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Electric_charge">
  /// KSP Wiki: Electric charge</seealso>
  public const string ElectricCharge = "ElectricCharge";

  /// <summary>Liquid fuel resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Liquid_fuel">
  /// KSP Wiki: Liquid fuel</seealso>
  public const string LiquidFuel = "LiquidFuel";

  /// <summary>Oxidizer resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Oxidizer">
  /// KSP Wiki: Oxidizer</seealso>
  public const string Oxidizer = "Oxidizer";

  /// <summary>Intake air resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Intake_air">
  /// KSP Wiki: Intake air</seealso>
  public const string IntakeAir = "IntakeAir";

  /// <summary>Solid fuel resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Solid_fuel">
  /// KSP Wiki: Solid fuel</seealso>
  public const string SolidFuel = "SolidFuel";

  /// <summary>Monopropellant resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Monopropellant">
  /// KSP Wiki: Monopropellant</seealso>
  public const string MonoPropellant = "MonoPropellant";

  /// <summary>EVA Propellant resource name.</summary>
  /// <remarks>It's the fuel that powers the EVA jetpack.</remarks>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Extra-Vehicular_Activity">
  /// KSP Wiki: Extra-Vehicular Activity</seealso>
  public const string EvaPropellant = "EVA Propellant";

  /// <summary>Xenon gas resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Xenon_gas">
  /// KSP Wiki: Xenon gas</seealso>
  public const string XenonGas = "XenonGas";

  /// <summary>Ore resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Ore">
  /// KSP Wiki: Ore</seealso>
  public const string Ore = "Ore";

  /// <summary>Ablator resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Ablator">
  /// KSP Wiki: Ablator</seealso>
  public const string Ablator = "Ablator";

  /// <summary>Returns an ID for the specified resource name.</summary>
  /// <remarks>This ID can be used in the methods that can only work with IDs.</remarks>
  /// <param name="resourceName">The name of the stock resource.</param>
  /// <returns>An ID of the resource.</returns>
  public static int GetId(string resourceName) {
    return resourceName.GetHashCode();
  }

  /// <summary>Returns a user friendly name of the resource.</summary>
  /// <param name="resourceName">The resource common name.</param>
  /// <returns>A user friendly string that identifies the resource.</returns>
  public static string GetResourceTitle(string resourceName) {
    return PartResourceLibrary.Instance.GetDefinition(resourceName).title;
  }

  /// <summary>Returns a user friendly name of the resource.</summary>
  /// <param name="resourceId">The resource ID.</param>
  /// <returns>A user friendly string that identifies the resource.</returns>
  public static string GetResourceTitle(int resourceId) {
    return PartResourceLibrary.Instance.GetDefinition(resourceId).title;
  }
}

}  // namepsace

namespace KSPDev.ModelUtils {

/// TODO: Merge with KSPDev.Hierarchy
public static class Hierarchy2 {
  /// TODO: Replace default FindTransformByPath version.
  public static Transform FindTransformByPath(Transform parent, string path, Transform defValue = null) {
    var obj = Hierarchy.FindTransformByPath(parent, path);
    if (obj == null && defValue != null) {
      Debug.LogWarningFormat(
          "Cannot find model object: root={0}, path={1}. Using a fallback: {2}",
          DbgFormatter.TranformPath(parent), path, DbgFormatter.TranformPath(defValue));
      return defValue;
    }
    return obj;
  }

  /// <summary>Finds an object in the part's model.</summary>
  /// <param name="part">The part to look for the objects in.</param>
  /// <param name="path">The path to look for.</param>
  /// <param name="defValue">The default value to return when no object found.</param>
  /// <returns>The found object or <c>null</c>.</returns>
  public static Transform FindPartModelByPath(Part part, string path, Transform defValue = null) {
    return FindTransformByPath(Hierarchy.GetPartModelTransform(part), path, defValue: defValue);
  }
}
  
}  // namespace
