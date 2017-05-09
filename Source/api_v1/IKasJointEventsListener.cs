// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine;

namespace KAS {

/// <summary>Interface that notifies listeners about joints breaking.</summary>
/// <seealso cref="BrokenJointListener"/>
/// <example><code source="Examples/BrokenJointListener-Examples.cs" region="BrokenJointListenerExample"/></example>
public interface IKasJointEventsListener {
  /// <summary>Triggers when connection is broken due to too strong force applied.</summary>
  /// <remarks>
  /// This event is expected to be called from Unity <c>OnJointBreak</c> callback. It means it will
  /// come from fixed frame update thread. Not all operations can be done from this thread.
  /// </remarks>
  /// <param name="hostObj">Game object that owns the joint.</param>
  /// <param name="breakForce">Actual force that has been applied.</param>
  void OnKASJointBreak(GameObject hostObj, float breakForce);
}

}  // namespace
