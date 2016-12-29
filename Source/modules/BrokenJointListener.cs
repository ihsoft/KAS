// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using System;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>Helper class to detect joint breakage and deliver event to the host part.</summary>
/// <remarks>
/// Modules that implement <see cref="IKasJointEventsListener"/> will get notified about a joint
/// broken on the specific game object. It allows distinguishing joints in a multi-joint setup since
/// normally Unity doesn't tell which joint has broken if there were more than one on the object.
/// </remarks>
/// <example>
/// Let's say a module needs to create an extra joint to something. If it was added to the part
/// then there would be <i>two</i> joints on the game object. When any of them got broken it would
/// be impossible to figure out which one exactly is destroyed. To overcome this limitation an extra
/// game object with a kinematic <see cref="Rigidbody"/> can be used:
/// <code><![CDATA[
/// class MyJoint : PartModule, IKasJointEventsListener {
///   public override void OnStart(StartState state) {
///     base.OnStart(state);
///     var jointObj = new GameObject("MyFakeRB");
///
///     // Attach new object to the part.
///     jointObj.transform.parent = part.transform;
///     jointObj.transform.localPosition = Vector3.zero;
///     jointObj.transform.localRotation = Quaternion.identity;
///     jointObj.transform.localScale = Vector3.one;
///
///     // Add active modules.
///     jointObj.AddComponent<Rigidbody>().isKinematic = true;
///     jointObj.AddComponent<FixedJoint>().connectedBody = GetTargetBody();
///     jointObj.AddComponent<BrokenJointListener>().hostPart = part;
///   }
///
///   Rigidbody GetTargetBody() {
///     return null;  // Let's pretend it returns a real target object.
///   }
///
///   /// <inheritdoc/>
///   public virtual void OnKASJointBreak(GameObject hostObj, float breakForce) {
///     // Check for hostObj when a specific joint needs to be identified.
///     if (hostObj.name == "MyFakeRB") {
///       Debug.LogWarningFormat("Joint on MyFakeRB is broken with force {0}", breakForce);
///     }
///     Destroy(hostObj);
///   }
/// }
/// ]]></code>
/// </example>
public class BrokenJointListener : MonoBehaviour,
    // Syntax sugar interfaces.
    IJointEventsListener {

  /// <summary>Part to send messages to.</summary>
  public Part hostPart;

  /// <inheritdoc/>
  public void OnJointBreak(float breakForce) {
    Debug.LogFormat("Joint on {0} is broken with force {1}. Notifying part {2}",
                    gameObject, breakForce, DbgFormatter.PartId(hostPart));
    hostPart.FindModulesImplementing<IKasJointEventsListener>()
        .ForEach(x => x.OnKASJointBreak(gameObject, breakForce));
  }
}

}  // namespace
