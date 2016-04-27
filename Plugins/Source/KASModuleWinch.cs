using System;
using System.Collections;
using System.Text;
using UnityEngine;

namespace KAS {

public class KASModuleWinch : KASModuleAttachCore {
  //Part.cfg file
  [KSPField]
  public float maxLenght = 50.0f;
  [KSPField]
  public float cableSpring = 1000.0f;
  [KSPField]
  public float cableDamper = 0.1f;
  [KSPField]
  public float cableWidth = 0.04f;
  [KSPField]
  public float motorMaxSpeed = 2f;
  [KSPField]
  public float motorMinSpeed = 0.01f;
  [KSPField]
  public float motorAcceleration = 0.05f;
  [KSPField]
  public float powerDrain = 0.5f;
  [KSPField]
  public float releaseOffset = 1f;
  [KSPField]
  public string headTransformName = "head";
  [KSPField]
  public float headMass = 0.01f;
  [KSPField]
  public string headPortNodeName = "portNode";
  [KSPField]
  public string connectedPortNodeName = "bottom";
  [KSPField]
  public string anchorNodeName = "anchorNode";
  [KSPField]
  public Vector3 evaGrabHeadPos = new Vector3(0.05f, 0.01f, -0.11f);
  [KSPField]
  public Vector3 evaGrabHeadDir = new Vector3(0f, 0f, -1f);
  [KSPField]
  public Vector3 evaDropHeadPos = new Vector3(0.05f, 0.01f, -0.16f);
  [KSPField]
  public Vector3 evaDropHeadRot = new Vector3(180f, 0f, 0f);
  [KSPField]
  public bool ejectEnabled = true;
  [KSPField]
  public float ejectForce = 20f;
  [KSPField]
  public float lockMinDist = 0.08f;
  [KSPField]
  public float lockMinFwdDot = 0.90f;

  //Sounds & texture
  [KSPField]
  private string cableTexPath = "KAS/Textures/cable";
  [KSPField]
  private string motorSndPath = "KAS/Sounds/winchSmallMotor";
  [KSPField]
  private string motorStartSndPath = "KAS/Sounds/winchSmallMotorStart";
  [KSPField]
  private string motorStopSndPath = "KAS/Sounds/winchSmallMotorStop";
  [KSPField]
  private string headLockSndPath = "KAS/Sounds/winchSmallLock";
  [KSPField]
  private string ejectSndPath = "KAS/Sounds/winchSmallEject";
  [KSPField]
  public string headGrabSndPath = "KAS/Sounds/grab";

  [KSPField(guiActive = true, guiName = "Key control", guiFormat = "S")]
  public string controlField = "";
  [KSPField(guiActive = true, guiName = "Head State", guiFormat = "S")]
  public string headStateField = "Locked";
  [KSPField(guiActive = true, guiName = "Cable State", guiFormat = "S")]
  public string winchStateField = "Idle";
  [KSPField(guiActive = true, guiName = "Lenght", guiFormat = "F2", guiUnits = "m")]
  public float lengthField = 0.0f;

  // FX
  public FXGroup fxSndMotorStart;
  public FXGroup fxSndMotor;
  public FXGroup fxSndMotorStop;
  public FXGroup fxSndHeadLock;
  public FXGroup fxSndEject;
  public FXGroup fxSndHeadGrab;
  private Texture2D texCable;
  public KAS_Tube tubeRenderer;

  // Winch GUI
  [KSPField(isPersistant = true)]
  public string winchName = "";
  public bool isActive = true;
  private bool isBlocked = false;
  public bool guiRepeatRetract = false;
  public bool guiRepeatExtend = false;
  public bool guiRepeatTurnLeft = false;
  public bool guiRepeatTurnRight = false;
  public bool highLightStarted = false;

  // Transforms
  public Transform headTransform;
  public Transform headPortNode;
  private Transform winchAnchorNode;
  private Transform headAnchorNode;
  private KASModulePhysicChild headPhysicModule;

  // Cable control
  [KSPField(isPersistant = true)]
  private bool controlActivated = true;
  [KSPField(isPersistant = true)]
  private bool controlInverted = false;
  public KAS_Shared.cableControl release;
  public KAS_Shared.cableControl retract;
  public KAS_Shared.cableControl extend;
  public float motorSpeed = 0f;
  public float motorSpeedSetting;

  public Part evaHolderPart = null;
  private Transform evaHeadNodeTransform;
  private Collider evaCollider;

  // Plug
  public KASModulePort grabbedPortModule = null;
  private PlugState headStateVar = PlugState.Locked;

  public PlugState headState {
    get { return headStateVar; }
    set {
      headStateVar = value;
      if (headStateVar == PlugState.Locked) {
        headStateField = "Locked";
      }
      if (headStateVar == PlugState.Deployed) {
        headStateField = "Deployed";
      }
      if (headStateVar == PlugState.PlugUndocked) {
        headStateField = "Plugged(Undocked)";
      }
      if (headStateVar == PlugState.PlugDocked) {
        headStateField = "Plugged(Docked)";
      }
    }
  }

  public PortInfo connectedPortInfo;
  public struct PortInfo {
    public KASModulePort module;
    public string savedVesselID;
    public string savedPartID;
  }
  private bool fromSave = false;

  // Cable & Head
  public SpringJoint cableJoint;
  private Vector3 headOrgLocalPos;
  private Quaternion headOrgLocalRot;
  private Vector3 headCurrentLocalPos;
  private Quaternion headCurrentLocalRot;
  private float orgWinchMass;

  public enum PlugState {
    Locked = 0,
    Deployed = 1,
    PlugDocked = 2,
    PlugUndocked = 3,
  }

  public float cableJointLength {
    get {
      if (cableJoint) {
        return cableJoint.maxDistance;
      } else {
        return 0;
      }
    }
    set {
      if (cableJoint) {
        cableJoint.maxDistance = lengthField = value;
      }
    }
  }

  public float cableRealLenght {
    get {
      if (cableJoint) {
        return Vector3.Distance(winchAnchorNode.position, headAnchorNode.position);
      } else
        return 0;
    }
  }

  public Part nodeConnectedPart {
    get {
      AttachNode an = this.part.findAttachNode(connectedPortNodeName);
      if (an != null && an.attachedPart) {
        return an.attachedPart;
      }
      return null;
    }
    set {
      AttachNode an = this.part.findAttachNode(connectedPortNodeName);
      if (an != null) {
        an.attachedPart = value;
      } else {
        KAS_Shared.DebugError("connectedPort(Winch) Cannot set connectedPart !");
      }
    }
  }

