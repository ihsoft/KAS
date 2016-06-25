using UnityEngine;
using System;
using System.Collections;

namespace KAS {

public class KASModulePhysicChild : PartModule {
  Vector3 currentLocalPos;
  Quaternion currentLocalRot;
  GameObject physicObj;
  Rigidbody physicObjRb;

  /// <summary>Starts physics handling on the object.</summary>
  /// <remarks>The object is expected to not have Rigidbody. The one will be added with the proper
  /// mass and velocity settings.</remarks>
  public void StartPhysics(GameObject physicObj, float mass) {
    KAS_Shared.DebugLog("StartPhysics(PhysicChild)");
    if (this.physicObj == null) {
      this.physicObj = physicObj;
      physicObjRb = physicObj.AddComponent<Rigidbody>();
      physicObj.transform.parent = null;
      physicObjRb.mass = mass;
      physicObjRb.useGravity = false;
      physicObjRb.velocity = part.Rigidbody.velocity;
      physicObjRb.angularVelocity = part.Rigidbody.angularVelocity;
    } else {
      KAS_Shared.DebugWarning("StartPhysics(PhysicChild) Physic already started! Ignore.");
    }
  }

  /// <summary>Stops physics handling on the object.</summary>
  /// <remarks>Rigidbody on the object gets destroyed.</remarks>
  public void StopPhysics() {
    KAS_Shared.DebugLog("StopPhysics(PhysicChild)");
    if (physicObj != null) {
      UnityEngine.Object.Destroy(physicObjRb);
      physicObjRb = null;
      physicObj.transform.parent = part.transform;
      physicObj = null;
    } else {
      KAS_Shared.DebugWarning("StopPhysics(PhysicChild) Physic already stopped! Ignore.");
    }
  }

  /// <summary>Part's message handler.</summary>
  /// <remarks>Temporarily suspends physics handling on the object.</remarks>
  void OnPartPack() {
    if (physicObj != null) {
      KAS_Shared.DebugLog("OnPartPack(PhysicChild)");
      currentLocalPos = KAS_Shared.GetLocalPosFrom(physicObj.transform, part.transform);
      currentLocalRot = KAS_Shared.GetLocalRotFrom(physicObj.transform, part.transform);
      physicObjRb.isKinematic = true;
      physicObj.transform.parent = part.transform;
      StartCoroutine(WaitPhysicUpdate());
    }
  }

  /// <summary>Part's message handler.</summary>
  /// <remarks>Resumes physics handling on the object.</remarks>
  void OnPartUnpack() {
    if (physicObj != null && physicObjRb.isKinematic) {
      KAS_Shared.DebugLog("OnPartUnpack(PhysicChild)");
      physicObj.transform.parent = null;
      KAS_Shared.SetPartLocalPosRotFrom(
          physicObj.transform, part.transform, currentLocalPos, currentLocalRot);
      physicObjRb.isKinematic = false;
      StartCoroutine(WaitPhysicUpdate());
    }
  }

  /// <summary>Overriden from MonoBehavior.</summary>
  void OnDestroy() {
    KAS_Shared.DebugLog("OnDestroy(PhysicChild)");
    if (physicObjRb != null) {
      StopPhysics();
    }
  }

  /// <summary>Overriden from MonoBehavior.</summary>
  void FixedUpdate() {
    if (physicObjRb != null && !physicObjRb.isKinematic) {
      physicObjRb.AddForce(part.vessel.graviticAcceleration, ForceMode.Acceleration);
    }
  }

  /// <summary>Aligns position, rotation and velocity of the rigidbody.</summary>
  /// <remarks>The update is delayed till the next fixed update to let game's physics to work.
  /// </remarks>
  /// <returns>Nothing.</returns>
  IEnumerator WaitPhysicUpdate() {
    yield return new WaitForFixedUpdate();
    KAS_Shared.SetPartLocalPosRotFrom(
        physicObj.transform, part.transform, currentLocalPos, currentLocalRot);
    if (!physicObjRb.isKinematic) {
      KAS_Shared.DebugLog(
          "WaitPhysicUpdate(PhysicChild) Set velocity to: {0} | angular velocity: {1}",
          part.Rigidbody.velocity, part.Rigidbody.angularVelocity);
      physicObjRb.angularVelocity = part.Rigidbody.angularVelocity;
      physicObjRb.velocity = part.Rigidbody.velocity;
    }
  }
}

}  // namespace
