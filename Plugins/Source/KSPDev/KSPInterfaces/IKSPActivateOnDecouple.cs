﻿// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.KSPInterfaces {

/// <summary>Documented analogue of IActivateOnDecouple interface.</summary>
/// <remarks>Inherit from <see cref="IActivateOnDecouple"/> to be able reacting on parts decoupling.
/// <see cref="IKSPDevModuleInfo"/> is a full equivalent except it's documented.
/// Inheriting modules from both interfaces gives better code documentation.
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
/// </remarks>
public interface IKSPActivateOnDecouple {
  /// <summary>Called when two parts decouple.</summary>
  /// <param name="nodeName">Attach node name that has been detached.</param>
  /// <param name="weDecouple">If <c>true</c> then owner part was child of the other part.</param>
  void DecoupleAction(string nodeName, bool weDecouple);
}

}
