// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.KSPInterfaces {

/// <summary>Declares callbacks that are called when a joint between two parts is changed.</summary>
/// <remarks>
/// Events of this inteface are triggered by Unity engine via reflections. It's not required for the
/// module to implement the interface to be notified but by implementing it the code becomes more
/// consistent and less error prone.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// public class MyModule : PartModule, IJointEventsListener {
///   /// <inheritdoc/>
///   public void OnJointBreak(float breakForce) {
///     Debug.LogInfoFormat("OnJointBreak: {0}", breakForce);
///   }
/// }
/// ]]></code>
/// </example>
/// <seealso href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnJointBreak.html">
/// Unity 3D: OnJointBreak</seealso>
public interface IJointEventsListener {
  /// <summary>Triggers when connection is broken due to too strong force applied.</summary>
  /// <param name="breakForce">Actual force that has been applied.</param>
  void OnJointBreak(float breakForce);
}

}  // namespace
