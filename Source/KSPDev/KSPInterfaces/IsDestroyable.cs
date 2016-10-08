// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.KSPInterfaces {

/// <summary>Interface for modules that need not now if script object is destroyed.</summary>
/// <remarks>
/// Events of this inteface are triggered by Unity engine via reflections. It's not required for the
/// module to implement the interface to be notified but by implementing it the code becomes more
/// consistent and less error prone.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// public class MyModule : PartModule, IsDestroyable {
///   /// <inheritdoc/>
///   public void OnDestory() {
///     Debug.LogInfo("OnDestory");
///   }
/// }
/// ]]></code>
/// </example>
/// <seealso href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnDestroy.html">
/// Unity 3D: OnDestroy</seealso>
public interface IsDestroyable {
  /// <summary>Triggers when Unity object is about to destroy.</summary>
  void OnDestroy();
}

}  // namespace
