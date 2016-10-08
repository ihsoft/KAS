// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.KSPInterfaces {

/// <summary>Interface to track physics state changes in the part's module.</summary>
/// <remarks>
/// Events of this inteface are triggered by KSP engine via Unity messaging mechanism. It's not
/// required for the module to implement the interface to be notified but by implementing it the
/// code becomes more consistent and less error prone.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// public class MyModule : PartModule, IsPackable {
///   /// <inheritdoc/>
///   public void OnPartPack() {
///     Debug.LogInfo("OnPartPack");
///   }
///   /// <inheritdoc/>
///   public void OnPartUnpack() {
///     Debug.LogInfo("OnPartUnpack);
///   }
/// }
/// ]]></code>
/// </example>
/// <seealso href="https://docs.unity3d.com/ScriptReference/GameObject.SendMessage.html">
/// Unity 3D: GameObject.SendMessage</seealso>
/// <seealso href="https://kerbalspaceprogram.com/api/class_part.html">KSP: Part</seealso>
public interface IsPackable {
  /// <summary>Triggers when physics stops on the part.</summary>
  void OnPartPack();
  /// <summary>Triggers when physics starts on the part.</summary>
  void OnPartUnpack();
}

}  // namespace
