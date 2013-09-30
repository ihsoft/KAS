using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine;
using KSP.IO;

namespace KAS
{
    public class KASModuleWinchHead : MonoBehaviour
    {
        public KASModuleWinch connectedWinch;
    }

    public class KASModuleWinch : KASModuleAttachCore
    {
        //Part.cfg file
        [KSPField] public float maxLenght = 50.0f;
        [KSPField] public float cableSpring = 1000.0f;
        [KSPField] public float cableDamper = 0.1f;
        [KSPField] public float cableWidth = 0.04f;
        [KSPField] public float motorMaxSpeed = 2f;
        [KSPField] public float motorMinSpeed = 0.01f;
        [KSPField] public float motorAcceleration = 0.05f;
        [KSPField] public float powerDrain = 0.5f;
        [KSPField] public float releaseOffset = 1f;
        [KSPField] public string headTransformName = "head";
        [KSPField] public float headMass = 0.01f;
        [KSPField] public string headPortNodeName = "portNode";
        [KSPField] public string connectedPortNodeName = "bottom";
        [KSPField] public string anchorNodeName = "anchorNode";
        [KSPField] public Vector3 evaGrabHeadPos = new Vector3(0.05f, 0.01f, -0.11f);
        [KSPField] public Vector3 evaGrabHeadDir = new Vector3(0f, 0f, -1f);
        [KSPField] public Vector3 evaDropHeadPos = new Vector3(0.05f, 0.01f, -0.16f);
        [KSPField] public Vector3 evaDropHeadRot = new Vector3(180f, 0f, 0f);
        [KSPField] public bool ejectEnabled = true;
        [KSPField] public float ejectForce = 20f;
        [KSPField] public float lockMinDist = 0.08f;
        [KSPField] public float lockMinFwdDot = 0.90f;

        //Sounds & texture
        [KSPField] private string cableTexPath = "KAS/Textures/cable";
        [KSPField] private string motorSndPath = "KAS/Sounds/winchSmallMotor";
        [KSPField] private string motorStartSndPath = "KAS/Sounds/winchSmallMotorStart";
        [KSPField] private string motorStopSndPath = "KAS/Sounds/winchSmallMotorStop";
        [KSPField] private string headLockSndPath = "KAS/Sounds/winchSmallLock";
        [KSPField] private string ejectSndPath = "KAS/Sounds/winchSmallEject";
        [KSPField] public string headGrabSndPath = "KAS/Sounds/grab";

        [KSPField(guiActive = true, guiName = "Key control", guiFormat="S")] public string controlField = "";
        [KSPField(guiActive = true, guiName = "Head State", guiFormat="S")] public string headStateField = "Locked";
        [KSPField(guiActive = true, guiName = "Cable State", guiFormat="S")] public string winchStateField = "Idle";
        [KSPField(guiActive = true, guiName = "Lenght", guiFormat = "F2", guiUnits="m")] public float lengthField = 0.0f;

        // FX
        public FXGroup fxSndMotorStart, fxSndMotor, fxSndMotorStop, fxSndHeadLock, fxSndEject, fxSndHeadGrab;
        private Texture2D texCable;
        public KAS_Tube tubeRenderer;

        // Winch GUI
        [KSPField(isPersistant = true)] public string winchName = "";
        public bool isActive = true;
        public bool guiRepeatRetract = false;
        public bool guiRepeatExtend = false;
        public bool guiRepeatTurnLeft = false;
        public bool guiRepeatTurnRight= false;
        public bool highLightStarted = false;

        // Transforms
        public Transform headTransform;
        private Transform headPortNode;
        private Transform winchAnchorNode;
        private Transform headAnchorNode;

        // Cable control
        [KSPField(isPersistant = true)] private bool controlActivated = true;
        [KSPField(isPersistant = true)] private bool controlInverted = false;
        public KAS_Shared.cableControl release;
        public KAS_Shared.cableControl retract;
        public KAS_Shared.cableControl extend;
        public float motorSpeed = 0f;
        public float motorSpeedSetting;
        
        public Part evaHolderPart = null;
        private float orgKerbalMass;
        private Transform evaHeadNodeTransform;
        private Collider evaCollider;

        // Plug
        public FixedJoint headJoint;
        private PlugState headStateVar = PlugState.Locked;

        public PlugState headState
        {
            get { return headStateVar; }
            set
            {
                headStateVar = value;
                if (headStateVar == PlugState.Locked) headStateField = "Locked";
                if (headStateVar == PlugState.Deployed) headStateField = "Deployed";
                if (headStateVar == PlugState.PlugUndocked) headStateField = "Plugged(Undocked)";
                if (headStateVar == PlugState.PlugDocked) headStateField = "Plugged(Docked)";
            }
        }

        public PortInfo connectedPortInfo;
        public struct PortInfo
        {
            public KASModulePort module;
            public string savedVesselID;
            public string savedPartID;
        }

        // Cable & Head
        public SpringJoint cableJoint;
        private Vector3 headOrgLocalPos;
        private Quaternion headOrgLocalRot;
        private Vector3 headCurrentLocalPos;
        private Quaternion headCurrentLocalRot;
        private float orgWinchMass;

        public enum PlugState
        {
            Locked = 0,
            Deployed = 1,
            PlugDocked = 2,
            PlugUndocked = 3,
        }

        public float cableJointLength
        {
            get
            {
                if (cableJoint) return cableJoint.maxDistance;
                else return 0;
            }
            set
            {
                if (cableJoint)
                {
                    cableJoint.maxDistance = lengthField = value;
                }
            }
        }

        public float cableRealLenght
        {
            get
            {
                if (cableJoint)
                {
                    return Vector3.Distance(winchAnchorNode.position, headAnchorNode.position);
                }
                else return 0;
            }
        }

