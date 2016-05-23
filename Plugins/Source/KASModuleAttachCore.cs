using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace KAS {
 
public class KASModuleAttachCore : PartModule {
  // Fixed attach handling
  public FxAttach FixedAttach;
  public struct FxAttach {
    public FixedJoint fixedJoint;
    public Part srcPart;
    public Part tgtPart;
    public bool createJointOnUnpack;
    public string savedSrcPartID;
    public string savedSrcVesselID;
    public string savedTgtPartID;
    public string savedTgtVesselID;
    public float savedBreakForce;
  }

  // Static attach handling
  public StAttach StaticAttach;
  public struct StAttach {
    public FixedJoint fixedJoint;
    public GameObject connectedGameObject;
  }

  // Docking attach handling
  public KASModuleAttachCore dockedAttachModule = null;
  private DockedVesselInfo vesselInfo = null;
  private string dockedPartID = null;

  // Common
  public struct PhysicObjInfo {
    public Vector3 orgPos;
    public Quaternion orgRot;
    public string savedTransformName;
    public float savedMass;
    public Vector3 savedLocalPos;
    public Quaternion savedLocalRot;
  }

  public AttachModeInfo attachMode;
  public struct AttachModeInfo {
    public bool Docked;
    public bool Coupled;
    public bool FixedJoint;
    public bool StaticJoint;
  }

  public enum AttachType {
    Docked = 1,
    Coupled = 2,
    FixedJoint = 3,
    StaticJoint = 4,
  }

  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    if (attachMode.FixedJoint) {
      KAS_Shared.DebugLog("OnSave(Core) Fixed joint detected, saving info...");
      ConfigNode FxNode = node.AddNode("FIXEDATTACH");
      FxNode.AddValue("srcPartID", FixedAttach.srcPart.flightID.ToString());
      FxNode.AddValue("srcVesselID", FixedAttach.srcPart.vessel.id.ToString());
      FxNode.AddValue("tgtPartID", FixedAttach.tgtPart.flightID.ToString());
      FxNode.AddValue("tgtVesselID", FixedAttach.tgtPart.vessel.id.ToString());
      if (FixedAttach.fixedJoint) {
        KAS_Shared.DebugLog(string.Format("OnSave(Core) Saving breakforce from joint config : {0}",
                                          FixedAttach.fixedJoint.breakForce));
        FxNode.AddValue("breakForce", FixedAttach.fixedJoint.breakForce);
      } else {
        KAS_Shared.DebugLog(string.Format(
            "OnSave(Core) Saving breakforce from saved : {0}", FixedAttach.savedBreakForce));
        FxNode.AddValue("breakForce", FixedAttach.savedBreakForce);
      }
    }
    if (attachMode.StaticJoint) {
      KAS_Shared.DebugLog("OnSave(Core) Static joint detected, saving info...");
      node.AddValue("StaticJoint", "True");
    }
    if (attachMode.Docked) {
      KAS_Shared.DebugLog("OnSave(Core) Docked joint detected, saving info...");
      if (dockedAttachModule) {
        node.AddValue("dockedPartID", dockedAttachModule.part.flightID.ToString());
        ConfigNode nodeD = node.AddNode("DOCKEDVESSEL");
        this.vesselInfo.Save(nodeD);
      } else {
        KAS_Shared.DebugError("OnSave(Core) dockedAttachModule is null!");
      }         
    }
  }

  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    if (node.HasNode("FIXEDATTACH")) {
      ConfigNode FxNode = node.GetNode("FIXEDATTACH");
      KAS_Shared.DebugLog("OnLoad(Core) Loading fixed joint info from save...");
      if (FxNode.HasValue("srcPartID") && FxNode.HasValue("srcVesselID")
          && FxNode.HasValue("tgtPartID") && FxNode.HasValue("tgtVesselID")
          && FxNode.HasValue("breakForce")) {
        FixedAttach.savedSrcPartID = FxNode.GetValue("srcPartID").ToString();
        FixedAttach.savedSrcVesselID = FxNode.GetValue("srcVesselID").ToString();
        FixedAttach.savedTgtPartID = FxNode.GetValue("tgtPartID").ToString();
        FixedAttach.savedTgtVesselID = FxNode.GetValue("tgtVesselID").ToString();
        FixedAttach.savedBreakForce = float.Parse(FxNode.GetValue("breakForce"));
        attachMode.FixedJoint = true;
      } else {
        KAS_Shared.DebugWarning("OnLoad(Core) Missing node value(s)!");
      }
    }
    if (node.HasNode("DOCKEDVESSEL") && node.HasValue("dockedPartID")) {
      KAS_Shared.DebugLog("OnLoad(Core) Loading docked info from save...");
      vesselInfo = new DockedVesselInfo();
      vesselInfo.Load(node.GetNode("DOCKEDVESSEL"));
      dockedPartID = node.GetValue("dockedPartID").ToString();
      attachMode.Docked = true;
    }
    if (node.HasValue("StaticJoint")) {
      attachMode.StaticJoint = true;
    }
  }

  public override void OnStart(StartState state) {
    base.OnStart(state);
    if (state == StartState.Editor || state == StartState.None) {
      return;
    }

    if (attachMode.Docked) {
      Part dockedPart = KAS_Shared.GetPartByID(this.vessel, dockedPartID);
      if (dockedPart && (dockedPart == part.parent || dockedPart.parent == part)) {
        KASModuleAttachCore dockedAttachModuleTmp = dockedPart.GetComponent<KASModuleAttachCore>();
        if (dockedAttachModuleTmp == null) {
          KAS_Shared.DebugError("OnLoad(Core) Unable to get docked module!");
          attachMode.Docked = false;
        } else if (dockedAttachModuleTmp.attachMode.Docked
                   && dockedAttachModuleTmp.dockedPartID == part.flightID.ToString()
                   && dockedAttachModuleTmp.vesselInfo != null) {
          KAS_Shared.DebugLog(string.Format("OnLoad(Core) Part already docked to {0}",
                                            dockedAttachModuleTmp.part.partInfo.title));
          this.dockedAttachModule = dockedAttachModuleTmp;
          dockedAttachModuleTmp.dockedAttachModule = this;
        } else {
          KAS_Shared.DebugLog(string.Format(
              "OnLoad(Core) Re-set docking on {0}", dockedAttachModuleTmp.part.partInfo.title));
          AttachDocked(dockedAttachModuleTmp);
        }
      } else {
        KAS_Shared.DebugError("OnLoad(Core) Unable to get saved docked part!");
        attachMode.Docked = false;
      }
    }

    // TODO: attachMode.Coupled

    if (attachMode.FixedJoint) {
      StartCoroutine(WaitAndInitFixedAttach());
    }
    // Nothing to do on attachMode.StaticJoint (see OnVesselLoaded)
  }

  public virtual void OnPartPack() {
    if (StaticAttach.connectedGameObject) {
      Destroy(StaticAttach.connectedGameObject);
    }
  }

  private IEnumerator<YieldInstruction> WaitAndInitFixedAttach() {
    yield return new WaitForEndOfFrame();

    InitFixedAttach();
  }

  protected virtual void InitFixedAttach() {
    if (attachMode.FixedJoint) {
      Part srcPart = KAS_Shared.GetPartByID(
          FixedAttach.savedSrcVesselID, FixedAttach.savedSrcPartID);
      Part tgtPart = KAS_Shared.GetPartByID(
          FixedAttach.savedTgtVesselID, FixedAttach.savedTgtPartID);
      if (tgtPart) {
        KAS_Shared.DebugLog(string.Format(
            "OnLoad(Core) Re-set fixed joint on {0}", tgtPart.partInfo.title));
        AttachFixed(srcPart, tgtPart, FixedAttach.savedBreakForce);
      } else {
        KAS_Shared.DebugError(
            "OnLoad(Core) Unable to get saved connected part of the fixed joint !");
        attachMode.FixedJoint = false;
      }
    }
  }

  public virtual void OnPartUnpack() {
    if (attachMode.StaticJoint) {
      KAS_Shared.DebugLog("OnVesselGoOffRails(Core) Re-attach static object");
      AttachStatic();
    }
  }

  protected virtual void OnJointBreak(float breakForce) {
    KAS_Shared.DebugWarning(string.Format(
        "OnJointBreak(Core) A joint broken on {0} !, force: {1}", part.partInfo.title, breakForce));
    StartCoroutine(WaitAndCheckJoint());
  }

  private IEnumerator WaitAndCheckJoint() {
    yield return new WaitForFixedUpdate();
    if (attachMode.StaticJoint) {
      if (StaticAttach.fixedJoint == null) {
        KAS_Shared.DebugWarning("WaitAndCheckJoint(Core) Static join broken !");
        OnJointBreakStatic();
      } 
    }
    if (attachMode.FixedJoint) {
      if (FixedAttach.fixedJoint == null) {
        KAS_Shared.DebugWarning("WaitAndCheckJoint(Core) Fixed join broken !");
        OnJointBreakFixed();
      }
    }
  }

  public virtual void OnJointBreakStatic() {
    Detach(AttachType.StaticJoint);
  }

  public virtual void OnJointBreakFixed() {
    Detach(AttachType.FixedJoint);
  }

  protected virtual void OnDestroy() {
    SetCreateJointOnUnpack(false);

    if (StaticAttach.connectedGameObject) {
      Destroy(StaticAttach.connectedGameObject);
    }
  }

  protected virtual void OnPartDie() {
    if (attachMode.Docked) {
      if (dockedAttachModule && dockedAttachModule.dockedAttachModule == this) {
        dockedAttachModule.attachMode.Docked = false;
        dockedAttachModule.dockedAttachModule = null;
      }

      attachMode.Docked = false;
      dockedAttachModule = null;
    }
  }

  private void SetCreateJointOnUnpack(bool newval) {
    if (FixedAttach.createJointOnUnpack != newval) {
      FixedAttach.createJointOnUnpack = newval;

      if (newval) {
        GameEvents.onVesselGoOffRails.Add(new EventData<Vessel>.OnEvent(this.OnVesselGoOffRails));
      } else {
        GameEvents.onVesselGoOffRails.Remove(
            new EventData<Vessel>.OnEvent(this.OnVesselGoOffRails));
      }
    }
  }

  private void OnVesselGoOffRails(Vessel vess) {
    if (FixedAttach.createJointOnUnpack
        && (vess == FixedAttach.srcPart.vessel || vess == FixedAttach.tgtPart.vessel)) {
      KAS_Shared.DebugWarning(
          "OnUpdate(Core) Fixed attach set and both part unpacked, creating fixed joint...");
      AttachFixed(FixedAttach.srcPart, FixedAttach.tgtPart, FixedAttach.savedBreakForce);
      SetCreateJointOnUnpack(false);
    }
  }

  public void MoveAbove(Vector3 position, Vector3 normal, float distance) {
    //Find position above the surface   
    Vector3 hitAbove = position + (normal * distance);
    //Find the rotation aligned with the object surface
    Quaternion alignedRotation = Quaternion.FromToRotation(Vector3.up, normal);
    //Set position and rotation
    this.part.transform.position = hitAbove;
    this.part.transform.rotation = alignedRotation;
  }

  public void AttachFixed(Part srcPart, Part tgtPart, float breakForce) {
    attachMode.FixedJoint = true;
    FixedAttach.srcPart = srcPart;
    FixedAttach.tgtPart = tgtPart;

    if (!srcPart.packed && !tgtPart.packed) {
      KAS_Shared.DebugLog("AttachFixed(Core) Create fixed joint on " + srcPart.partInfo.title
                          + " with " + tgtPart.partInfo.title);
      if (FixedAttach.fixedJoint)
        Destroy(FixedAttach.fixedJoint);
      FixedAttach.fixedJoint = srcPart.gameObject.AddComponent<FixedJoint>();
      FixedAttach.fixedJoint.connectedBody = tgtPart.rb;
      FixedAttach.fixedJoint.breakForce = breakForce;
      FixedAttach.fixedJoint.breakTorque = breakForce;
    } else {
      SetCreateJointOnUnpack(true);
      KAS_Shared.DebugWarning("AttachFixed(Core) Cannot create fixed joint as part(s) is packed,"
                              + " delaying to unpack...");
    }
  }

  public void AttachStatic(float breakForce = 10) {
    KAS_Shared.DebugLog("JointToStatic(Base) Create kinematic rigidbody");
    if (StaticAttach.connectedGameObject) {
      Destroy(StaticAttach.connectedGameObject);
    }
    var obj = new GameObject("KASBody");
    var objRigidbody = obj.AddComponent<Rigidbody>();
    objRigidbody.isKinematic = true;
    obj.transform.position = this.part.transform.position;
    obj.transform.rotation = this.part.transform.rotation;
    StaticAttach.connectedGameObject = obj;

    KAS_Shared.DebugLog("JointToStatic(Base) Create fixed joint on the kinematic rigidbody");
    if (StaticAttach.fixedJoint) {
      Destroy(StaticAttach.fixedJoint);
    }
    FixedJoint CurJoint = this.part.gameObject.AddComponent<FixedJoint>();
    CurJoint.breakForce = breakForce;
    CurJoint.breakTorque = breakForce;
    // FIXME: Just don't set connected body when attaching to static.
    CurJoint.connectedBody = objRigidbody;
    StaticAttach.fixedJoint = CurJoint;
    attachMode.StaticJoint = true;
  }

  public void AttachDocked(KASModuleAttachCore otherAttachModule, Vessel forceDominant = null) {
    // Don't overwrite vesselInfo on redundant calls
    if (part.vessel == otherAttachModule.part.vessel
        && attachMode.Docked && dockedAttachModule == otherAttachModule
        && otherAttachModule.attachMode.Docked && otherAttachModule.dockedAttachModule == this
        && vesselInfo != null && otherAttachModule.vesselInfo != null) {
      KAS_Shared.DebugWarning("DockTo(Core) Parts already docked, nothing to do at all");
      return;
    }

    // Save vessel Info
    vesselInfo = new DockedVesselInfo();
    vesselInfo.name = vessel.vesselName;
    vesselInfo.vesselType = vessel.vesselType;
    vesselInfo.rootPartUId = vessel.rootPart.flightID;
    dockedAttachModule = otherAttachModule;
    dockedPartID = otherAttachModule.part.flightID.ToString();

    otherAttachModule.vesselInfo = new DockedVesselInfo();
    otherAttachModule.vesselInfo.name = otherAttachModule.vessel.vesselName;
    otherAttachModule.vesselInfo.vesselType = otherAttachModule.vessel.vesselType;
    otherAttachModule.vesselInfo.rootPartUId = otherAttachModule.vessel.rootPart.flightID;
    otherAttachModule.dockedAttachModule = this;
    otherAttachModule.dockedPartID = part.flightID.ToString();

    // Set reference
    attachMode.Docked = true;
    otherAttachModule.attachMode.Docked = true;

    // Stop if already docked
    if (otherAttachModule.part.parent == part || part.parent == otherAttachModule.part) {
      KAS_Shared.DebugWarning("DockTo(Core) Parts already docked, nothing more to do");
      return;
    }

    // This results in a somewhat wrong state, but it's better to not make it even more wrong.
    if (otherAttachModule.part.vessel == part.vessel) {
      KAS_Shared.DebugWarning("DockTo(Core) BUG: Parts belong to the same vessel, doing nothing");
      return;
    }

    // Reset vessels position and rotation for returning all parts to their original position and
    // rotation before coupling
    vessel.SetPosition(vessel.transform.position, true);
    vessel.SetRotation(vessel.transform.rotation);
    otherAttachModule.vessel.SetPosition(otherAttachModule.vessel.transform.position, true);
    otherAttachModule.vessel.SetRotation(otherAttachModule.vessel.transform.rotation);
          
    // Couple depending of mass

    Vessel dominantVessel = GetDominantVessel(this.vessel, otherAttachModule.vessel);

    if (forceDominant == this.vessel || forceDominant == otherAttachModule.vessel) {
      dominantVessel = forceDominant;
    }

    KAS_Shared.DebugLog(string.Format("DockTo(Core) Master vessel is {0}",
                                      dominantVessel.vesselName));
          
    if (dominantVessel == this.vessel) {
      KAS_Shared.DebugLog(string.Format("DockTo(Core) Docking {0} from {1} with {2} from {3}",
                                        otherAttachModule.part.partInfo.title,
                                        otherAttachModule.vessel.vesselName,
                                        part.partInfo.title,
                                        vessel.vesselName));
      if (FlightGlobals.ActiveVessel == otherAttachModule.part.vessel) {
        KAS_Shared.DebugLog(string.Format("DockTo(Core) Switching focus to {0}",
                                          this.part.vessel.vesselName));
        FlightGlobals.ForceSetActiveVessel(this.part.vessel);
        FlightInputHandler.ResumeVesselCtrlState(this.part.vessel);
      }
      otherAttachModule.part.Couple(this.part);
    } else {
      KAS_Shared.DebugLog(string.Format("DockTo(Core) Docking {0} from {1} with {2} from {3}",
                                        part.partInfo.title,
                                        vessel.vesselName,
                                        otherAttachModule.part.partInfo.title,
                                        otherAttachModule.vessel.vesselName));
      if (FlightGlobals.ActiveVessel == part.vessel) {
        KAS_Shared.DebugLog(string.Format("DockTo(Core) Switching focus to {0}",
                                          otherAttachModule.part.vessel.vesselName));
        FlightGlobals.ForceSetActiveVessel(otherAttachModule.part.vessel);
        FlightInputHandler.ResumeVesselCtrlState(otherAttachModule.part.vessel);
      }
      part.Couple(otherAttachModule.part);
    }

    GameEvents.onVesselWasModified.Fire(this.part.vessel);
  }

  private Vessel GetDominantVessel(Vessel v1, Vessel v2) {
    // Check 1 - Dominant vessel will be the higher type
    if (v1.vesselType > v2.vesselType) {
      return v1;
    }
    if (v1.vesselType < v2.vesselType) {
      return v2;
    }

    // Check 2- If type are the same, dominant vessel will be the heaviest
    float diffMass = Mathf.Abs((v1.GetTotalMass() - v2.GetTotalMass()));
    if (diffMass >= 0.01f) {
      return v1.GetTotalMass() <= v2.GetTotalMass() ? v2 : v1;
    }
    // Check 3 - If weight is similar, dominant vessel will be the one with the higher ID
    return v1.id.CompareTo(v2.id) <= 0 ? v2 : v1;
  }

  public void Detach() {
    if (attachMode.Docked) {
      Detach(AttachType.Docked);
    }
    if (attachMode.Coupled) {
      Detach(AttachType.Coupled);
    }
    if (attachMode.FixedJoint) {
      Detach(AttachType.FixedJoint);
    }
    if (attachMode.StaticJoint) {
      Detach(AttachType.StaticJoint);
    }
  }

  private void UndockVessel() {
    if (part.parent != null) {
      var my_node = part.findAttachNodeByPart(part.parent);
      if (my_node != null) {
        my_node.attachedPart = null;
      }

      var other_node = part.parent.findAttachNodeByPart(part);
      if (other_node != null) {
        other_node.attachedPart = null;
      }
    }

    part.Undock(vesselInfo);
  }

  public void Detach(AttachType attachType) {
    KAS_Shared.DebugLog(string.Format(
        "Detach(Base) Attach mode is Docked:{0},Coupled:{1},FixedJoint:{2},StaticJoint:{3}",
        attachMode.Docked, attachMode.Coupled, attachMode.FixedJoint, attachMode.StaticJoint));
    KAS_Shared.DebugLog(string.Format("Detach(Base) Attach type is : {0}", attachType));

    // Docked
    if (attachType == AttachType.Docked) {
      if (dockedAttachModule.part.parent == this.part) {
        KAS_Shared.DebugLog(string.Format("Detach(Base) Undocking {0} from {1}",
                                          dockedAttachModule.part.partInfo.title,
                                          dockedAttachModule.vessel.vesselName));
        dockedAttachModule.UndockVessel();
      }
      if (this.part.parent == dockedAttachModule.part) {
        KAS_Shared.DebugLog(string.Format(
            "Detach(Base) Undocking {0} from {1}", part.partInfo.title, vessel.vesselName));
        this.UndockVessel();
      }
      if (dockedAttachModule.dockedAttachModule == this) {
        dockedAttachModule.dockedAttachModule = null;
        dockedAttachModule.dockedPartID = null;
        dockedAttachModule.attachMode.Docked = false;
      }
      dockedAttachModule = null;
      dockedPartID = null;
      attachMode.Docked = false;
    }
    // Coupled
    if (attachType == AttachType.Coupled) {
      // TODO???
      attachMode.Coupled = false;
    }
    // FixedJoint
    if (attachType == AttachType.FixedJoint) {
      KAS_Shared.DebugLog(string.Format(
          "Detach(Base) Removing fixed joint on {0}", FixedAttach.srcPart.partInfo.title));
      if (FixedAttach.fixedJoint) {
        Destroy(FixedAttach.fixedJoint);
      }
      SetCreateJointOnUnpack(false);
      FixedAttach.fixedJoint = null;
      FixedAttach.tgtPart = null;
      attachMode.FixedJoint = false;
    }
    // StaticJoint
    if (attachType == AttachType.StaticJoint) {
      KAS_Shared.DebugLog(string.Format(
          "Detach(Base) Removing static rigidbody and fixed joint on {0}", part.partInfo.title));
      if (StaticAttach.fixedJoint) {
        Destroy(StaticAttach.fixedJoint);
      }
      if (StaticAttach.connectedGameObject) {
        Destroy(StaticAttach.connectedGameObject);
      }
      StaticAttach.fixedJoint = null;
      StaticAttach.connectedGameObject = null;
      attachMode.StaticJoint = false;
    }
  }
}

}  // namespace
