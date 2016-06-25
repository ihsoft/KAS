using UnityEngine;
using System;
using System.Collections;

namespace KAS {

public class KASModulePhysicChild : PartModule {
  public float mass = 0.01f;
  public GameObject physicObj;

  bool physicActive;
  Vector3 currentLocalPos;
  Quaternion currentLocalRot;

  public void Start() {
    KAS_Shared.DebugLog("Start(PhysicChild)");
  /// <summary>Starts physics handling on the object.</summary>
  /// <remarks>The object is expected to not have Rigidbody. The one will be added with the proper
  /// mass and velocity settings.</remarks>
    if (!physicActive) {
      var physicObjRigidbody = physicObj.AddComponent<Rigidbody>();
      physicObjRigidbody.mass = mass;
      physicObj.transform.parent = null;
      physicObjRigidbody.useGravity = false;
      physicObjRigidbody.velocity = part.Rigidbody.velocity;
      physicObjRigidbody.angularVelocity = part.Rigidbody.angularVelocity;
      FlightGlobals.addPhysicalObject(physicObj);
      physicActive = true;
    } else {
      KAS_Shared.DebugWarning("Start(PhysicChild) Physic already started !");
    }
  }

  public void Stop() {
    KAS_Shared.DebugLog("Stop(PhysicChild)");
  /// <summary>Stops physics handling on the object.</summary>
  /// <remarks>Rigidbody on the object gets destroyed.</remarks>
    if (physicActive) {
      UnityEngine.Object.Destroy(physicObj.GetComponent<Rigidbody>());
      physicObj.transform.parent = part.transform;
      physicActive = false;
    } else {
      KAS_Shared.DebugWarning("Stop(PhysicChild) Physic already stopped !");
    }
  }

  public void OnPartPack() {
  /// <summary>Part's message handler.</summary>
  /// <remarks>Temporarily suspends physics handling on the object.</remarks>
    if (physicActive) {
      KAS_Shared.DebugLog("OnPartPack(PhysicChild)");
      currentLocalPos = KAS_Shared.GetLocalPosFrom(physicObj.transform, part.transform);
      currentLocalRot = KAS_Shared.GetLocalRotFrom(physicObj.transform, part.transform);
      FlightGlobals.removePhysicalObject(physicObj);
      physicObj.GetComponent<Rigidbody>().isKinematic = true;
      physicObj.transform.parent = part.transform;
      StartCoroutine(WaitPhysicUpdate());
    }
  }

  public void OnPartUnpack() {
  /// <summary>Part's message handler.</summary>
  /// <remarks>Resumes physics handling on the object.</remarks>
    if (physicActive) {
      var physicObjRigidbody = physicObj.GetComponent<Rigidbody>();
      if (physicObjRigidbody.isKinematic) {
        KAS_Shared.DebugLog("OnPartUnpack(PhysicChild)");
        physicObj.transform.parent = null;
        KAS_Shared.SetPartLocalPosRotFrom(
            physicObj.transform, part.transform, currentLocalPos, currentLocalRot);
        physicObjRigidbody.isKinematic = false;
        FlightGlobals.addPhysicalObject(physicObj);
        StartCoroutine(WaitPhysicUpdate());
      }
    }
  }

  /// <summary>Overriden from MonoBehavior.</summary>
  void OnDestroy() {
    KAS_Shared.DebugLog("OnDestroy(PhysicChild)");
    if (physicActive) {
      Stop();
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
    var physicObjRigidbody = physicObj.GetComponent<Rigidbody>();
    if (!physicObjRigidbody.isKinematic) {
      KAS_Shared.DebugLog(string.Format(
          "WaitPhysicUpdate(PhysicChild) Set velocity to: {0} | angular velocity: {1}",
          part.Rigidbody.velocity, part.Rigidbody.angularVelocity));
      physicObjRigidbody.angularVelocity = part.Rigidbody.angularVelocity;
      physicObjRigidbody.velocity = part.Rigidbody.velocity;
    }
  }
}

}  // namespace