  public KASModulePort nodeConnectedPort {
    get {
      AttachNode an = this.part.findAttachNode(connectedPortNodeName);
      if (an != null) {
        if (an.attachedPart) {
          KASModulePort portModule = an.attachedPart.GetComponent<KASModulePort>();
          if (portModule) {
            return portModule;
          }
        }
      }
      return null;
    }
    set {
      AttachNode an = this.part.findAttachNode(connectedPortNodeName);
      if (an != null) {
        if (value) {
          an.attachedPart = value.part;
        } else {
          an.attachedPart = null;
        }
      } else {
        KAS_Shared.DebugError("connectedPort(Winch) Cannot set connectedPort !");
      }
    }
  }

  public bool PlugDocked {
    get {
      if (headState == PlugState.PlugDocked) {
        return true;
      } else {
        return false;
      }
    }
    set {
      if ((value == true)) {
        if (headState == PlugState.PlugUndocked) {
          ChangePlugMode(PlugState.PlugDocked);
        }
      } else {
        if (headState == PlugState.PlugDocked) {
          ChangePlugMode(PlugState.PlugUndocked);
        }
      }
    }
  }

  public override string GetInfo() {
    var sb = new StringBuilder();
    sb.AppendFormat("<b>Maximum length</b>: {0:F0}m", maxLenght);
    sb.AppendLine();
    sb.AppendFormat("<b>Power consumption</b>: {0:F1}", powerDrain);
    sb.AppendLine();
    return sb.ToString();
  }

  public override void OnSave(ConfigNode node) {
    base.OnSave(node);

    if (headState != PlugState.Locked) {
      KAS_Shared.DebugLog("OnSave(Winch) Winch head deployed, saving info...");
      ConfigNode cableNode = node.AddNode("Head");
      cableNode.AddValue(
          "headLocalPos",
          KSPUtil.WriteVector(KAS_Shared.GetLocalPosFrom(headTransform, this.part.transform)));
      cableNode.AddValue(
          "headLocalRot",
          KSPUtil.WriteQuaternion(KAS_Shared.GetLocalRotFrom(headTransform, this.part.transform)));
    }

    if (headState == PlugState.PlugDocked || headState == PlugState.PlugUndocked) {
      ConfigNode plugNode = node.AddNode("PLUG");
      if (headState == PlugState.PlugDocked)
        plugNode.AddValue("type", "docked");
      if (headState == PlugState.PlugUndocked)
        plugNode.AddValue("type", "undocked");
      plugNode.AddValue("vesselId", connectedPortInfo.module.vessel.id.ToString());
      plugNode.AddValue("partId", connectedPortInfo.module.part.flightID.ToString());
    }
  }

  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    if (node.HasNode("Head")) {
      KAS_Shared.DebugLog("OnLoad(Winch) Loading winch head info from save...");
      ConfigNode cableNode = node.GetNode("Head");
      headCurrentLocalPos = KSPUtil.ParseVector3(cableNode.GetValue("headLocalPos"));
      headCurrentLocalRot = KSPUtil.ParseQuaternion(cableNode.GetValue("headLocalRot"));
      headState = PlugState.Deployed;
      fromSave = true;
    }

    if (node.HasNode("PLUG")) {
      KAS_Shared.DebugLog("OnLoad(Winch) Loading plug info from save...");
      ConfigNode plugNode = node.GetNode("PLUG");
      connectedPortInfo.savedVesselID = plugNode.GetValue("vesselId").ToString();
      connectedPortInfo.savedPartID = plugNode.GetValue("partId").ToString();
      if (plugNode.GetValue("type").ToString() == "docked") {
        headState = PlugState.PlugDocked;
      }
      if (plugNode.GetValue("type").ToString() == "undocked") {
        headState = PlugState.PlugUndocked;
      }
      fromSave = true;
    }
  }

  public override void OnStart(StartState state) {
    base.OnStart(state);
    if (state == StartState.Editor || state == StartState.None) {
      return;
    }

    texCable = GameDatabase.Instance.GetTexture(cableTexPath, false);
    if (!texCable) {
      KAS_Shared.DebugError("cable texture loading error !");
      ScreenMessages.PostScreenMessage(
          "Texture file : " + cableTexPath
          + " as not been found, please check your KAS installation !",
          10, ScreenMessageStyle.UPPER_CENTER);
    }
    KAS_Shared.createFXSound(this.part, fxSndMotorStart, motorStartSndPath, false);
    KAS_Shared.createFXSound(this.part, fxSndMotor, motorSndPath, true);
    KAS_Shared.createFXSound(this.part, fxSndMotorStop, motorStopSndPath, false);
    KAS_Shared.createFXSound(this.part, fxSndHeadLock, headLockSndPath, false);
    KAS_Shared.createFXSound(this.part, fxSndEject, ejectSndPath, false);
    KAS_Shared.createFXSound(this.part, fxSndHeadGrab, headGrabSndPath, false);

    // Get head transform
    headTransform = this.part.FindModelTransform(headTransformName);
    if (!headTransform) {
      KAS_Shared.DebugError("OnStart(Winch) Head transform " + headTransformName
                            + " not found in the model !");
      DisableWinch();
      return;
    }

    // get winch anchor node
    winchAnchorNode = this.part.FindModelTransform(anchorNodeName);
    if (!winchAnchorNode) {
      KAS_Shared.DebugError("OnStart(Winch) Winch anchor tranform node " + anchorNodeName
                            + " not found in the model !");
      DisableWinch();
      return;
    }

    // Get head port node
    headPortNode = this.part.FindModelTransform(headPortNodeName);
    if (!headPortNode) {
      KAS_Shared.DebugError("OnStart(Winch) Head transform port node " + headPortNodeName
                            + " not found in the model !");
      DisableWinch();
      return;
    }

    //Set connector node transform
    AttachNode an = this.part.findAttachNode(connectedPortNodeName);
    an.nodeTransform = new GameObject("KASWinchConnectorAn").transform;
    an.nodeTransform.parent = this.part.transform;
    an.nodeTransform.localPosition = an.position;
    an.nodeTransform.localRotation = Quaternion.LookRotation(an.orientation);
    an.nodeTransform.parent = headTransform;

    // Set linked object module 
    KAS_LinkedPart linkedPart = headTransform.gameObject.AddComponent<KAS_LinkedPart>();
    linkedPart.part = this.part;

    // Create head anchor node
    headAnchorNode = new GameObject("KASHeadAnchor").transform;
    headAnchorNode.position = winchAnchorNode.position;
    headAnchorNode.rotation = winchAnchorNode.rotation;
    headAnchorNode.parent = headTransform;
    headAnchorNode.rotation *= Quaternion.Euler(new Vector3(180f, 0f, 0f));

    // Get original head position and rotation
    headOrgLocalPos = KAS_Shared.GetLocalPosFrom(headTransform, this.part.transform);
    headOrgLocalRot = KAS_Shared.GetLocalRotFrom(headTransform, this.part.transform);

    if (nodeConnectedPort) {
      KAS_Shared.DebugWarning("OnStart(Winch) NodeConnectedPort is : "
                              + nodeConnectedPort.part.partInfo.title);
    } else {
      if (nodeConnectedPart) {
        KAS_Shared.DebugWarning(
            "OnStart(Winch) Connected part is not a port, configuration not supported !");
        isBlocked = true;
        headState = PlugState.Locked;
      } else {
        KAS_Shared.DebugWarning("OnStart(Winch) No connected part found !");
      }
    }

    // Get saved port module if any
    if (headState == PlugState.PlugDocked || headState == PlugState.PlugUndocked) {
      StartCoroutine(WaitAndLoadConnection());
    }

    if (headState != PlugState.Locked) {
      KAS_Shared.SetPartLocalPosRotFrom(headTransform, this.part.transform,
                                        headCurrentLocalPos, headCurrentLocalRot);
      SetTubeRenderer(true);
    }

    motorSpeedSetting = motorMaxSpeed / 2;

    KAS_Shared.DebugWarning("OnStart(Winch) HeadState : " + headState);
    GameEvents.onVesselGoOnRails.Add(new EventData<Vessel>.OnEvent(this.OnVesselGoOnRails));
    GameEvents.onVesselGoOffRails.Add(new EventData<Vessel>.OnEvent(this.OnVesselGoOffRails));
    GameEvents.onCrewBoardVessel.Add(
        new EventData<GameEvents.FromToAction<Part, Part>>.OnEvent(this.OnCrewBoardVessel));
  }

