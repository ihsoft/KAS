// Kerbal Attachment System
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using UnityEngine;

namespace KAS {

/// <summary>
/// Helper class to detect a joint breakage and deliver the event to the part that actually controls
/// this joint.
/// </summary>
/// <remarks>
/// The modules that implement <see cref="IKasJointEventsListener"/> will get notified about a
/// broken joint on a specific game object. It allows distinguishing the joints in a multi-joint
/// setup since normally Unity doesn't tell which joint has broken.
/// </remarks>
/// <example>
/// Let's say a module needs to create an extra joint to some other rigidbody. If the joint
/// component was added to the part's game object then there would be <i>two</i> joints on the part:
/// the stock game's one and the custom one. When any of them gets broken, the game's core assumes
/// it was a joint between the part and its parent, even though it wasn't <i>that</i> joint. To
/// overcome this limitation, an extra game object and this component can be used:
/// <code source="Examples/BrokenJointListener-Examples.cs" region="BrokenJointListenerExample"/>
/// </example>
internal sealed class KASInternalBrokenJointListener : MonoBehaviour,
    // KSP syntax sugar interfaces.
    IJointEventsListener {

  /// <summary>Part to send messages to.</summary>
  public Part hostPart;

  /// <inheritdoc/>
  public void OnJointBreak(float breakForce) {
    HostedDebugLog.Info(gameObject.transform, "Joint is broken with force {0}. Notifying part {1}",
                        breakForce, hostPart);
    hostPart.FindModulesImplementing<IKasJointEventsListener>()
        .ForEach(x => x.OnKASJointBreak(gameObject, breakForce));
  }
}

}  // namespace
