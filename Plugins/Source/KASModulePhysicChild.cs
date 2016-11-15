using UnityEngine;
using System;
using System.Collections;

namespace KAS {

/// <summary>
/// A module for a disconnected object that should be affected by the normal physics. 
/// </summary>
/// <remarks>It's expected the object is somehow related to the part. If part gets destroyed then
/// the physical child get destroyed as well.</remarks>
public class KASModulePhysicChild : PartModule {
  /// <summary>Local position to save during (un)packing.</summary>
  Vector3 currentLocalPos;
  /// <summary>Local roattion to save during (un)packing.</summary>
  Quaternion currentLocalRot;
  /// <summary>A physics object. This module doesn't own it.</summary>
  GameObject physicObj;
  /// <summary>Cached rigidbody of the physics object.</summary>
  Rigidbody physicObjRb;

  /// <summary>Starts physics handling on the object.</summary>
  /// <remarks>The object is expected to not have rigidbody. The one will be added with the proper
  /// mass and velocity settings. Parent transform of the physics object will be set top
  /// <c>null</c>, and it will become an idependent object.</remarks>
  /// <param name="physicObj">Game object to attach physics to. In normal case it's never a part's
  /// gameobject.</param>
  /// <param name="mass">Mass of the rigidbody.</param>
  /// <param name="delayPhysics">If default or <c>false</c> then new object gets parent's velocity
  /// immediately. Otherwise, the rigidbody is created as kinematic and velocity is sync'ed in the
  /// next <c>FixedUpdate()</c> call.</param>
  public void StartPhysics(GameObject physicObj, float mass, bool delayPhysics = false) {
    KAS_Shared.DebugLog("StartPhysics(PhysicChild)");
    if (this.physicObj == null) {
      this.physicObj = physicObj;
      physicObjRb = physicObj.AddComponent<Rigidbody>();
      physicObjRb.mass = mass;
      physicObjRb.useGravity = false;
      if (delayPhysics) {
        physicObjRb.isKinematic = true;
        StartCoroutine(WaitAndPromoteToPhysic());
      } else {
        physicObjRb.velocity = part.Rigidbody.velocity;
        physicObjRb.angularVelocity = part.Rigidbody.angularVelocity;
        physicObj.transform.parent = null;
      }
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
      StartCoroutine(WaitAndUpdateVelocities());
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
      StartCoroutine(WaitAndUpdateVelocities());
    }
  }

  /// <summary>Overriden from MonoBehaviour.</summary>
  void OnDestroy() {
    KAS_Shared.DebugLog("OnDestroy(PhysicChild)");
    if (physicObjRb != null) {
      StopPhysics();
    }
  }

  /// <summary>Overriden from MonoBehaviour.</summary>
  void FixedUpdate() {
    if (physicObjRb != null && !physicObjRb.isKinematic) {
      physicObjRb.AddForce(vessel.precalc.integrationAccel, ForceMode.Acceleration);
    }
  }

  /// <summary>Aligns position, rotation and velocity of the rigidbody.</summary>
  /// <remarks>The update is delayed till the next fixed update to let game's physics to work.
  /// </remarks>
  /// <returns>Nothing.</returns>
  IEnumerator WaitAndUpdateVelocities() {
    yield return new WaitForFixedUpdate();
    KAS_Shared.SetPartLocalPosRotFrom(
        physicObj.transform, part.transform, currentLocalPos, currentLocalRot);
    if (!physicObjRb.isKinematic) {
      Debug.LogFormat("Set velocity to: {0} | angular velocity: {1}",
                      part.Rigidbody.velocity, part.Rigidbody.angularVelocity);
      physicObjRb.angularVelocity = part.Rigidbody.angularVelocity;
      physicObjRb.velocity = part.Rigidbody.velocity;
    }
  }

  /// <summary>Turns rigidbody to a physics object.</summary>
  /// <remarks>The update is delayed till the next fixed update to let game's physics to work. Once
  /// updated the physical object is guaranteed to be detached from the parent.</remarks>
  /// <returns>Nothing.</returns>
  IEnumerator WaitAndPromoteToPhysic() {
    yield return new WaitForFixedUpdate();
    Debug.LogFormat("Delayed set velocity to: {0} | angular velocity: {1}",
                    part.Rigidbody.velocity, part.Rigidbody.angularVelocity);
    physicObjRb.angularVelocity = part.Rigidbody.angularVelocity;
    physicObjRb.velocity = part.Rigidbody.velocity;
    physicObjRb.isKinematic = false;
    physicObj.transform.parent = null;
  }
}

}  // namespace
