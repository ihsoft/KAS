using UnityEngine;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace KAS
{
    public class KASModulePhysicChild : PartModule
{
    public float mass = 0.01f;
    public GameObject physicObj;

    private bool physicActive = false;
    private Vector3 currentLocalPos;
    private Quaternion currentLocalRot;

    // Methods
    public void Start()
    {
        KAS_Shared.DebugLog("Start(PhysicChild)");
        if (!physicActive)
        {
            physicObj.AddComponent<Rigidbody>();
            physicObj.rigidbody.mass = mass;
            physicObj.transform.parent = null;
            physicObj.rigidbody.useGravity = true;
            physicObj.rigidbody.velocity = this.part.rigidbody.velocity;
            physicObj.rigidbody.angularVelocity = this.part.rigidbody.angularVelocity;
            FlightGlobals.addPhysicalObject(physicObj);
            physicActive = true;
        }
        else
        {
            KAS_Shared.DebugWarning("Start(PhysicChild) Physic already started !");
        }
    }

    public void OnPartPack()
    {
        if (physicActive)
        {
            KAS_Shared.DebugLog("OnPartPack(PhysicChild)");
            currentLocalPos = KAS_Shared.GetLocalPosFrom(physicObj.transform, this.part.transform);
            currentLocalRot = KAS_Shared.GetLocalRotFrom(physicObj.transform, this.part.transform);
            FlightGlobals.removePhysicalObject(physicObj);
            physicObj.rigidbody.isKinematic = true;
            physicObj.transform.parent = this.part.transform;
            StartCoroutine(WaitPhysicUpdate());
        }
    }

    public void OnPartUnpack()
    {
        if (physicActive)
        {
            if (physicObj.rigidbody.isKinematic)
            {
                KAS_Shared.DebugLog("OnPartUnpack(PhysicChild)");
                physicObj.transform.parent = null;
                KAS_Shared.SetPartLocalPosRotFrom(physicObj.transform, this.part.transform, currentLocalPos, currentLocalRot);
                physicObj.rigidbody.isKinematic = false;
                FlightGlobals.addPhysicalObject(physicObj);
                StartCoroutine(WaitPhysicUpdate());
            }
        }
    }

    private IEnumerator WaitPhysicUpdate()
    {
        yield return new WaitForFixedUpdate();
        KAS_Shared.SetPartLocalPosRotFrom(physicObj.transform, this.part.transform, currentLocalPos, currentLocalRot);
        if (physicObj.rigidbody.isKinematic == false)
        {
            KAS_Shared.DebugLog("WaitPhysicUpdate(PhysicChild) Set velocity to : " + this.part.rigidbody.velocity + " | angular velocity : " + this.part.rigidbody.angularVelocity);
            physicObj.rigidbody.angularVelocity = this.part.rigidbody.angularVelocity;
            physicObj.rigidbody.velocity = this.part.rigidbody.velocity;
        }
    }

    public void Stop()
    {
        KAS_Shared.DebugLog("Stop(PhysicChild)");
        if (physicActive)
        {
            FlightGlobals.removePhysicalObject(physicObj);
            UnityEngine.Object.Destroy(physicObj.rigidbody);
            physicObj.transform.parent = this.part.transform;
            physicActive = false;
        }
        else
        {
            KAS_Shared.DebugWarning("Stop(PhysicChild) Physic already stopped !");
        }
    }

    private void OnDestroy()
    {
        KAS_Shared.DebugLog("OnDestroy(PhysicChild)");
        if (physicActive) Stop();
    }
}


}