  IEnumerator WaitAndLoadConnection() {
    yield return new WaitForEndOfFrame();

    // Get saved port module if any
    if (headState == PlugState.PlugDocked || headState == PlugState.PlugUndocked) {
      KAS_Shared.DebugLog("OnStart(Winch) Retrieve part with ID : " + connectedPortInfo.savedPartID
                          + " | From vessel ID : " + connectedPortInfo.savedVesselID);
      Part connectedPartSaved =
          KAS_Shared.GetPartByID(connectedPortInfo.savedVesselID, connectedPortInfo.savedPartID);
      if (connectedPartSaved) {
        KASModulePort connectedPortSaved = connectedPartSaved.GetComponent<KASModulePort>();
        if (connectedPortSaved) {
          connectedPortInfo.module = connectedPortSaved;
        } else {
          KAS_Shared.DebugError("OnStart(Winch) Unable to get saved plugged port module !");
          headState = PlugState.Locked;
        }
      } else {
        KAS_Shared.DebugError("OnStart(Winch) Unable to get saved plugged part !");
        headState = PlugState.Locked;
      }
    }
  }

  void OnVesselGoOnRails(Vessel vess) {
    if (vess != this.vessel) {
      return;
    }
  }

  void OnVesselGoOffRails(Vessel vess) {
    if (!fromSave || vessel.packed || (connectedPortInfo.module
                                       && connectedPortInfo.module.vessel.packed)) {
      return;
    }

    // From save
    if (headState == PlugState.Deployed && fromSave) {
      KAS_Shared.DebugLog("OnVesselGoOffRails(Winch) Head deployed or docked and no cable joint"
                          + " exist, re-deploy and set head position");
      Deploy();
      KAS_Shared.SetPartLocalPosRotFrom(headTransform, this.part.transform,
                                        headCurrentLocalPos, headCurrentLocalRot);
      cableJointLength = cableRealLenght;
      fromSave = false;
    }

    if (headState == PlugState.PlugUndocked && fromSave) {
      KAS_Shared.DebugLog("OnVesselGoOffRails(Winch) From save, Plug (undocked) to : "
                          + connectedPortInfo.module.part.partInfo.title);
      PlugHead(connectedPortInfo.module, PlugState.PlugUndocked, true, false);
      fromSave = false;
    }

    if (headState == PlugState.PlugDocked && fromSave) {
      KAS_Shared.DebugLog("OnVesselGoOffRails(Winch) From save, Plug (docked) to : "
                          + connectedPortInfo.module.part.partInfo.title);
      PlugHead(connectedPortInfo.module, PlugState.PlugDocked, true, false, true);
      fromSave = false;
    }

    // Just in case
    fromSave = false;
  }

  void OnCrewBoardVessel(GameEvents.FromToAction<Part, Part> fromToAction) {
    if (evaHolderPart && !grabbedPortModule && fromToAction.from.vessel == evaHolderPart.vessel) {
      KAS_Shared.DebugLog(fromToAction.from.vessel.vesselName + " boarding "
                          + fromToAction.to.vessel.vesselName
                          + " with a winch head grabbed, dropping it to avoid destruction");
      DropHead();
    }
  }
      
  public override void OnPartUnpack() {
    base.OnPartUnpack();

    KAS_Shared.DebugLog("OnPartUnpack(Winch)");
    if (headState != PlugState.Locked && headTransform.GetComponent<Rigidbody>()) {
      cableJointLength = cableRealLenght;
    }
  }

  protected override void OnDestroy() {
    base.OnDestroy();

    GameEvents.onVesselGoOnRails.Remove(new EventData<Vessel>.OnEvent(this.OnVesselGoOnRails));
    GameEvents.onVesselGoOffRails.Remove(new EventData<Vessel>.OnEvent(this.OnVesselGoOffRails));
    GameEvents.onCrewBoardVessel.Remove(
        new EventData<GameEvents.FromToAction<Part, Part>>.OnEvent(this.OnCrewBoardVessel));
  }

  protected override void OnPartDie() {
    base.OnPartDie();

    if (evaHolderPart) {
      DropHead();
    } else {
      UnplugHead(false);
    }

    SetHeadToPhysic(false);
  }

  public override void OnUpdate() {
    base.OnUpdate();
    if (!HighLogic.LoadedSceneIsFlight) {
      return;
    }
    if (isActive && !isBlocked) {
      UpdateMotor();
    }
    UpdateOrgPos();
  }

  private void DisableWinch() {
    Events["ContextMenuRelease"].guiActive = false;
    Events["ContextMenuRetract"].guiActive = false;
    Events["ContextMenuExtend"].guiActive = false;
    Events["ContextMenuGrabHead"].guiActiveUnfocused = false;
    Events["ContextMenuLockHead"].guiActiveUnfocused = false;
    isActive = false;
  }

