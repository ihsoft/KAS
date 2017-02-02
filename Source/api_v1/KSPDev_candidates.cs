// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

// Store here methods that are used by KAS but should be moved into KSPDev.

namespace KSPDev.KSPInterfaces {

/// <summary>Interface to notify about part's forced destruction.</summary>
/// <remarks>
/// Events of this inteface are triggered by KSP engine via Unity messaging mechanism. It's not
/// required for the module to implement the interface to be notified but by implementing it the
/// code becomes more consistent and less error prone.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// public class MyModule : PartModule, IsPartDeathListener {
///   /// <inheritdoc/>
///   public virtual void OnPartDie() {
///     Debug.LogFromat("OnPartDie: {0}", part.name);
///   }
/// }
/// ]]></code>
/// </example>
/// <seealso href="https://docs.unity3d.com/ScriptReference/GameObject.SendMessage.html">
/// Unity 3D: GameObject.SendMessage</seealso>
/// <seealso href="https://kerbalspaceprogram.com/api/class_part.html">KSP: Part</seealso>
public interface IsPartDeathListener {
  /// <summary>Triggers when part ois destroyed by the game's logic.</summary>
  /// <remarks>At this moment the part is already decoupled from the vessel.</remarks>
  void OnPartDie();
  }
}  // namespace
