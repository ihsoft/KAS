using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KAS {

public class KASModuleTelescopicArm : PartModule {
  [KSPField] public string headAttachNode = "bottom";
  [KSPField] public float speed = 1;
  [KSPField] public Vector3 direction = new Vector3(0f, -1f, 0f);
  [KSPField] public float powerDrain = 1f;
  [KSPField] public float breakForce = 20;
  [KSPField] public float driveSpring = 1000;
  [KSPField] public float driveDamper = 200;
  [KSPField] public float driveForce = 10;
  [KSPField] public float boomHeadMass = 0.01f;
  [KSPField] public float headPosOffset = 100;

  //Sounds
  [KSPField] public string motorStartSndPath = "KAS/Sounds/telescopicMotorstart";
  [KSPField] public  string motorSndPath = "KAS/Sounds/telescopicMotor";
  [KSPField] public string motorStopSndPath = "KAS/Sounds/telescopicMotorstop";
  [KSPField] public string sectionSndPath = "KAS/Sounds/telescopicSection";
  public FXGroup fxSndMotorStart, fxSndMotor, fxSndMotorStop, fxSndSection;

  //Misc
  private Dictionary<int, SectionInfo> sections = new Dictionary<int, SectionInfo>();
  private Dictionary<int, Vector3> savedSectionsLocalPos = new Dictionary<int, Vector3>();
  private List<AttachNode> attachNodes = new List<AttachNode>();
  public List<FixedJoint> fixedJnts = new List<FixedJoint>();
  private KASModulePhysicChild boomHeadPhysicModule;
  private float orgBoomHeadMass;
  private bool boomHeadLoaded = false;

  [KSPField(isPersistant = true)]
  private int sectionIndex = 0;
  [KSPField(guiActive = true, guiName = "Keyboard control", guiFormat = "S")]
  public string controlField = "Activated";
  [KSPField(guiActive = true, guiName = "State", guiFormat = "S")]
  public string stateField = "Idle";
  [KSPField(guiActive = true, guiName = "Projection distance", guiFormat = "F6", guiUnits = "m")]
  public float prjDist = 0;
  [KSPField(guiActive = true, guiName = "Projection angle", guiFormat = "F6", guiUnits = "m")]
  public float prjAngle = 0;
  [KSPField(isPersistant = true, guiActive = true, guiName = "Head current position",
            guiFormat = "F6", guiUnits = "m")]
  public float headPos = 0;
  [KSPField(isPersistant = true, guiActive = true, guiName = "Head target position",
            guiFormat = "F6", guiUnits = "m")]
  public float targetPos = 0;
  [KSPField(isPersistant = true)]
  private bool controlActivated = true;
  [KSPField(isPersistant = true)]
  private bool controlInverted = false;

  private float sectionTotalLenght = 0;
  private moveHead extend;
  private moveHead retract;
  public ConfigurableJoint slideJoint;
  private float driveWay;
  public bool driveActived = false;
  public DriveState driveState = DriveState.Stopped;

  public enum DriveState {
    Stopped = 0,
    Extending = 1,
    Retracting = 2,
  }
      
  public class SectionInfo {
    public Transform transform;
    public float lenght;
    public Vector3 orgLocalPos;
    public Quaternion orgLocalRot;
    public Vector3 savedLocalPos;
  }

  public struct moveHead {
    public bool active;
    public bool starting;
    public bool isrunning;
    public bool full;
  }

  public override string GetInfo() {
    var sb = new StringBuilder();
    sb.AppendFormat("<b>Speed</b>: {0:F0}", speed);
    sb.AppendLine();
    sb.AppendFormat("<b>Drive force</b>: {0:F0}", driveForce);
    sb.AppendLine();
    sb.AppendFormat("<b>Power consumption</b>: {0:F1}/s", powerDrain);
    sb.AppendLine();
    return sb.ToString();
  }
      
  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    foreach (KeyValuePair<int, SectionInfo> section in sections) {
      ConfigNode sectionNode = node.AddNode("SECTIONPOS");
      sectionNode.AddValue("index", section.Key);
      sectionNode.AddValue(
          "localPos",
          KSPUtil.WriteVector(KAS_Shared.GetLocalPosFrom(section.Value.transform, part.transform)));
      sectionNode.AddValue(
          "localRot",
          KSPUtil.WriteQuaternion(KAS_Shared.GetLocalRotFrom(section.Value.transform,
                                                             part.transform)));
    }
  }

  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    foreach (ConfigNode sectionPosNode in node.GetNodes("SECTIONPOS")) {
      if (sectionPosNode.HasValue("index") && sectionPosNode.HasValue("localPos")
          && sectionPosNode.HasValue("localRot")) {
        int index = int.Parse(sectionPosNode.GetValue("index"));
        Vector3 localPos = KSPUtil.ParseVector3(sectionPosNode.GetValue("localPos"));
        savedSectionsLocalPos.Add(index, localPos);
      }
    }
  }

  public override void OnStart(StartState state) {
    base.OnStart(state);
    if (state == StartState.Editor || state == StartState.None) {
      return;
    }

    Events["ContextMenuRetract"].guiName =
        "Retract (" + KASAddonControlKey.telescopicRetractKey + ")";
    Events["ContextMenuExtend"].guiName =
        "Extend (" + KASAddonControlKey.telescopicExtendKey + ")";

    KAS_Shared.createFXSound(this.part, fxSndMotorStart, motorStartSndPath, false);
    KAS_Shared.createFXSound(this.part, fxSndMotor, motorSndPath, true);
    KAS_Shared.createFXSound(this.part, fxSndMotorStop, motorStopSndPath, false);
    KAS_Shared.createFXSound(this.part, fxSndSection, sectionSndPath, false);

    LoadSections();

    if (savedSectionsLocalPos.Count > 0) {
      KAS_Shared.DebugLog("OnStart(TelescopicArm) - Re-set section position from save");
      foreach (KeyValuePair<int, SectionInfo> section in sections) {
        KAS_Shared.DebugLog("OnStart(TelescopicArm) - Move section " + section.Key
                            + " to local position : " + section.Value.savedLocalPos);
        section.Value.transform.position =
            part.transform.TransformPoint(section.Value.savedLocalPos);
      }
    }
  }

  private void LoadSections() {
    sections.Clear();
    sectionTotalLenght = 0;
    ConfigNode node = KAS_Shared.GetBaseConfigNode(this);
    int i = 0;
    foreach (ConfigNode sectionNode in node.GetNodes("SECTION")) {
      if (sectionNode.HasValue("transformName") && sectionNode.HasValue("lenght")) {
        SectionInfo section = new SectionInfo();
        string sectionTransformName = sectionNode.GetValue("transformName");
        section.transform = this.part.FindModelTransform(sectionTransformName);
        if (!section.transform) {
          KAS_Shared.DebugError("LoadSections(TelescopicArm) Section transform "
                                + sectionTransformName + " not found in the model !");
          continue;
        }
        if (!float.TryParse(sectionNode.GetValue("lenght"), out section.lenght)) {
          KAS_Shared.DebugError(
              "LoadSections(TelescopicArm) Unable to parse lenght of the Section : "
              + sectionTransformName);
          continue;
        }
        section.orgLocalPos = KAS_Shared.GetLocalPosFrom(section.transform, this.part.transform);
        section.orgLocalRot = KAS_Shared.GetLocalRotFrom(section.transform, this.part.transform);
        if (savedSectionsLocalPos.Count > 0) {
          section.savedLocalPos = savedSectionsLocalPos[i];
        }
        sections.Add(i, section);
        sectionTotalLenght += section.lenght;
        i++;
      }
    }
  }

  private void disableSectionCollision() {
    KAS_Shared.DisableVesselCollision(this.vessel, this.part.collider);
    foreach (KeyValuePair<int, SectionInfo> section in sections) {
      var transformCollider = section.Value.transform.GetComponent<Collider>();
      if (transformCollider) {
        KAS_Shared.DisableVesselCollision(this.vessel, transformCollider);
      } else {
        KAS_Shared.DebugWarning("LoadBoomHead(TelescopicArm) - Section : "
                                + section.Value.transform.name + " do not have any collider !");
      }
    }
  }

  public void OnPartUnpack() {
    if (!boomHeadLoaded) {
      LoadBoomHead();
    }
  }

  public void LoadBoomHead() {
    if (savedSectionsLocalPos.Count > 0) {
      //Reset section(s) to org pos
      foreach (KeyValuePair<int, SectionInfo> section in sections) {
        section.Value.transform.position = part.transform.TransformPoint(section.Value.orgLocalPos);
      }
    }

    KAS_Shared.DebugLog("LoadBoomHead(TelescopicArm) Create physical object...");
    boomHeadPhysicModule = this.part.gameObject.GetComponent<KASModulePhysicChild>();
    if (!boomHeadPhysicModule) {
      KAS_Shared.DebugLog(
          "LoadBoomHead(TelescopicArm) - KASModulePhysicChild do not exist, adding it...");
      boomHeadPhysicModule = this.part.gameObject.AddComponent<KASModulePhysicChild>();
    }
    boomHeadPhysicModule.mass = boomHeadMass;
    boomHeadPhysicModule.physicObj = sections[0].transform.gameObject;
    boomHeadPhysicModule.StartPhysics();

    orgBoomHeadMass = this.part.mass;
    float newMass = this.part.mass - boomHeadMass;
    if (newMass > 0) {
      this.part.mass = newMass;
    } else {
      KAS_Shared.DebugWarning(
          "LoadBoomHead(TelescopicArm) - Mass of the boom head is greater than the part !");
    }

    KAS_Shared.DebugLog("LoadRotor - Disable collision...");
    disableSectionCollision();

    KAS_Shared.DebugLog("LoadBoomHead(TelescopicArm) - Create configurable joint...");
    slideJoint = this.part.gameObject.AddComponent<ConfigurableJoint>();
    slideJoint.connectedBody = sections[0].transform.GetComponent<Rigidbody>();
    slideJoint.axis = Vector3.zero;
    slideJoint.secondaryAxis = Vector3.zero;
    slideJoint.breakForce = breakForce;
    slideJoint.breakTorque = breakForce;
    slideJoint.angularXMotion = ConfigurableJointMotion.Locked;
    slideJoint.angularYMotion = ConfigurableJointMotion.Locked;
    slideJoint.angularZMotion = ConfigurableJointMotion.Locked;
    slideJoint.xMotion = ConfigurableJointMotion.Locked;
    slideJoint.yMotion = ConfigurableJointMotion.Locked;
    slideJoint.zMotion = ConfigurableJointMotion.Locked;

    if (direction.x != 0) {
      slideJoint.xMotion = ConfigurableJointMotion.Limited;
      driveWay = direction.x;
      KAS_Shared.DebugLog(
          "LoadBoomHead(TelescopicArm) - Direction set to x axis with driveWay set to : "
          + driveWay);
    } else if (direction.y != 0) {
      slideJoint.yMotion = ConfigurableJointMotion.Limited;
      driveWay = direction.y;
      KAS_Shared.DebugLog(
          "LoadBoomHead(TelescopicArm) - Direction set to y axis with driveWay set to : "
          + driveWay);
    } else if (direction.z != 0) {
      slideJoint.zMotion = ConfigurableJointMotion.Limited;
      driveWay = direction.z;
      KAS_Shared.DebugLog(
          "LoadBoomHead(TelescopicArm) - Direction set to z axis with driveWay set to : "
          + driveWay);
    }

    JointDrive drv = new JointDrive();
    drv.mode = JointDriveMode.PositionAndVelocity;
    drv.positionSpring = driveSpring;
    drv.positionDamper = driveDamper;
    drv.maximumForce = driveForce;
    slideJoint.xDrive = slideJoint.yDrive = slideJoint.zDrive = drv;

    var jointLimit = new SoftJointLimit();
    jointLimit.limit = sectionTotalLenght;
    jointLimit.bounciness = 1;
    slideJoint.linearLimit = jointLimit;
    slideJoint.linearLimitSpring = new SoftJointLimitSpring() {
      damper = 200,
      spring = 1000
    };

    if (savedSectionsLocalPos.Count > 0) {
      KAS_Shared.DebugLog("LoadBoomHead(TelescopicArm) - Re-set section position from save");
      float sumLenght = 0;
      foreach (KeyValuePair<int, SectionInfo> section in sections) {
        KAS_Shared.DebugLog("LoadBoomHead(TelescopicArm) - Move section " + section.Key
                            + " to local position : " + section.Value.savedLocalPos);
        section.Value.transform.position =
            part.transform.TransformPoint(section.Value.savedLocalPos);
        sumLenght += section.Value.lenght;
        if (headPos > sumLenght) {
          KAS_Shared.DebugLog("LoadBoomHead(TelescopicArm) - Parent section "
                              + sections[section.Key + 1].transform.name + " to : "
                              + section.Value.transform.name);
          sections[section.Key + 1].transform.parent = section.Value.transform;
        }
      }
    }

    // Get boom head attach nodes
    attachNodes.Clear();
    ConfigNode node = KAS_Shared.GetBaseConfigNode(this);
    List<string> attachNodesSt = new List<string>(node.GetValues("attachNode"));
    foreach (String anString in attachNodesSt) {
      AttachNode an = this.part.findAttachNode(anString);
      if (an != null) {
        KAS_Shared.DebugLog("LoadBoomHead(TelescopicArm) - Boom head attach node added : " + an.id);
        attachNodes.Add(an);
        if (an.attachedPart) {
          KAS_Shared.DebugLog("LoadBoomHead(TelescopicArm) - Setting boom head joint on : "
                              + an.attachedPart.partInfo.title);
          KAS_Shared.RemoveFixedJointBetween(this.part, an.attachedPart);
          KAS_Shared.RemoveHingeJointBetween(this.part, an.attachedPart);
          FixedJoint fjnt = an.attachedPart.gameObject.AddComponent<FixedJoint>();
          fjnt.connectedBody = sections[0].transform.GetComponent<Rigidbody>();
          fixedJnts.Add(fjnt);
        }
      }
    }
    SetTargetPos(targetPos);
    boomHeadLoaded = true;
  }

  void Update() {
    base.OnUpdate();
    if (!HighLogic.LoadedSceneIsFlight) {
      return;
    }
    UpdateHeadPos();
    UpdateExtend();
    UpdateRetract();
    UpdateSections();
    UpdateOrgPos();
  }

  private void UpdateHeadPos() {
    float dist = Vector3.Distance(
        part.transform.TransformPoint(sections[0].orgLocalPos), sections[0].transform.position);
    headPos = Mathf.Round(dist * headPosOffset) / headPosOffset;
    if (slideJoint) {
      prjDist = slideJoint.projectionDistance;
      prjAngle = slideJoint.projectionAngle;
    }
  }

  private void UpdateOrgPos() {
    if (boomHeadLoaded) {
      if (this.part.hasIndirectParent(this.part.localRoot)) {
        KAS_Shared.UpdateChildsOrgPos(this.part, false);
      }
      if (this.part.hasIndirectChild(this.part.localRoot)) {
        KAS_Shared.UpdateChildsOrgPos(this.part, true);
      }
    }
  }

  private void UpdateExtend() {
    if (extend.active && !extend.full) {
      if (KAS_Shared.RequestPower(this.part, powerDrain)) {
        extend.isrunning = true;
        if (!extend.starting) {
          retract.full = false;
          retract.active = false;
          extend.starting = true;
          stateField = "Extending...";
          fxSndMotorStart.audio.loop = false;
          fxSndMotorStart.audio.Play();
        }

        if (headPos < sectionTotalLenght) {
          if (!fxSndMotor.audio.isPlaying) {
            fxSndMotor.audio.Play();
          }
          if (targetPos < sectionTotalLenght) {
            SetTargetPos(targetPos + speed * TimeWarp.deltaTime);
          } else {
            SetTargetPos(sectionTotalLenght);
          }
        } else {
          extend.full = true;
          extend.active = false;
          SetTargetPos(sectionTotalLenght);
        }
      } else {
        if (this.part.vessel == FlightGlobals.ActiveVessel) {
          ScreenMessages.PostScreenMessage(
              string.Format("{0} stopped ! Insufficient Power", part.partInfo.title),
              5, ScreenMessageStyle.UPPER_CENTER);
        }
        stateField = "Insufficient Power";
        StopExtend();
      }
    } else {
      StopExtend();
    }
  }

  private void UpdateRetract() {
    if (retract.active && !retract.full) {
      if (KAS_Shared.RequestPower(this.part, powerDrain)) {
        retract.isrunning = true;
        if (!retract.starting) {
          extend.full = false;
          extend.active = false;
          retract.starting = true;
          stateField = "Retracting...";
          fxSndMotorStart.audio.Play();
        }

        if (headPos > 0) {
          if (!fxSndMotor.audio.isPlaying) {
            fxSndMotor.audio.Play();
          }
          if (targetPos > 0) {
            SetTargetPos(targetPos - speed * TimeWarp.deltaTime);
          } else {
            SetTargetPos(0);
          }
        } else {
          retract.full = true;
          retract.active = false;
          SetTargetPos(0);
        }
      } else {
        if (this.part.vessel == FlightGlobals.ActiveVessel) {
          ScreenMessages.PostScreenMessage(
              string.Format("{0} stopped ! Insufficient Power", part.partInfo.title),
              5, ScreenMessageStyle.UPPER_CENTER);
        }
        stateField = "Insufficient Power";
        StopRetract();
      }
    } else {
      StopRetract();
    }
  }

  private void UpdateSections() {
    if (headPos == Round(targetPos) || sections.Count <= 1) {
      return;
    }

    float sumLenght = 0;
    int activeSection = 0;
    foreach (KeyValuePair<int, SectionInfo> section in sections) {
      if (section.Key + 1 >= sections.Count) {
        return;
      }

      sumLenght += section.Value.lenght;
      if (headPos > sumLenght && sectionIndex == activeSection) {
        KAS_Shared.DebugLog("UpdateSections(TelescopicArm) - Extend | Change section to : "
                            + sectionIndex);
        sections[section.Key + 1].transform.parent = section.Value.transform;
        sectionIndex += 1;
        disableSectionCollision();
        fxSndSection.audio.Play();
      }
      if (headPos <= sumLenght && sectionIndex == activeSection + 1) {
        KAS_Shared.DebugLog("UpdateSections(SlideMotor) - Retract | Change section to : "
                            + sectionIndex);
        sections[section.Key + 1].transform.parent = this.part.transform;
        sections[section.Key + 1].transform.localPosition = section.Value.orgLocalPos;
        sections[section.Key + 1].transform.localRotation = section.Value.orgLocalRot;
        sectionIndex -= 1;
        disableSectionCollision();
        fxSndSection.audio.Play();
      }
      activeSection += 1;
    }
  }

  private float Round(float f) {
    return Mathf.Round((f * headPosOffset) / headPosOffset);
  }

  private void SetTargetPos(float pos) {
    slideJoint.targetPosition = GetOrientedLocalPos(pos);
    targetPos = pos;
  }

  private Vector3 GetOrientedLocalPos(float pos) {
    Vector3 orientedLocalPos = new Vector3();
    if (direction.x != 0) {
      if (driveWay > 0) {
        orientedLocalPos = new Vector3(pos, 0, 0);
      }
      if (driveWay < 0) {
        orientedLocalPos = new Vector3(-pos, 0, 0);
      }
    } else if (direction.y != 0) {
      if (driveWay > 0) {
        orientedLocalPos = new Vector3(0, pos, 0);
      }
      if (driveWay < 0) {
        orientedLocalPos = new Vector3(0, -pos, 0);
      }
    } else if (direction.z != 0) {
      if (driveWay > 0) {
        orientedLocalPos = new Vector3(0, 0, pos);
      }
      if (driveWay < 0) {
        orientedLocalPos = new Vector3(0, 0, -pos);
      }
    }
    return orientedLocalPos;
  }

  private void StopExtend() {
    if (extend.isrunning) {
      extend.isrunning = false;
      extend.starting = false;
      stateField = "Idle";
      fxSndMotor.audio.Stop();
      fxSndMotorStop.audio.Play();
      extend.active = false;
    }
  }

  private void StopRetract() {
    if (retract.isrunning) {
      retract.isrunning = false;
      retract.starting = false;
      stateField = "Idle";
      fxSndMotor.audio.Stop();
      fxSndMotorStop.audio.Play();
      retract.active = false;
    }
  }

  [KSPEvent(name = "ContextMenuToggleControl", active = true, guiActive = true,
            guiName = "Toggle Control")]
  public void ContextMenuToggleControl() {
    controlActivated = !controlActivated;
    controlField = controlActivated.ToString();
  }

  [KSPEvent(name = "ContextMenuRetract", active = true, guiActive = true,
            guiName = "Retract")]
  public void ContextMenuRetract() {
    retract.active = !retract.active;
  }

  [KSPEvent(name = "ContextMenuExtend", active = true, guiActive = true,
            guiName = "Extend")]
  public void ContextMenuExtend() {
    extend.active = !extend.active;
  }

  [KSPAction("Retract", actionGroup = KSPActionGroup.None)]
  public void ActionGroupRetract(KSPActionParam param) {
    if (boomHeadLoaded) {
      ContextMenuRetract();
    }
  }

  [KSPAction("Extend", actionGroup = KSPActionGroup.None)]
  public void ActionGroupExtend(KSPActionParam param) {
    if (boomHeadLoaded) {
      ContextMenuExtend();
    }
  }

  public void EventTelescopicExtend(bool activated) {
    if (!controlActivated) {
      return;
    }
    if (controlInverted) {
      retract.active = activated;
    } else {
      extend.active = activated;
    }
  }

  public void EventTelescopicRetract(bool activated) {
    if (!controlActivated) {
      return;
    }
    if (controlInverted) {
      extend.active = activated;
    } else {
      retract.active = activated;
    }
  }
}

}  // namespace