  private void UpdateMotor() {
    #region release
    if (release.active) {
      if (headState == PlugState.Locked) {
        cableJointLength = 0f;
        Deploy();
        if (headState == PlugState.Locked) {
          extend.active = false;
          KAS_Shared.DebugError(
              "Deploy(Winch) - Something go wrong, cannot deploy the winch head !!");
        }
      }
      release.isrunning = true;
      if (!release.starting) {
        retract.active = false;
        extend.active = false;
        retract.full = false;
        release.starting = true;
        motorSpeed = 0;
        winchStateField = "Released";
      }
      float tempCablelenghtF = cableRealLenght + releaseOffset;
      if (tempCablelenghtF > maxLenght) {
        release.active = false;
        cableJointLength = maxLenght;
      } else {
        cableJointLength = tempCablelenghtF;
      }
    } else {
      if (release.isrunning) {
        release.isrunning = false;
        release.starting = false;
        winchStateField = "Idle";
        release.active = false;
      }
    }
    #endregion

    #region Extend
    if (extend.active && !extend.full) {
      if (headState == PlugState.Locked) {
        cableJointLength = 0f;
        Deploy();
        if (headState == PlugState.Locked) {
          extend.active = false;
          KAS_Shared.DebugError(
              "Deploy(Winch) - Something go wrong, cannot deploy the winch head !!");
        }
      }
      if (KAS_Shared.RequestPower(this.part, powerDrain)) {
        extend.isrunning = true;
        if (!extend.starting) {
          retract.full = false;
          retract.active = false;
          release.active = false;
          extend.starting = true;
          winchStateField = "Extending cable...";
          motorSpeed = 0;
          fxSndMotorStart.audio.loop = false;
          fxSndMotorStart.audio.Play();
        }

        if (motorSpeedSetting <= 0) {
          motorSpeedSetting = motorMinSpeed;
        }
        if (motorSpeed < motorSpeedSetting) {
          motorSpeed += motorAcceleration;
        }
        if (motorSpeed > motorSpeedSetting + motorAcceleration) {
          motorSpeed -= motorAcceleration;
        }
        float tempCablelenghtE = cableJointLength + motorSpeed * TimeWarp.deltaTime;
        if (tempCablelenghtE > maxLenght) {
          extend.full = true;
          extend.active = false;
          cableJointLength = maxLenght;
        } else {
          if (!fxSndMotor.audio.isPlaying) {
            fxSndMotor.audio.Play();
          }
          cableJointLength = tempCablelenghtE;
        }
      } else {
        if (this.part.vessel == FlightGlobals.ActiveVessel) {
          ScreenMessages.PostScreenMessage("Winch stopped ! Insufficient Power",
                                           5, ScreenMessageStyle.UPPER_CENTER);
        }
        winchStateField = "Insufficient Power";
        StopExtend();
      }
    } else {
      StopExtend();
    }
    #endregion

    #region retract
    if (retract.active && !retract.full) {
      if (headState == PlugState.Locked) {
        StopRetract();
        return;
      }
      if (KAS_Shared.RequestPower(this.part, powerDrain)) {
        retract.isrunning = true;
        if (!retract.starting) {
          extend.full = false;
          extend.active = false;
          release.active = false;
          retract.starting = true;
          winchStateField = "Retracting cable...";
          motorSpeed = 0;
          fxSndMotorStart.audio.loop = false;
          fxSndMotorStart.audio.Play();
        }

        if (motorSpeedSetting <= 0) {
          motorSpeedSetting = motorMinSpeed;
        }
        if (motorSpeed < motorSpeedSetting) {
          motorSpeed += motorAcceleration;
        }
        if (motorSpeed > motorSpeedSetting + motorAcceleration) {
          motorSpeed -= motorAcceleration;
        }
        float tempCableLenghtR = cableJointLength - motorSpeed * TimeWarp.deltaTime;
        if (tempCableLenghtR > 0) {
          if (!fxSndMotor.audio.isPlaying) {
            fxSndMotor.audio.Play();
          }
          cableJointLength = tempCableLenghtR;
        } else {
          OnFullRetract();
        }
      } else {
        if (this.part.vessel == FlightGlobals.ActiveVessel) {
          ScreenMessages.PostScreenMessage("Winch stopped ! Insufficient Power",
                                           5, ScreenMessageStyle.UPPER_CENTER);
        }
        winchStateField = "Insufficient Power";
        StopRetract();
      }
    } else {
      StopRetract();
    }
    #endregion
  }

  private void UpdateOrgPos() {
    if (headState == PlugState.PlugDocked) {
      if (connectedPortInfo.module.part.parent == this.part) {
        KAS_Shared.UpdateChildsOrgPos(connectedPortInfo.module.part, true);
      }
      if (this.part.parent == connectedPortInfo.module.part) {
        KAS_Shared.UpdateChildsOrgPos(this.part, true);
      }
    }
  }

  private void StopExtend() {
    if (extend.isrunning) {
      motorSpeed = 0;
      extend.isrunning = false;
      extend.starting = false;
      winchStateField = "Idle";
      fxSndMotor.audio.Stop();
      fxSndMotorStop.audio.loop = false;
      fxSndMotorStop.audio.Play();
      extend.active = false;
    }
  }

  private void StopRetract() {
    if (retract.isrunning) {
      motorSpeed = 0;
      retract.isrunning = false;
      retract.starting = false;
      winchStateField = "Idle";
      fxSndMotor.audio.Stop();
      fxSndMotorStop.audio.loop = false;
      fxSndMotorStop.audio.Play();
      retract.active = false;
    }
  }

  public void OnFullRetract() {
    if ((IsLockable() || headState == PlugState.Deployed) && !evaHolderPart) {
      retract.full = true;
      retract.active = false;
      cableJointLength = 0;
      Lock();
    } else {
      ScreenMessages.PostScreenMessage("Connected parts not aligned ! Locking impossible.",
                                       5, ScreenMessageStyle.UPPER_CENTER);
      retract.active = false;
    }
  }

