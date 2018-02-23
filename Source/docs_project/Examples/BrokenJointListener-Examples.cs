// Kerbal Attachment System - Examples
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using KAS;
using KASAPIv1;
using UnityEngine;

namespace Examples {

#region BrokenJointListenerExample 
class BrokenJointListenerExample : PartModule, IKasJointEventsListener {

  // Creates a physical link between the part and an arbitrary rigidbody. In case of this link gets
  // broken the part's joint will not be affected.  
  public void MakePhysicalLink(Rigidbody targetRb) {
    // Create an object that will receive the joint break event.
    var jointObj = new GameObject("MyFakeRB");
    jointObj.transform.localPosition = Vector3.zero;
    jointObj.transform.localRotation = Quaternion.identity;
    jointObj.transform.localScale = Vector3.one;
    jointObj.AddComponent<Rigidbody>();

    // Create a rigid joint with the part's RB.
    var rigidJoint = jointObj.AddComponent<FixedJoint>();
    rigidJoint.connectedBody = part.rb;
    rigidJoint.breakForce = Mathf.Infinity;  // Unbreakable.
    rigidJoint.breakTorque = Mathf.Infinity;  // Unbreakable.

    // Connect to the target and add a listener.
    var targetJoint = jointObj.AddComponent<FixedJoint>();
    targetJoint.connectedBody = targetRb;
    targetJoint.breakForce = 10;
    targetJoint.breakTorque = 10;

    // All modules on the host part that implement IKasJointEventsListener will be notified.
    jointObj.AddComponent<KASInternalBrokenJointListener>().hostPart = part;
  }

  /// <inheritdoc/>
  public virtual void OnKASJointBreak(GameObject hostObj, float breakForce) {
    // Ensure that the joint being destoyed was created by this module since the event is global
    // for the part.
    if (hostObj.name == "MyFakeRB") {
      Debug.LogWarningFormat("Joint on MyFakeRB is broken with force {0}", breakForce);
      Destroy(hostObj);
    }
  }
}
#endregion

};  // namespace
