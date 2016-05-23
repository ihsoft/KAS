using UnityEngine;
using System;
using System.Collections;

namespace KAS {

public class KASModulePhysicChild : PartModule {
  public float mass = 0.01f;
  public GameObject physicObj;

  bool physicActive = false;
  Vector3 currentLocalPos;
  Quaternion currentLocalRot;

  // Methods
  public void Start() {
    KAS_Shared.DebugLog("Start(PhysicChild)");
    if (!physicActive) {
      var physicObjRigidbody = physicObj.AddComponent<Rigidbody>();
      physicObjRigidbody.mass = mass;
      physicObj.transform.parent = null;
      physicObjRigidbody.useGravity = true;
      physicObjRigidbody.velocity = this.part.Rigidbody.velocity;
      physicObjRigidbody.angularVelocity = this.part.Rigidbody.angularVelocity;
      FlightGlobals.addPhysicalObject(physicObj);
      physicActive = true;
    } else {
      KAS_Shared.DebugWarning("Start(PhysicChild) Physic already started !");
    }
  }

  public void OnPartPack() {
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
    if (physicActive) {
      var physicObjRigidbody = physicObj.GetComponent<Rigidbody>();
      if (physicObjRigidbody.isKinematic) {
        KAS_Shared.DebugLog("OnPartUnpack(PhysicChild)");

        physicObj.transform.parent = null;
        //physicObj.transform.parent = part.transform;

        KAS_Shared.SetPartLocalPosRotFrom(
            physicObj.transform, part.transform, currentLocalPos, currentLocalRot);
        physicObjRigidbody.isKinematic = false;
        FlightGlobals.addPhysicalObject(physicObj);
        StartCoroutine(WaitPhysicUpdate());
      }
    }
  }

  private IEnumerator WaitPhysicUpdate() {
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

  public void Stop() {
    KAS_Shared.DebugLog("Stop(PhysicChild)");
    if (physicActive) {
      FlightGlobals.removePhysicalObject(physicObj);
      UnityEngine.Object.Destroy(physicObj.GetComponent<Rigidbody>());
      physicObj.transform.parent = part.transform;
      physicActive = false;
    } else {
      KAS_Shared.DebugWarning("Stop(PhysicChild) Physic already stopped !");
    }
  }

  private void OnDestroy() {
    KAS_Shared.DebugLog("OnDestroy(PhysicChild)");
    if (physicActive) {
      Stop();
    }
  }
}

}  // namespace