  public void Deploy() {
    KAS_Shared.DebugLog("Deploy(Winch) - Return head to original pos");
    KAS_Shared.SetPartLocalPosRotFrom(
        headTransform, this.part.transform, headOrgLocalPos, headOrgLocalRot);

    SetHeadToPhysic(true);
    orgWinchMass = this.part.mass;
    float newMass = this.part.mass - headMass;
    if (newMass > 0) {
      this.part.mass = newMass;
    } else {
      KAS_Shared.DebugWarning("Deploy(Winch) - Mass of the head is greater than the winch !");
    }

    KAS_Shared.DebugLog("Deploy(Winch) - Create spring joint");
    cableJoint = this.part.gameObject.AddComponent<SpringJoint>();
    cableJoint.connectedBody = headTransform.GetComponent<Rigidbody>();
    cableJoint.maxDistance = 0;
    cableJoint.minDistance = 0;
    cableJoint.spring = cableSpring;
    cableJoint.damper = cableDamper;
    cableJoint.breakForce = 999;
    cableJoint.breakTorque = 999;
    cableJoint.anchor = winchAnchorNode.localPosition;

    if (nodeConnectedPort) {
      KAS_Shared.DebugLog("Deploy(Winch) - Connected port detected, plug head in docked mode...");
      nodeConnectedPort.nodeConnectedPart = null;
      PlugHead(nodeConnectedPort, PlugState.PlugDocked, alreadyDocked: true);
    } else {
      KAS_Shared.DebugLog("Deploy(Winch) - Deploy connector only...");
      headState = PlugState.Deployed;
    }

    nodeConnectedPort = null;
    KAS_Shared.DebugLog("Deploy(Winch) - Enable tube renderer");
    SetTubeRenderer(true);
  }

  public void Lock() {
    if (cableJoint) {
      KAS_Shared.DebugLog("Lock(Winch) Removing spring joint");
      Destroy(cableJoint);
    }
    KAS_Shared.SetPartLocalPosRotFrom(
        headTransform, this.part.transform, headOrgLocalPos, headOrgLocalRot);

    if (headState == PlugState.PlugDocked || headState == PlugState.PlugUndocked) {
      KAS_Shared.DebugLog("Lock(Winch) Dock connected port");
      // Save control state
      Vessel originalVessel = this.vessel;
      bool is_active = (FlightGlobals.ActiveVessel == this.vessel);
      // Decouple and re-dock
      KASModulePort tmpPortModule = connectedPortInfo.module;
      UnplugHead(false);
      KAS_Shared.MoveAlignLight(
          tmpPortModule.part.vessel, tmpPortModule.portNode, part.vessel, headPortNode);
      AttachDocked(tmpPortModule, originalVessel);
      nodeConnectedPort = tmpPortModule;
      tmpPortModule.nodeConnectedPart = this.part;
      // Restore controls and focus
      if (is_active) {
        FlightGlobals.ForceSetActiveVessel(this.vessel);
        FlightInputHandler.ResumeVesselCtrlState(this.vessel);
      }
    }
    SetHeadToPhysic(false);
    this.part.mass = orgWinchMass;

    SetTubeRenderer(false);
    motorSpeed = 0;
    cableJoint = null;
    headState = PlugState.Locked;
    fxSndHeadLock.audio.Play();
  }

  public void SetTubeRenderer(bool activated) {
    if (activated) {
      // loading strut renderer
      tubeRenderer = this.part.gameObject.AddComponent<KAS_Tube>();
      tubeRenderer.tubeTexTilingOffset = 4;
      tubeRenderer.tubeScale = cableWidth;
      tubeRenderer.sphereScale = cableWidth;
      tubeRenderer.tubeTexture = texCable;
      tubeRenderer.sphereTexture = texCable;
      tubeRenderer.tubeJoinedTexture = texCable;
      tubeRenderer.srcJointType = KAS_Tube.tubeJointType.Joined;
      tubeRenderer.tgtJointType = KAS_Tube.tubeJointType.Joined;
      // Set source and target 
      tubeRenderer.srcNode = headAnchorNode;
      tubeRenderer.tgtNode = winchAnchorNode;
      // Load the tube
      tubeRenderer.Load();
    } else {
      tubeRenderer.UnLoad();
    }
  }

  public void GrabHead(Vessel kerbalEvaVessel, KASModulePort grabbedPort = null) {
    KAS_Shared.DebugLog("GrabHead(Winch) Grabbing part");
    //Drop already grabbed head
    KASModuleWinch tmpGrabbbedHead = KAS_Shared.GetWinchModuleGrabbed(kerbalEvaVessel);
    if (tmpGrabbbedHead) {
      KAS_Shared.DebugLog("GrabHead(Winch) - Drop current grabbed head");
      tmpGrabbbedHead.DropHead();
    }

    if (headState == PlugState.PlugDocked || headState == PlugState.PlugUndocked) {
      KAS_Shared.DebugLog("GrabHead(Winch) - Unplug head");
      UnplugHead();
    }

    if (headState == PlugState.Locked) {
      KAS_Shared.DebugLog("GrabHead(Winch) - Deploy head");
      Deploy();
    }

    evaCollider = KAS_Shared.GetEvaCollider(kerbalEvaVessel, "jetpackCollider");

    evaHeadNodeTransform = new GameObject("KASEvaHeadNode").transform;
    evaHeadNodeTransform.parent = evaCollider.transform;
    evaHeadNodeTransform.localPosition = evaGrabHeadPos;
    evaHeadNodeTransform.rotation =
        KAS_Shared.DirectionToQuaternion(evaCollider.transform, evaGrabHeadDir);

    SetHeadToPhysic(false);

    grabbedPortModule = grabbedPort;

    if (grabbedPort) {
      KAS_Shared.DebugLog("GrabHead(Winch) - Moving head to grabbed port node...");
      headTransform.rotation =
          Quaternion.FromToRotation(headPortNode.forward, -grabbedPort.portNode.forward)
          * headTransform.rotation;
      headTransform.position =
          headTransform.position - (headPortNode.position - grabbedPort.portNode.position);
    } else {
      KAS_Shared.DebugLog("GrabHead(Winch) - Moving head to eva node...");
      KAS_Shared.MoveAlign(headTransform, headPortNode, evaHeadNodeTransform);
    }
    // Set cable joint connected body to eva
    SetCableJointConnectedBody(kerbalEvaVessel.rootPart.rb);
    headTransform.parent = evaHeadNodeTransform;
    cableJointLength = cableRealLenght;

    evaHolderPart = kerbalEvaVessel.rootPart;
    release.active = true;
    fxSndHeadGrab.audio.Play();
  }

