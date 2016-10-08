// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.KSPInterfaces {

/// <summary>Documented analogue of IActivateOnDecouple interface.</summary>
/// <remarks>
/// Inherit from <see cref="IActivateOnDecouple"/> to be able reacting on parts decoupling.
/// <see cref="IKSPDevModuleInfo"/> is a full equivalent except it's documented.
/// Inheriting modules from both interfaces gives better code documentation.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// public class MyModule : PartModule, IActivateOnDecouple, IKSPActivateOnDecouple {
///   /// <inheritdoc/>
///   public virtual void DecoupleAction(string nodeName, bool weDecouple) {
///     Debug.LogInfo("DecoupleAction");
///   }
/// }
/// ]]></code>
/// </example>
/// <seealso href="https://kerbalspaceprogram.com/api/interface_i_activate_on_decouple.html">
/// KSP: IActivateOnDecouple</seealso>
public interface IKSPActivateOnDecouple {
  /// <summary>Called when two parts decouple.</summary>
  /// <remarks>
  /// Callback is only called on the part if it has an attach node that connects it to the other
  /// part. Just removing from the vessel hierarchy won't trigger the event.
  /// </remarks>
  /// <param name="nodeName">Attach node name that has been detached.</param>
  /// <param name="weDecouple">
  /// If <c>true</c> then the part being notified was a child in the relation of the detached part.
  /// </param>
  void DecoupleAction(string nodeName, bool weDecouple);
}

}  // namespace