        public Part nodeConnectedPart
        {
            get
            {
                AttachNode an = this.part.findAttachNode(connectedPortNodeName);
                if (an != null)
                {
                    if (an.attachedPart)
                    {
                        return an.attachedPart;
                    }
                }
                return null;
            }
            set
            {
                AttachNode an = this.part.findAttachNode(connectedPortNodeName);
                if (an != null)
                {
                    an.attachedPart = value;
                }
                else
                {
                    KAS_Shared.DebugError("connectedPort(Winch) Cannot set connectedPart !");
                }
            }
        }

        public KASModulePort nodeConnectedPort
        {
            get
            {
                AttachNode an = this.part.findAttachNode(connectedPortNodeName);
                if (an != null)
                {
                    if (an.attachedPart)
                    {
                        KASModulePort portModule = an.attachedPart.GetComponent<KASModulePort>();
                        if (portModule)
                        {
                            return portModule;
                        }
                    }
                }
                return null;
            }
            set
            {
                AttachNode an = this.part.findAttachNode(connectedPortNodeName);
                if (an != null)
                {
                    if (value)
                    {
                        an.attachedPart = value.part;
                    }
                    else
                    {
                        an.attachedPart = null;
                    }
                }
                else
                {
                    KAS_Shared.DebugError("connectedPort(Winch) Cannot set connectedPort !");
                }
            }
        }

        public bool PlugDocked
        {
            get
            {
                if (headState == PlugState.PlugDocked)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                if ((value == true))
                {
                    if (headState == PlugState.PlugUndocked)
                    {
                        ChangePlugMode(PlugState.PlugDocked);
                    }
                }
                else
                {
                    if (headState == PlugState.PlugDocked)
                    {
                        ChangePlugMode(PlugState.PlugUndocked);
                    }
                }
            }
        }
        