  public void DropHead() {
    if (!evaHolderPart) {
      KAS_Shared.DebugWarning("DropHead(Winch) - Nothing to drop !");
      return;
    }
    Collider evaCollider = KAS_Shared.GetEvaCollider(evaHolderPart.vessel, "jetpackCollider");
    KAS_Shared.MoveRelatedTo(headTransform, evaCollider.transform, evaDropHeadPos, evaDropHeadRot);

    SetHeadToPhysic(true);
    SetCableJointConnectedBody(headTransform.GetComponent<Rigidbody>());

    if (evaHeadNodeTransform) {
      Destroy(evaHeadNodeTransform.gameObject);
    }

    grabbedPortModule = null;
    release.active = false;
    cableJointLength = cableRealLenght;
    evaHolderPart = null;
    evaHeadNodeTransform = null;
  }

  public void SetHeadToPhysic(bool active) {
    if (active) {
      KAS_Shared.DebugLog("SetHeadToPhysic(Winch) - Create physical object");
      headPhysicModule = this.part.gameObject.GetComponent<KASModulePhysicChild>();
      if (!headPhysicModule) {
        KAS_Shared.DebugLog(
            "SetHeadToPhysic(Winch) - KASModulePhysicChild do not exist, adding it...");
        headPhysicModule = this.part.gameObject.AddComponent<KASModulePhysicChild>();
      }
      headPhysicModule.mass = headMass;
      headPhysicModule.physicObj = headTransform.gameObject;
      headPhysicModule.Start();
    } else {
      headPhysicModule.Stop();
      Destroy(headPhysicModule);
    }
  }

  public void SetCableJointConnectedBody(Rigidbody newBody) {
    //Save body relative position
    Vector3 relativeBodyPos = KAS_Shared.GetLocalPosFrom(newBody.transform, headTransform);
    Quaternion relativeBodyRot = KAS_Shared.GetLocalRotFrom(newBody.transform, headTransform);

    //Save body and head current position
    Vector3 currentPos = headTransform.position;
    Quaternion currentRot = headTransform.rotation;
    Vector3 currentBodyPos = newBody.transform.position;
    Quaternion currentBodyRot = newBody.transform.rotation;

    // Move head and body to lock position
    KAS_Shared.SetPartLocalPosRotFrom(
        headTransform, this.part.transform, headOrgLocalPos, headOrgLocalRot);
    KAS_Shared.SetPartLocalPosRotFrom(
        newBody.transform, headTransform, relativeBodyPos, relativeBodyRot);

    // Connect eva rigidbody
    cableJoint.connectedBody = newBody;
    // Return body and head to the current position
    newBody.transform.position = currentBodyPos;
    newBody.transform.rotation = currentBodyRot;
    headTransform.position = currentPos;
    headTransform.rotation = currentRot;
  }

  public void LockHead() {
    if (evaHolderPart) {
      if (evaHolderPart == FlightGlobals.ActiveVessel.rootPart) {
        DropHead();
        Lock();
      } else {
        ScreenMessages.PostScreenMessage("You didn't have anything to lock in !",
                                         5, ScreenMessageStyle.UPPER_CENTER);
      }
    } else {
      ScreenMessages.PostScreenMessage("Head as not been grabbed !",
                                       5, ScreenMessageStyle.UPPER_CENTER);
    }
  }

  public void PlugHead(KASModulePort portModule, PlugState plugMode, bool fromSave = false,
                       bool fireSound = true, bool alreadyDocked = false) {
    if (plugMode == PlugState.Locked || plugMode == PlugState.Deployed) {
      return;
    }

    if (!alreadyDocked) {
      if (portModule.strutConnected()) {
        ScreenMessages.PostScreenMessage(portModule.part.partInfo.title + " is already used !",
                                         5, ScreenMessageStyle.UPPER_CENTER);
        return;
      }
      if (portModule.plugged) {
        ScreenMessages.PostScreenMessage(portModule.part.partInfo.title + " is already used !",
                                         5, ScreenMessageStyle.UPPER_CENTER);
        return;
      }
      if (this.part.vessel == portModule.part.vessel && fromSave == false) {
        plugMode = PlugState.PlugUndocked;
      }
    }

    if (!cableJoint) {
      Deploy();
    }
    DropHead();
          
    if (plugMode == PlugState.PlugUndocked) {
      KAS_Shared.DebugLog("PlugHead(Winch) - Plug using undocked mode");
      headState = PlugState.PlugUndocked;
      if (fireSound) {
        AudioSource.PlayClipAtPoint(GameDatabase.Instance.GetAudioClip(portModule.plugSndPath),
                                    portModule.part.transform.position);
      }
    }
    if (plugMode == PlugState.PlugDocked) {
      KAS_Shared.DebugLog("PlugHead(Winch) - Plug using docked mode");
      // This should be safe even if already connected
      AttachDocked(portModule);
      // Set attached part
      portModule.part.findAttachNode(portModule.attachNode).attachedPart = this.part;
      this.part.findAttachNode(connectedPortNodeName).attachedPart = portModule.part;
      // Remove joints between connector and winch
      KAS_Shared.RemoveAttachJointBetween(this.part, portModule.part);
      headState = PlugState.PlugDocked;
      if (fireSound) {
        AudioSource.PlayClipAtPoint(
            GameDatabase.Instance.GetAudioClip(portModule.plugDockedSndPath),
            portModule.part.transform.position);
      }
      // Kerbal Joint Reinforcement compatibility
      GameEvents.onPartUndock.Fire(portModule.part);
    }

    KAS_Shared.DebugLog("PlugHead(Winch) - Moving head...");
    headTransform.rotation =
        Quaternion.FromToRotation(headPortNode.forward, -portModule.portNode.forward)
        * headTransform.rotation;
    headTransform.position =
        headTransform.position - (headPortNode.position - portModule.portNode.position);
    SetHeadToPhysic(false);
    SetCableJointConnectedBody(portModule.part.rb);
    headTransform.parent = portModule.part.transform;
    cableJointLength = cableRealLenght + 0.01f;

    // Set variables
    connectedPortInfo.module = portModule;
    connectedPortInfo.module.plugged = true;
    portModule.winchConnected = this;
  }

  public void UnplugHead(bool fireSound = true) {
    if (headState == PlugState.Locked || headState == PlugState.Deployed)
      return;

    if (headState == PlugState.PlugUndocked && fireSound) {
      AudioSource.PlayClipAtPoint(
          GameDatabase.Instance.GetAudioClip(connectedPortInfo.module.plugSndPath),
          connectedPortInfo.module.part.transform.position);
    }
    if (headState == PlugState.PlugDocked) {
      Detach();
      if (fireSound) {
        AudioSource.PlayClipAtPoint(
            GameDatabase.Instance.GetAudioClip(connectedPortInfo.module.unplugDockedSndPath),
            connectedPortInfo.module.part.transform.position);
      }
    }
    SetHeadToPhysic(true);
    SetCableJointConnectedBody(headTransform.GetComponent<Rigidbody>());

    connectedPortInfo.module.winchConnected = null;
    connectedPortInfo.module.nodeConnectedPart = null;
    connectedPortInfo.module.plugged = false;
    connectedPortInfo.module = null;
    nodeConnectedPort = null;
    headState = PlugState.Deployed;
  }

