// Kerbal Attachment System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using UnityEngine;

namespace KASAPIv1 {

/// <summary>Interface that notifies listeners about joints breaking.</summary>
/// <remarks>
/// This interface must be supported on the "other" side. The object that implements it only
/// declares a desire to know about the joint state. However, when the joint state is actually
/// changed, some other code needs to take care of calling the callback.
/// </remarks>
/// <example><code source="Examples/IKasJointEventsListener-Examples.cs" region="BrokenJointListenerExample"/></example>
public interface IKasJointEventsListener {
  /// <summary>
  /// Triggers when a connection on the object is broken due to too strong force applied.
  /// </summary>
  /// <remarks>
  /// This event is expected to be called from a Unity physics method. Not all actions can be done
  /// from this kind of handlers.
  /// </remarks>
  /// <param name="hostObj">The game object that owns the joint.</param>
  /// <param name="breakForce">The actual force that has been applied to break the joint.</param>
  void OnKASJointBreak(GameObject hostObj, float breakForce);
}

}  // namespace