        public override string GetInfo()
        {
            string info = base.GetInfo();
            info += "---- Winch ----";
            info += "\n";
            info += "Max lenght : " + maxLenght + " m";
            info += "\n";
            info += "Cable spring : " + cableSpring;
            info += "\n";
            info += "Cable damper : " + cableDamper;
            info += "\n";
            info += "Cable width : " + cableWidth;
            info += "\n";
            info += "Motor max speed : " + motorMaxSpeed;
            info += "\n";
            info += "Motor min speed : " + motorMinSpeed;
            info += "\n";
            info += "Motor acceleration : " + motorAcceleration;
            info += "\n";
            info += "Power consumption : " + powerDrain + "/s";
            if (ejectEnabled)
            {
                info += "\n";
                info += "Eject force : " + ejectForce;
            }
            info += "\n";
            info += "Grab head key : " + KASAddonControlKey.grabHeadKey;
            info += "\n";
            info += "Extend key : " + KASAddonControlKey.winchExtendKey;
            info += "\n";
            info += "Retract key : " + KASAddonControlKey.winchRetractKey;
            info += "\n";
            info += "Rotate left key : " + KASAddonControlKey.winchHeadLeftKey;
            info += "\n";
            info += "Rotate right key : " + KASAddonControlKey.winchHeadRightKey;
            info += "\n";
            info += "Toogle hook : " + KASAddonControlKey.winchHookKey;
            if (ejectEnabled)
            {
                info += "\n";
                info += "Eject key : " + KASAddonControlKey.winchEjectKey;
            }
           
            return info;
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            if (headState != PlugState.Locked)
            {
                KAS_Shared.DebugLog("OnSave(Winch) Winch head deployed, saving info...");
                ConfigNode cableNode = node.AddNode("Head");
                cableNode.AddValue("headLocalPos", KSPUtil.WriteVector(KAS_Shared.GetLocalPosFrom(headTransform, this.part.transform)));
                cableNode.AddValue("headLocalRot", KSPUtil.WriteQuaternion(KAS_Shared.GetLocalRotFrom(headTransform, this.part.transform)));     
            }

            if (headState == PlugState.PlugDocked || headState == PlugState.PlugUndocked)
            {
                ConfigNode plugNode = node.AddNode("PLUG");
                if (headState == PlugState.PlugDocked) plugNode.AddValue("type", "docked");
                if (headState == PlugState.PlugUndocked) plugNode.AddValue("type", "undocked");
                plugNode.AddValue("vesselId", connectedPortInfo.module.vessel.id.ToString());
                plugNode.AddValue("partId", connectedPortInfo.module.part.flightID.ToString());
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasNode("Head"))
            {
                KAS_Shared.DebugLog("OnLoad(Winch) Loading winch head info from save...");
                ConfigNode cableNode = node.GetNode("Head");
                headCurrentLocalPos = KSPUtil.ParseVector3(cableNode.GetValue("headLocalPos"));
                headCurrentLocalRot = KSPUtil.ParseQuaternion(cableNode.GetValue("headLocalRot"));
                headState = PlugState.Deployed;
            }

            if (node.HasNode("PLUG"))
            {
                KAS_Shared.DebugLog("OnLoad(Winch) Loading plug info from save...");
                ConfigNode plugNode = node.GetNode("PLUG");
                connectedPortInfo.savedVesselID = plugNode.GetValue("vesselId").ToString();
                connectedPortInfo.savedPartID = plugNode.GetValue("partId").ToString();
                if (plugNode.GetValue("type").ToString() == "docked")
                {
                    headState = PlugState.PlugDocked;
                }
                if (plugNode.GetValue("type").ToString() == "undocked")
                {
                    headState = PlugState.PlugUndocked;
                }
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (state == StartState.Editor || state == StartState.None) return;

            texCable = GameDatabase.Instance.GetTexture(cableTexPath, false);
            if (!texCable)
            {
                KAS_Shared.DebugError("cable texture loading error !");
                ScreenMessages.PostScreenMessage("Texture file : " + cableTexPath + " as not been found, please check your KAS installation !", 10, ScreenMessageStyle.UPPER_CENTER);
            }
            KAS_Shared.createFXSound(this.part, fxSndMotorStart, motorStartSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndMotor, motorSndPath, true);
            KAS_Shared.createFXSound(this.part, fxSndMotorStop, motorStopSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndHeadLock, headLockSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndEject, ejectSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndHeadGrab, headGrabSndPath, false);

            // Get head transform
            headTransform = this.part.FindModelTransform(headTransformName);
            if (!headTransform)
            {
                KAS_Shared.DebugError("OnStart(Winch) Head transform " + headTransformName + " not found in the model !");
                DisableWinch();
                return;
            }

            // get winch anchor node
            winchAnchorNode = this.part.FindModelTransform(anchorNodeName);
            if (!winchAnchorNode)
            { 
                KAS_Shared.DebugError("OnStart(Winch) Winch anchor tranform node " + anchorNodeName + " not found in the model !");
                DisableWinch();
                return;
            }

            // Get head port node
            headPortNode = this.part.FindModelTransform(headPortNodeName);
            if (!headPortNode)
            {
                KAS_Shared.DebugError("OnStart(Winch) Head transform port node " + headPortNodeName + " not found in the model !");
                DisableWinch();
                return;
            }

            // Set head module 
            KASModuleWinchHead winchHeadModule = headTransform.gameObject.AddComponent<KASModuleWinchHead>();
            winchHeadModule.connectedWinch = this;

            // Create head anchor node
            headAnchorNode = new GameObject("KASHeadAnchor").transform;
            headAnchorNode.position = winchAnchorNode.position;
            headAnchorNode.rotation = winchAnchorNode.rotation;
            headAnchorNode.parent = headTransform;
            headAnchorNode.rotation *= Quaternion.Euler(new Vector3(180f, 0f, 0f));
    
            // Get original head position and rotation
            headOrgLocalPos = KAS_Shared.GetLocalPosFrom(headTransform, this.part.transform);
            headOrgLocalRot = KAS_Shared.GetLocalRotFrom(headTransform, this.part.transform);

            if (nodeConnectedPort)
            {
                KAS_Shared.DebugWarning("OnStart(Winch) NodeConnectedPort is : " + nodeConnectedPort.part.partInfo.title);
            }
            else
            {
                if (nodeConnectedPart)
                {
                    KAS_Shared.DebugError("OnStart(Winch) Connected part is not a port, configuration not supported !");
                    DisableWinch();
                    return;
                }
                else
                {
                    KAS_Shared.DebugWarning("OnStart(Winch) No connected part found !");
                }      
            }

            // Get saved port module if any
            if (headState == PlugState.PlugDocked || headState == PlugState.PlugUndocked)
            {
                Part connectedPartSaved = KAS_Shared.GetPartByID(connectedPortInfo.savedVesselID, connectedPortInfo.savedPartID);
                if (connectedPartSaved)
                {
                    KASModulePort connectedPortSaved = connectedPartSaved.GetComponent<KASModulePort>();
                    if (connectedPortSaved)
                    {
                        connectedPortInfo.module = connectedPortSaved;
                    }
                    else
                    {
                        KAS_Shared.DebugError("OnStart(Winch) Unable to get saved plugged port module !");
                        headState = PlugState.Locked;
                    }
                }
                else
                {
                    KAS_Shared.DebugError("OnStart(Winch) Unable to get saved plugged part !");
                    headState = PlugState.Locked;
                }
            }
        
            if (headState != PlugState.Locked)
            {
                KAS_Shared.SetPartLocalPosRotFrom(headTransform, this.part.transform, headCurrentLocalPos, headCurrentLocalRot);
                SetTubeRenderer(true);
            }

            motorSpeedSetting = motorMaxSpeed / 2;

            KAS_Shared.DebugWarning("OnStart(Winch) HeadState : " + headState);
            GameEvents.onVesselGoOnRails.Add(new EventData<Vessel>.OnEvent(this.OnVesselGoOnRails));
            GameEvents.onVesselGoOffRails.Add(new EventData<Vessel>.OnEvent(this.OnVesselGoOffRails));
        }

        void OnVesselGoOnRails(Vessel vess)
        {
            if (vess != this.vessel) return;
            if (headState == PlugState.Deployed)
            {
                headTransform.rigidbody.isKinematic = true;
            } 
        }

        void OnVesselGoOffRails(Vessel vess)
        {
            if (vess != this.vessel) return;
            // From save
            if (headState == PlugState.Deployed && !cableJoint)
            {
                KAS_Shared.DebugLog("OnVesselGoOffRails(Winch) Head deployed or docked and no cable joint exist, re-deploy and set head position");
                Deploy();
                KAS_Shared.SetPartLocalPosRotFrom(headTransform, this.part.transform, headCurrentLocalPos, headCurrentLocalRot);
                cableJointLength = cableRealLenght;
            }

            if (headState == PlugState.PlugUndocked && !headJoint)
            {
                KAS_Shared.DebugLog("OnVesselGoOffRails(Winch) Plug (undocked) and no head joint found, Re-plug to : " + connectedPortInfo.module.part.partInfo.title);
                PlugHead(connectedPortInfo.module, PlugState.PlugUndocked, true, false);
            }

            if (headState == PlugState.PlugDocked && !headJoint)
            {
                KAS_Shared.DebugLog("OnVesselGoOffRails(Winch) Plug (docked) and no head joint found, deploying head and move port(s) parts");
                PlugHead(connectedPortInfo.module, PlugState.PlugDocked, true, false);
            }
            if (headTransform.rigidbody) headTransform.rigidbody.isKinematic = false;
        }
     
        void OnDestroy()
        {
            GameEvents.onVesselGoOnRails.Remove(new EventData<Vessel>.OnEvent(this.OnVesselGoOnRails));
            GameEvents.onVesselGoOffRails.Remove(new EventData<Vessel>.OnEvent(this.OnVesselGoOffRails));
        }
     
        public override void OnUpdate()
        {
            base.OnUpdate();
            if (!HighLogic.LoadedSceneIsFlight) return;
            UpdateMotor();
            UpdateOrgPos();
        }

        private void DisableWinch()
        {
            Events["ContextMenuRelease"].guiActive = false;
            Events["ContextMenuRetract"].guiActive = false;
            Events["ContextMenuExtend"].guiActive = false;
            Events["ContextMenuGrabHead"].guiActiveUnfocused = false;
            Events["ContextMenuLockHead"].guiActiveUnfocused = false;
            isActive = false;
        }

        private void UpdateMotor()
        {         
            #region release
            if (release.active)
            {
                if (headState == PlugState.Locked)
                {
                    cableJointLength = 0f;
                    Deploy();
                }
                release.isrunning = true;
                if (!release.starting)
                {
                    retract.active = false;
                    extend.active = false;
                    retract.full = false;
                    release.starting = true;
                    motorSpeed = 0;
                    winchStateField = "Released";
                }
                float tempCablelenghtF = cableRealLenght + releaseOffset;
                if (tempCablelenghtF > maxLenght)
                {
                    release.active = false;
                    cableJointLength = maxLenght;
                }
                else
                {
                    cableJointLength = tempCablelenghtF;
                }
            }
            else
            {
                if (release.isrunning)
                {
                    release.isrunning = false;
                    release.starting = false;
                    winchStateField = "Idle";
                    release.active = false;
                }
            }
            #endregion
            
            #region Extend
            if (extend.active && !extend.full)
            {
                if (headState == PlugState.Locked)
                {
                    cableJointLength = 0f;
                    Deploy();
                }
                if (KAS_Shared.RequestPower(this.part, powerDrain))
                {
                    extend.isrunning = true;
                    if (!extend.starting)
                    {
                        retract.full = false;
                        retract.active = false;
                        release.active = false;
                        extend.starting = true;
                        winchStateField = "Extending cable...";
                        motorSpeed = 0;
                        fxSndMotorStart.audio.loop = false;
                        fxSndMotorStart.audio.Play();
                    }

                    if (motorSpeedSetting <= 0) motorSpeedSetting = motorMinSpeed;
                    if (motorSpeed < motorSpeedSetting)
                    {
                        motorSpeed += motorAcceleration;
                    }
                    if (motorSpeed > motorSpeedSetting + motorAcceleration)
                    {
                        motorSpeed -= motorAcceleration;
                    }
                    float tempCablelenghtE = cableJointLength + motorSpeed * TimeWarp.deltaTime;
                    if (tempCablelenghtE > maxLenght)
                    {
                        extend.full = true;
                        extend.active = false;
                        cableJointLength = maxLenght;
                    }
                    else
                    {
                        if (!fxSndMotor.audio.isPlaying)
                        {
                            fxSndMotor.audio.Play();
                        }
                        cableJointLength = tempCablelenghtE;
                    }
                }
                else
                {
                    if (this.part.vessel == FlightGlobals.ActiveVessel)
                    {
                        ScreenMessages.PostScreenMessage("Winch stopped ! Insufficient Power", 5, ScreenMessageStyle.UPPER_CENTER);
                    }
                    winchStateField = "Insufficient Power";
                    StopExtend();
                }
            }
            else
            {
                StopExtend();
            }
            #endregion

            #region retract
            if (retract.active && !retract.full)
            {
                if (headState == PlugState.Locked)
                {
                    StopRetract();
                    return;
                }
                if (KAS_Shared.RequestPower(this.part, powerDrain))
                {
                    retract.isrunning = true;
                    if (!retract.starting)
                    {
                        extend.full = false;
                        extend.active = false;
                        release.active = false;
                        retract.starting = true;
                        winchStateField = "Retracting cable...";
                        motorSpeed = 0;
                        fxSndMotorStart.audio.loop = false;
                        fxSndMotorStart.audio.Play();
                    }

                    if (motorSpeedSetting <= 0) motorSpeedSetting = motorMinSpeed;
                    if (motorSpeed < motorSpeedSetting)
                    {
                        motorSpeed += motorAcceleration;
                    }
                    if (motorSpeed > motorSpeedSetting + motorAcceleration)
                    {
                        motorSpeed -= motorAcceleration;
                    }
                    float tempCableLenghtR = cableJointLength - motorSpeed * TimeWarp.deltaTime;
                    if (tempCableLenghtR > 0)
                    {
                        if (!fxSndMotor.audio.isPlaying)
                        {
                            fxSndMotor.audio.Play();
                        }
                        cableJointLength = tempCableLenghtR;
                    }
                    else
                    {
                        OnFullRetract();
                    }
                }
                else
                {
                    if (this.part.vessel == FlightGlobals.ActiveVessel)
                    {
                        ScreenMessages.PostScreenMessage("Winch stopped ! Insufficient Power", 5, ScreenMessageStyle.UPPER_CENTER);
                    }
                    winchStateField = "Insufficient Power";
                    StopRetract();
                }
            }
            else
            {
                StopRetract();
            }
            #endregion
        }

        private void UpdateOrgPos()
        {
            if (headState == PlugState.PlugDocked)
            {
                if (IsUpDown(connectedPortInfo.module.part))
                {
                    KAS_Shared.UpdateChildsOrgPos(this.part);
                }
                if (IsDownUp(connectedPortInfo.module.part))
                {
                    KAS_Shared.UpdateChildsOrgPos(connectedPortInfo.module.part);
                }
            }
        }

        private void StopExtend()
        {
            if (extend.isrunning)
            {
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

        private void StopRetract()
        {
            if (retract.isrunning)
            {
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

        public void OnFullRetract()
        {
            if ((IsLockable() || headState == PlugState.Deployed) && !evaHolderPart)
            {
                retract.full = true;
                retract.active = false;
                cableJointLength = 0;
                Lock();
            }
            else
            {
                ScreenMessages.PostScreenMessage("Connected parts not aligned ! Locking impossible.", 5, ScreenMessageStyle.UPPER_CENTER);
                retract.active = false;
            } 
        }

        public void Deploy()
        {
            KAS_Shared.DebugLog("Deploy(Winch) - Return head to original pos");
            KAS_Shared.SetPartLocalPosRotFrom(headTransform, this.part.transform, headOrgLocalPos, headOrgLocalRot);

            KAS_Shared.DebugLog("Deploy(Winch) - Create physical object");


            KAS_Shared.CreatePhysicObject(headTransform, headMass, this.part.rigidbody);
            orgWinchMass = this.part.mass;
            float newMass = this.part.mass - headMass;
            if (newMass > 0)
            {
                this.part.mass = newMass;
            }
            else
            {
                KAS_Shared.DebugWarning("Deploy(Winch) - Mass of the head is greater than the winch !");
            }

            KAS_Shared.DebugLog("Deploy(Winch) - Create spring joint");
            cableJoint = this.part.gameObject.AddComponent<SpringJoint>();
            cableJoint.connectedBody = headTransform.rigidbody;
            cableJoint.maxDistance = 0;
            cableJoint.minDistance = 0;
            cableJoint.spring = cableSpring;
            cableJoint.damper = cableDamper;
            cableJoint.breakForce = 999;
            cableJoint.breakTorque = 999;
            cableJoint.anchor = winchAnchorNode.localPosition;

            if (nodeConnectedPort)
            {
                KAS_Shared.DebugLog("Deploy(Winch) - Connected port detected, plug head in docked mode...");
                KASModulePort tmpPortModule = nodeConnectedPort;
                if (attachMode.Docked)
                {
                    Detach();
                }
                else
                {
                    if (IsUpDown(nodeConnectedPart)) nodeConnectedPart.decouple();
                    if (IsDownUp(nodeConnectedPart)) this.part.decouple();
                }
                PlugHead(tmpPortModule, PlugState.PlugDocked);
            }
            else
            {
                headState = PlugState.Deployed;
            }

            KAS_Shared.DebugLog("Deploy(Winch) - Enable tube renderer");
            SetTubeRenderer(true);

            //KAS_Shared.DisableVesselCollision(this.part.vessel, headTransform.collider);
        }

        public void Lock()
        {
            if (cableJoint)
            {
                KAS_Shared.DebugLog("Lock(Winch) Removing spring joint");
                Destroy(cableJoint);
            }
            KAS_Shared.SetPartLocalPosRotFrom(headTransform, this.part.transform, headOrgLocalPos, headOrgLocalRot);

            if (headState == PlugState.PlugDocked || headState == PlugState.PlugUndocked)
            {
                KAS_Shared.DebugLog("Lock(Winch) Dock connected port");
                KASModulePort tmpPortModule = connectedPortInfo.module;
                UnplugHead(false);
                KAS_Shared.MoveVesselRelatedToTransform(tmpPortModule.part.vessel, tmpPortModule.portNode, this.part.vessel, this.headPortNode);
                AttachDocked(tmpPortModule);
                nodeConnectedPort = tmpPortModule;
            }

            KAS_Shared.RemovePhysicObject(this.part, headTransform);
            this.part.mass = orgWinchMass;

            SetTubeRenderer(false);
            motorSpeed = 0;
            cableJoint = null;
            headState = PlugState.Locked;
            fxSndHeadLock.audio.Play();
        }

        public void SetTubeRenderer(bool activated)
        {
            if (activated)
            {
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
                tubeRenderer.tubeHasCollider = false;
                // Set source and target 
                tubeRenderer.srcNode = headAnchorNode;
                tubeRenderer.tgtNode = winchAnchorNode;
                // Load the tube
                tubeRenderer.Load();
            }
            else
            {
                tubeRenderer.UnLoad();
            }
        }

        public void GrabHead(Vessel kerbalEvaVessel)
        {
            KAS_Shared.DebugLog("GrabHead(Winch) Grabbing part");
            //Drop already grabbed head
            KASModuleWinch tmpGrabbbedHead = KAS_Shared.GetWinchModuleGrabbed(kerbalEvaVessel);
            if (tmpGrabbbedHead)
            {
                KAS_Shared.DebugLog("GrabHead(Winch) - Drop current grabbed head");
                tmpGrabbbedHead.DropHead();
            }

            if (headState == PlugState.PlugDocked || headState == PlugState.PlugUndocked)
            {
                KAS_Shared.DebugLog("GrabHead(Winch) - Unplug head");
                UnplugHead();
            }

            if (headState == PlugState.Locked)
            {
                KAS_Shared.DebugLog("GrabHead(Winch) - Deploy head");
                Deploy();
            }

            evaCollider = KAS_Shared.GetEvaCollider(kerbalEvaVessel, "jetpackCollider");

            evaHeadNodeTransform = new GameObject("KASEvaHeadNode").transform;
            evaHeadNodeTransform.parent = evaCollider.transform;
            evaHeadNodeTransform.localPosition = evaGrabHeadPos;
            evaHeadNodeTransform.rotation = KAS_Shared.DirectionToQuaternion(evaCollider.transform, evaGrabHeadDir);

            KAS_Shared.RemovePhysicObject(this.part, headTransform);

            KAS_Shared.MoveAlign(headTransform, headPortNode, evaHeadNodeTransform);
            // Parent eva to head for moving eva with the head
            kerbalEvaVessel.rootPart.transform.parent = headTransform;
            // Set cable joint connected body to eva
            SetCableJointConnectedBody(kerbalEvaVessel.rootPart.rigidbody);
            // Unparent eva to head
            kerbalEvaVessel.rootPart.transform.parent = null;

            headTransform.parent = evaHeadNodeTransform;
            cableJointLength = cableRealLenght;

            evaHolderPart = kerbalEvaVessel.rootPart;
            release.active = true;
            fxSndHeadGrab.audio.Play();
        }

        public void DropHead()
        {
            if (!evaHolderPart)
            {
                KAS_Shared.DebugWarning("DropHead(Winch) - Nothing to drop !");
                return;
            }
            Collider evaCollider = KAS_Shared.GetEvaCollider(evaHolderPart.vessel, "jetpackCollider");
            KAS_Shared.MoveRelatedTo(headTransform, evaCollider.transform, evaDropHeadPos, evaDropHeadRot);

            headTransform.parent = null;
            KAS_Shared.CreatePhysicObject(headTransform, headMass, evaHolderPart.rigidbody);
            SetCableJointConnectedBody(headTransform.rigidbody);

            if (evaHeadNodeTransform) Destroy(evaHeadNodeTransform.gameObject);

            release.active = false;
            cableJointLength = cableRealLenght;
            evaHolderPart = null;
            evaHeadNodeTransform = null;
        }

        public void SetCableJointConnectedBody(Rigidbody newBody)
        {
            //Save current connector position
            Vector3 currentPos = headTransform.position;
            Quaternion currentRot = headTransform.rotation;
            // Move head to lock position
            KAS_Shared.SetPartLocalPosRotFrom(headTransform, this.part.transform, headOrgLocalPos, headOrgLocalRot);
            // Connect eva rigidbody
            cableJoint.connectedBody = newBody;
            // Return connector to the current position
            headTransform.position = currentPos;
            headTransform.rotation = currentRot;
        }

        public void LockHead()
        {
            if (evaHolderPart)
            {
                if (evaHolderPart == FlightGlobals.ActiveVessel.rootPart)
                {
                    DropHead();
                    Lock();
                }
                else
                {
                    ScreenMessages.PostScreenMessage("You didn't have anything to lock in !", 5, ScreenMessageStyle.UPPER_CENTER);
                }
            }
            else
            {
                ScreenMessages.PostScreenMessage("Head as not been grabbed !", 5, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        public void PlugHead(KASModulePort portModule, PlugState plugMode, bool fromSave = false, bool fireSound = true)
        {
            if (plugMode == PlugState.Locked || plugMode == PlugState.Deployed) return;
            if (portModule.strutConnected())
            {
                ScreenMessages.PostScreenMessage(portModule.part.partInfo.title + " is already used !", 5, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (portModule.plugState == KASModulePort.KASPlugState.PlugDock || portModule.plugState == KASModulePort.KASPlugState.PlugUndock)
            {
                ScreenMessages.PostScreenMessage(portModule.part.partInfo.title + " is already used !", 5, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (this.part.vessel == portModule.part.vessel && fromSave == false)
            {
                plugMode = PlugState.PlugUndocked;
            }

            if (!cableJoint) Deploy();
            DropHead();

            if (plugMode == PlugState.PlugUndocked)
            {
                KAS_Shared.DebugLog("PlugHead(Winch) - Plug using undocked mode");
                headState = PlugState.PlugUndocked;
                if (fireSound) portModule.fxSndPlug.audio.Play();
            }
            if (plugMode == PlugState.PlugDocked)
            {
                KAS_Shared.DebugLog("PlugHead(Winch) - Plug using docked mode");  
                if (!fromSave)
                {
                    AttachDocked(portModule);
                }
                // Remove joints between connector and winch
                KAS_Shared.RemoveFixedJointBetween(this.part, portModule.part);
                KAS_Shared.RemoveHingeJointBetween(this.part, portModule.part);
                headState = PlugState.PlugDocked;
                nodeConnectedPort = portModule;
                if (fireSound) portModule.fxSndPlugDocked.audio.Play();
            }

            // Move head
            headTransform.rotation = Quaternion.FromToRotation(headPortNode.forward, -portModule.portNode.forward) * headTransform.rotation;
            headTransform.position = headTransform.position - (headPortNode.position - portModule.portNode.position);
            cableJointLength = cableRealLenght;
 
            // Create joint
            if (headJoint) Destroy(headJoint);
            headJoint = portModule.part.gameObject.AddComponent<FixedJoint>();
            headJoint.connectedBody = headTransform.rigidbody;
            headJoint.breakForce = portModule.breakForce;
            headJoint.breakTorque = portModule.breakForce;

            // Set variables
            connectedPortInfo.module = portModule;
            portModule.winchConnected = this;
        }

        public void UnplugHead(bool fireSound = true)
        {
            if (headState == PlugState.PlugUndocked || headState == PlugState.PlugDocked)
            {
                if (headState == PlugState.PlugUndocked)
                {
                    if (fireSound) connectedPortInfo.module.fxSndUnplug.audio.Play();
                }
                if (headState == PlugState.PlugDocked)
                {
                    Detach();
                    if (fireSound) connectedPortInfo.module.fxSndUnplugDocked.audio.Play();
                }
                connectedPortInfo.module.winchConnected = null;
                connectedPortInfo.module = null;
            }
            if (headJoint) Destroy(headJoint);
            headJoint = null;
            nodeConnectedPort = null;
            headState = PlugState.Deployed;
        }

        public void TogglePlugMode()
        {
            if (headState == PlugState.PlugDocked)
            {
                ChangePlugMode(PlugState.PlugUndocked);
            }
            else if (headState == PlugState.PlugUndocked)
            {
                ChangePlugMode(PlugState.PlugDocked);
            }
            else
            {
                ScreenMessages.PostScreenMessage("Cannot change plug mode while not connected !", 5, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        public void ChangePlugMode(PlugState newPlugMode)
        {
            if (headState == PlugState.PlugDocked || headState == PlugState.PlugUndocked)
            {
                KASModulePort orgPort = connectedPortInfo.module;
                UnplugHead(false);
                PlugHead(orgPort, newPlugMode, false);
            }
        }

        public void MovePort(KASModulePort portModule, Vector3 position, Quaternion rotation)
        {
            if (!IsUpDown(portModule.part) && !IsDownUp(portModule.part))
            {
                KAS_Shared.DebugError("SetPartAsMoveable(Core) - Unsupported configuration, moveable part must be parent or child of the core module !");
                return;
            }
            if (IsUpDown(portModule.part))
            {
                KAS_Shared.DebugLog("MoveMoveablePart(Core) - Moveable part is up>down");
                List<Part> allChilds = KAS_Shared.GetAllChilds(portModule.part, true);
                KAS_Shared.MovePartWith(portModule.part, allChilds, position, rotation);
            }
            if (IsDownUp(portModule.part))
            {
                KAS_Shared.DebugLog("MoveMoveablePart(Core) - Moveable part is down>up");
                List<Part> allParents = KAS_Shared.GetAllParents(portModule.part, true);
                KAS_Shared.MovePartWith(portModule.part, allParents, position, rotation);
            }
        }

        public void Eject()
        {
            if (headState == PlugState.Locked && ejectEnabled)
            {
                Deploy();
                cableJointLength = maxLenght;
                Vector3 force = winchAnchorNode.TransformDirection(Vector3.forward) * ejectForce;
                if (connectedPortInfo.module)
                {
                    connectedPortInfo.module.part.Rigidbody.AddForce(force, ForceMode.Force);
                }
                else
                {
                    headTransform.rigidbody.AddForce(force, ForceMode.Force);
                }
                this.part.Rigidbody.AddForce(-force, ForceMode.Force);
                fxSndEject.audio.Play();
            }
        }

        public bool IsUpDown(Part refPart)
        {
            //use new system 
            //this.part.hasIndirectChild(this.part.localRoot)
            //this.part.hasIndirectParent(this.part.localRoot)
            if (refPart.parent == this.part) return true;
            return false;
        }

        public bool IsDownUp(Part refPart)
        {
            if (this.part.parent == refPart) return true;
            return false;
        }

        private bool IsLockable()
        {
            float distance = Vector3.Distance(winchAnchorNode.position, headAnchorNode.position);
            if (distance > lockMinDist)
            {
                KAS_Shared.DebugLog("CanLock(Winch) - Can't lock, distance is : " + distance + " and lockMinDist set to : " + lockMinDist);
                return false;
            }
            float fwdDot = Mathf.Abs(Vector3.Dot(winchAnchorNode.forward, headAnchorNode.forward));
            if (fwdDot <= lockMinFwdDot)
            {
                KAS_Shared.DebugLog("CanLock(Winch) - Can't lock, forward dot is : " + fwdDot + " and lockMinFwdDot set to : " + lockMinFwdDot);
                return false;
            }
            float rollDot = Vector3.Dot(winchAnchorNode.up, headAnchorNode.up);
            if (rollDot <= float.MinValue)
            {
                KAS_Shared.DebugLog("CanLock(Winch) - Can't lock, roll dot is : " + rollDot + " and lockMinRollDot set to : " + float.MinValue);
                return false;
            }
            return true;
        }

        public KASModuleMagnet GetHookMagnet()
        {
            if (connectedPortInfo.module)
            {
                return connectedPortInfo.module.GetComponent<KASModuleMagnet>();
            }
            return null;
        }

        public KASModuleSuctionCup GetHookSuction()
        {
            if (connectedPortInfo.module)
            {
                return connectedPortInfo.module.GetComponent<KASModuleSuctionCup>();
            }
            return null;
        }

        public KASModuleGrapplingHook GetHookGrapple()
        {
            if (connectedPortInfo.module)
            {
                return connectedPortInfo.module.GetComponent<KASModuleGrapplingHook>();
            }
            return null;
        }

        public void RefreshControlState()
        {
            if (controlActivated)
            {
                if (controlInverted) controlField = "Enabled(Inverted)";
                else controlField = "Enabled";
            }
            else
            {
                if (controlInverted) controlField = "Disabled(Inverted)";
                else controlField = "Disabled";
            }
        }

        [KSPEvent(name = "ContextMenuToggleControl", active = true, guiActive = true, guiName = "Winch: Toggle Control")]
        public void ContextMenuToggleControl()
        {
            controlActivated = !controlActivated;
            RefreshControlState();
        }

        [KSPEvent(name = "ContextMenuInvertControl", active = true, guiActive = true, guiName = "Winch: Invert control")]
        public void ContextMenuInvertControl()
        {
            controlInverted = !controlInverted;
            RefreshControlState();
        }

        [KSPEvent(name = "ContextMenuGUI", active = true, guiActive = true, guiName = "Show GUI")]
        public void ContextMenuGUI()
        {
            KASAddonWinchGUI.ToggleGUI();
        }

        [KSPEvent(name = "ContextMenuPlugMode", active = true, guiActive = true, guiName = "Plug Mode")]
        public void ContextMenuPlugMode()
        {
            TogglePlugMode();
        }

        [KSPEvent(name = "ContextMenuUnplug", active = true, guiActive = true, guiName = "Unplug")]
        public void ContextMenuUnplug()
        {
            if (headState == PlugState.PlugDocked || headState == PlugState.PlugUndocked)
            {
                UnplugHead();
            }
        }

        [KSPEvent(name = "ContextMenuCableStretch", active = true, guiActive = true, guiName = "Instant Stretch")]
        public void ContextMenuCableStretch()
        {
            if (headState != PlugState.Locked)
            {
                cableJointLength = cableRealLenght;
            }
        }

        [KSPEvent(name = "ContextMenuEject", active = true, guiActive = true, guiName = "Eject")]
        public void ContextMenuEject()
        {
            Eject();
        }

        [KSPEvent(name = "ContextMenuRelease", active = true, guiActive = true, guiName = "Release")]
        public void ContextMenuRelease()
        {
            release.active = !release.active;
        }

        [KSPEvent(name = "ContextMenuRetract", active = true, guiActive = true, guiName = "Retract")]
        public void ContextMenuRetract()
        {
            retract.active = !retract.active;
        }

        [KSPEvent(name = "ContextMenuExtend", active = true, guiActive = true, guiName = "Extend")]
        public void ContextMenuExtend()
        {
            extend.active = !extend.active;
        }

        [KSPEvent(name = "ContextMenuGrabHead", active = true, guiActive = false, guiActiveUnfocused = true, guiName = "Grab connector")]
        public void ContextMenuGrabHead()
        {
            if (headState == PlugState.Locked && nodeConnectedPart)
            {
                ScreenMessages.PostScreenMessage("Can't grab a connector locked with a part !", 5, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (headState != PlugState.Locked)
            {
                ScreenMessages.PostScreenMessage("Can't grab a connector already deployed !", 5, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            GrabHead(FlightGlobals.ActiveVessel);
        }

        [KSPEvent(name = "ContextMenuLockHead", active = true, guiActive = false, guiActiveUnfocused = true, guiName = "Lock connector")]
        public void ContextMenuLockHead()
        {
            LockHead();
        }


        [KSPAction("Eject hook", actionGroup = KSPActionGroup.None)]
        public void ActionGroupEject(KSPActionParam param)
        {
            Eject();
        }

        [KSPAction("Release cable", actionGroup = KSPActionGroup.None)]
        public void ActionGroupRelease(KSPActionParam param)
        {
            if (!this.part.packed)
            {
                ContextMenuRelease();
            }
        }

        [KSPAction("Retract cable", actionGroup = KSPActionGroup.None)]
        public void ActionGroupRetract(KSPActionParam param)
        {
            if (!this.part.packed)
            {
                ContextMenuRetract();
            }
        }

        [KSPAction("Extend cable", actionGroup = KSPActionGroup.None)]
        public void ActionGroupExtend(KSPActionParam param)
        {
            if (!this.part.packed)
            {
                ContextMenuExtend();
            }
        }

        [KSPAction("Plug Mode", actionGroup = KSPActionGroup.None)]
        public void ActionGroupPlugMode(KSPActionParam param)
        {
            TogglePlugMode();
        }

        [KSPAction("Unplug", actionGroup = KSPActionGroup.None)]
        public void ActionGroupUnplug(KSPActionParam param)
        {
            if (!this.part.packed)
            {
                if (headState == PlugState.PlugDocked || headState == PlugState.PlugUndocked)
                {
                    UnplugHead();
                }
            }
        }

        [KSPAction("Toggle key control", actionGroup = KSPActionGroup.None)]
        public void ActionGroupToggleKeyControl(KSPActionParam param)
        {
            controlActivated = !controlActivated;
            RefreshControlState();
        }

        [KSPAction("Enable key control", actionGroup = KSPActionGroup.None)]
        public void ActionGroupEnableKeyControl(KSPActionParam param)
        {
            controlActivated = true;
            RefreshControlState();
        }

        [KSPAction("Disable key control", actionGroup = KSPActionGroup.None)]
        public void ActionGroupDisableKeyControl(KSPActionParam param)
        {
            controlActivated = false;
            RefreshControlState();
        }

        [KSPAction("Toggle inverted key", actionGroup = KSPActionGroup.None)]
        public void ActionGroupToggleInvertedKeyControl(KSPActionParam param)
        {
            controlInverted = !controlInverted;
            RefreshControlState();
        }

        [KSPAction("Enable inverted key", actionGroup = KSPActionGroup.None)]
        public void ActionGroupEnableInvertedKeyControl(KSPActionParam param)
        {
            controlInverted = true;
            RefreshControlState();
        }

        [KSPAction("Disable inverted key", actionGroup = KSPActionGroup.None)]
        public void ActionGroupDisableInvertedKeyControl(KSPActionParam param)
        {
            controlInverted = false;
            RefreshControlState();
        }


        // Key control event
        public void EventWinchExtend(bool activated)
        {
            if (!controlActivated) return;
            if (controlInverted) retract.active = activated;
            else extend.active = activated;
        }

        public void EventWinchRetract(bool activated)
        {
            if (!controlActivated) return;
            if (controlInverted) extend.active = activated;
            else retract.active = activated;
        }
   
        public void EventWinchHeadLeft()
        {
            if (connectedPortInfo.module)
            {
                connectedPortInfo.module.TurnLeft();
            }         
        }

        public void EventWinchHeadRight()
        {
            if (!controlActivated || headState == PlugState.Locked) return;
            if (connectedPortInfo.module)
            {
                connectedPortInfo.module.TurnRight();
            }  
        }

        public void EventWinchEject()
        {
            Eject();
        }

        public void EventWinchHook()
        {
            if (GetHookMagnet())
            {
                GetHookMagnet().ContextMenuMagnet();
            }
            if (GetHookGrapple())
            {
                GetHookGrapple().ContextMenuDetach();
            }
            if (GetHookSuction())
            {
                GetHookSuction().ContextMenuDetach();
            }
        }
    }
}