  public void TogglePlugMode() {
    if (headState == PlugState.PlugDocked) {
      ChangePlugMode(PlugState.PlugUndocked);
    } else if (headState == PlugState.PlugUndocked) {
      ChangePlugMode(PlugState.PlugDocked);
    } else {
      ScreenMessages.PostScreenMessage("Cannot change plug mode while not connected !",
                                       5, ScreenMessageStyle.UPPER_CENTER);
    }
  }

  public void ChangePlugMode(PlugState newPlugMode) {
    if (headState == PlugState.PlugDocked || headState == PlugState.PlugUndocked) {
      KASModulePort orgPort = connectedPortInfo.module;
      UnplugHead(false);
      PlugHead(orgPort, newPlugMode, false);
    }
  }

  public void Eject() {
    if (headState == PlugState.Locked && ejectEnabled) {
      Deploy();
      retract.full = false;
      cableJointLength = maxLenght;
      Vector3 force = winchAnchorNode.TransformDirection(Vector3.forward) * ejectForce;
      Rigidbody rb = connectedPortInfo.module
          ? connectedPortInfo.module.part.Rigidbody
          : headTransform.GetComponent<Rigidbody>();

      // Apply ejection force on the projectile and enhance collision check mode. 
      rb.AddForce(force, ForceMode.Force);
      StartCoroutine(LimitFreeFlyDistance(rb, cableJointLength));
      rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
      KAS_Shared.DebugLog(string.Format(
          "Set collision mode to ContinuousDynamic on part {0}", rb));

      // Compensate recoil on the winch.
      this.part.Rigidbody.AddForce(-force, ForceMode.Force);
      fxSndEject.audio.Play();
    }
  }

  /// <summary>A coroutine to restore performance collision check mode.</summary>
  /// <remarks>Given the maximum length of the cable this coroutine estimates how long will it
  /// take for a harpoon to hit anything, and this time is used as a timeout. When a target is
  /// hit the collision check mode get reset in the harpoon's code right at the impact. If
  /// harpoon hit nothing then this coroutine will disable the mode by timeout.</remarks>
  IEnumerator LimitFreeFlyDistance(Rigidbody rb, float maxLength) {
    StopCoroutine("LimitFreeFlyDistance");  // In case of one is running.

    // Give one physics update frame for the eject force to apply. 
    yield return new WaitForFixedUpdate();

    // Figure out how much time it will take for harpoon to fly at the maximum distance. 
    var maxTimeToFly = cableJointLength / rb.velocity.magnitude;
    KAS_Shared.DebugLog(string.Format(
        "Projectile {0} has been ejected at speed {1}. Max cable length {2} will be exahusted"
        + " in {3} seconds.",
        rb, rb.velocity.magnitude, maxLenght, maxTimeToFly));
    yield return new WaitForSeconds(maxTimeToFly + 0.5f);  // Add a delta just in case.

    // Restore performance mode if harpoon hasn't hit anyting.
    if (rb.collisionDetectionMode != CollisionDetectionMode.Discrete) {
      rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
      KAS_Shared.DebugLog(string.Format(
          "Projectile {0} hasn't hit anything. Reset collision check mode to Discrete", rb));
    }
  }

  private bool IsLockable() {
    float distance = Vector3.Distance(winchAnchorNode.position, headAnchorNode.position);
    if (distance > lockMinDist) {
      KAS_Shared.DebugLog("CanLock(Winch) - Can't lock, distance is : " + distance
                          + " and lockMinDist set to : " + lockMinDist);
      return false;
    }
    float fwdDot = Mathf.Abs(Vector3.Dot(winchAnchorNode.forward, headAnchorNode.forward));
    if (fwdDot <= lockMinFwdDot) {
      KAS_Shared.DebugLog("CanLock(Winch) - Can't lock, forward dot is : " + fwdDot
                          + " and lockMinFwdDot set to : " + lockMinFwdDot);
      return false;
    }
    float rollDot = Vector3.Dot(winchAnchorNode.up, headAnchorNode.up);
    if (rollDot <= float.MinValue) {
      KAS_Shared.DebugLog("CanLock(Winch) - Can't lock, roll dot is : " + rollDot
                          + " and lockMinRollDot set to : " + float.MinValue);
      return false;
    }
    return true;
  }

  public KASModuleMagnet GetHookMagnet() {
    if (connectedPortInfo.module) {
      return connectedPortInfo.module.GetComponent<KASModuleMagnet>();
    }
    return null;
  }

  public KASModuleHarpoon GetHookGrapple() {
    if (connectedPortInfo.module) {
      return connectedPortInfo.module.GetComponent<KASModuleHarpoon>();
    }
    return null;
  }

  public void RefreshControlState() {
    if (controlActivated) {
      if (controlInverted) {
        controlField = "Enabled(Inverted)";
      } else {
        controlField = "Enabled";
      }
    } else {
      if (controlInverted) {
        controlField = "Disabled(Inverted)";
      } else {
        controlField = "Disabled";
      }
    }
  }

  public bool CheckBlocked(bool message = false) {
    if (isBlocked && nodeConnectedPart && !nodeConnectedPort) {
      if (message) {
        ScreenMessages.PostScreenMessage(
            "Winch is blocked by " + nodeConnectedPart.partInfo.title + "!",
            5, ScreenMessageStyle.UPPER_CENTER);
      }
      return true;
    }

    return isBlocked = false;
  }

  [KSPEvent(name = "ContextMenuToggleControl", active = true, guiActive = true,
            guiName = "Winch: Toggle Control")]
  public void ContextMenuToggleControl() {
    controlActivated = !controlActivated;
    RefreshControlState();
  }

  [KSPEvent(name = "ContextMenuInvertControl", active = true, guiActive = true,
            guiName = "Winch: Invert control")]
  public void ContextMenuInvertControl() {
    controlInverted = !controlInverted;
    RefreshControlState();
  }

  [KSPEvent(name = "ContextMenuGUI", active = true, guiActive = true, guiName = "Show GUI")]
  public void ContextMenuGUI() {
    KASAddonWinchGUI.ToggleGUI();
  }

  [KSPEvent(name = "ContextMenuPlugMode", active = true, guiActive = true, guiName = "Plug Mode")]
  public void ContextMenuPlugMode() {
    TogglePlugMode();
  }

