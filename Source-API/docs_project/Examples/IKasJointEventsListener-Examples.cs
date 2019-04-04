// Kerbal Attachment System - Examples
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
using KSPDev.LogUtils;
using UnityEngine;

namespace Examples {

#region BrokenJointListenerExample 
class BrokenJointListenerExample : PartModule, IKasJointEventsListener {
  /// <inheritdoc/>
  public virtual void OnKASJointBreak(GameObject hostObj, float breakForce) {
    if (hostObj.name == "MyFakeRB") {
      DebugEx.Warning("Joint on MyFakeRB is broken with force {0}", breakForce);
      Destroy(hostObj);
    }
  }
}
#endregion

};  // namespace