  [KSPEvent(name = "ContextMenuUnplug", active = true, guiActive = true, guiName = "Unplug")]
  public void ContextMenuUnplug() {
    if (headState == PlugState.PlugDocked || headState == PlugState.PlugUndocked) {
      UnplugHead();
    }
  }

  [KSPEvent(name = "ContextMenuCableStretch", active = true, guiActive = true,
            guiName = "Instant Stretch")]
  public void ContextMenuCableStretch() {
    if (headState != PlugState.Locked) {
      cableJointLength = cableRealLenght;
    }
  }

  [KSPEvent(name = "ContextMenuEject", active = true, guiActive = true, guiName = "Eject")]
  public void ContextMenuEject() {
    if (!CheckBlocked(true)) {
      Eject();
    }
  }

  [KSPEvent(name = "ContextMenuRelease", active = true, guiActive = true, guiName = "Release")]
  public void ContextMenuRelease() {
    if (!CheckBlocked(true)) {
      release.active = !release.active;
    }
  }

  [KSPEvent(name = "ContextMenuRetract", active = true, guiActive = true, guiName = "Retract")]
  public void ContextMenuRetract() {
    if (!CheckBlocked(true)) {
      retract.active = !retract.active;
    }
  }

  [KSPEvent(name = "ContextMenuExtend", active = true, guiActive = true, guiName = "Extend")]
  public void ContextMenuExtend() {
    if (!CheckBlocked(true)) {
      extend.active = !extend.active;
    }
  }

  [KSPEvent(name = "ContextMenuGrabHead", active = true, guiActive = false,
            guiActiveUnfocused = true, guiName = "Grab connector")]
  public void ContextMenuGrabHead() {
    if (headState == PlugState.Locked && nodeConnectedPart) {
      ScreenMessages.PostScreenMessage("Can't grab a connector locked with a part !",
                                       5, ScreenMessageStyle.UPPER_CENTER);
      return;
    }
    if (headState != PlugState.Locked) {
      ScreenMessages.PostScreenMessage("Can't grab a connector already deployed !",
                                       5, ScreenMessageStyle.UPPER_CENTER);
      return;
    }
    GrabHead(FlightGlobals.ActiveVessel);
  }

  [KSPEvent(name = "ContextMenuLockHead", active = true, guiActive = false,
            guiActiveUnfocused = true, guiName = "Lock connector")]
  public void ContextMenuLockHead() {
    LockHead();
  }


  [KSPAction("Eject hook", actionGroup = KSPActionGroup.None)]
  public void ActionGroupEject(KSPActionParam param) {
    if (!this.part.packed && !CheckBlocked(true))
      Eject();
  }

  [KSPAction("Release cable", actionGroup = KSPActionGroup.None)]
  public void ActionGroupRelease(KSPActionParam param) {
    if (!this.part.packed) {
      ContextMenuRelease();
    }
  }

  [KSPAction("Retract cable", actionGroup = KSPActionGroup.None)]
  public void ActionGroupRetract(KSPActionParam param) {
    if (!this.part.packed) {
      ContextMenuRetract();
    }
  }

  [KSPAction("Extend cable", actionGroup = KSPActionGroup.None)]
  public void ActionGroupExtend(KSPActionParam param) {
    if (!this.part.packed) {
      ContextMenuExtend();
    }
  }

  [KSPAction("Plug Mode", actionGroup = KSPActionGroup.None)]
  public void ActionGroupPlugMode(KSPActionParam param) {
    if (!this.part.packed)
      TogglePlugMode();
  }

  [KSPAction("Unplug", actionGroup = KSPActionGroup.None)]
  public void ActionGroupUnplug(KSPActionParam param) {
    if (!this.part.packed) {
      if (headState == PlugState.PlugDocked || headState == PlugState.PlugUndocked) {
        UnplugHead();
      }
    }
  }

  [KSPAction("Toggle key control", actionGroup = KSPActionGroup.None)]
  public void ActionGroupToggleKeyControl(KSPActionParam param) {
    controlActivated = !controlActivated;
    RefreshControlState();
  }

  [KSPAction("Enable key control", actionGroup = KSPActionGroup.None)]
  public void ActionGroupEnableKeyControl(KSPActionParam param) {
    controlActivated = true;
    RefreshControlState();
  }

  [KSPAction("Disable key control", actionGroup = KSPActionGroup.None)]
  public void ActionGroupDisableKeyControl(KSPActionParam param) {
    controlActivated = false;
    RefreshControlState();
  }

  [KSPAction("Toggle inverted key", actionGroup = KSPActionGroup.None)]
  public void ActionGroupToggleInvertedKeyControl(KSPActionParam param) {
    controlInverted = !controlInverted;
    RefreshControlState();
  }

  [KSPAction("Enable inverted key", actionGroup = KSPActionGroup.None)]
  public void ActionGroupEnableInvertedKeyControl(KSPActionParam param) {
    controlInverted = true;
    RefreshControlState();
  }

  [KSPAction("Disable inverted key", actionGroup = KSPActionGroup.None)]
  public void ActionGroupDisableInvertedKeyControl(KSPActionParam param) {
    controlInverted = false;
    RefreshControlState();
  }

  // Key control event
  public void EventWinchExtend(bool activated) {
    if (!this.part.packed && !CheckBlocked() && controlActivated) {
      if (controlInverted) {
        retract.active = activated;
      } else {
        extend.active = activated;
      }
    }
  }

  public void EventWinchRetract(bool activated) {
    if (!this.part.packed && !CheckBlocked() && controlActivated) {
      if (controlInverted) {
        extend.active = activated;
      } else {
        retract.active = activated;
      }
    }
  }

  public void EventWinchHeadLeft() {
    if (!this.part.packed && controlActivated && headState != PlugState.Locked
        && connectedPortInfo.module) {
      connectedPortInfo.module.TurnLeft();
    }
  }

  public void EventWinchHeadRight() {
    if (!this.part.packed && controlActivated && headState != PlugState.Locked
        && connectedPortInfo.module) {
      connectedPortInfo.module.TurnRight();
    }
  }

  public void EventWinchEject() {
    if (!this.part.packed && controlActivated && !CheckBlocked()) {
      Eject();
    }
  }

  public void EventWinchHook() {
    if (this.part.packed || !controlActivated) {
      return;
    }
    if (GetHookMagnet()) {
      GetHookMagnet().ContextMenuMagnet();
    }
    if (GetHookGrapple()) {
      GetHookGrapple().ContextMenuDetach();
    }
  }
}

}  // namespace
