using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine;

namespace KAS
{
    public class KASModuleRotor : PartModule
    {
        [KSPField] public float powerDrain = 5f;
        [KSPField] public float breakForce = 20;
        [KSPField] public string rotorTransformName = null;
        [KSPField] public Vector3 axis = new Vector3(0f, -1f, 0f);
        [KSPField] public float speed = 30;
        [KSPField] public float spring = 2;
        [KSPField] public float damper = 1;
        [KSPField] public float force = 1;
        [KSPField] public float rotorMass = 0.2f;
        [KSPField] public bool hasLimit = false;
        [KSPField] public float limitMin = -90;
        [KSPField] public float limitMinBounce = 1;
        [KSPField] public float limitMax = 90;
        [KSPField] public float limitMaxBounce = 1;
        [KSPField] public float stopOffset = 0.01f;
        [KSPField] public float goToMinAngle = 0.5f;
        [KSPField] public float goToMinSpeed = 0.01f;
        [KSPField] public bool freeSpin = false;
        [KSPField] public string negativeWayText = "Left";
        [KSPField] public string positiveWayText = "Right";

        //Sounds
        [KSPField] public string motorStartSndPath = "KAS/Sounds/rotorMotorstart";
        [KSPField] public string motorSndPath = "KAS/Sounds/rotorMotor";
        [KSPField] public string motorStopSndPath = "KAS/Sounds/rotorMotorstop";
        public FXGroup fxSndMotorStart, fxSndMotor, fxSndMotorStop;

        //Gui
        [KSPField(guiActive = true, guiName = "Key control", guiFormat="S")] public string controlField = "";
        [KSPField(guiActive = true, guiName = "State", guiFormat="S")] public string stateField = "Idle";
        [KSPField(guiActive = true, guiName = "Angle", guiFormat = "F4", guiUnits = "°")] public float currentAngle = 0;
        [KSPField(guiActive = true, guiName = "Speed", guiFormat = "F4", guiUnits = "")] public float currentSpeed = 0;

        //Misc
        [KSPField(isPersistant = true)] private bool controlActivated = true;
        [KSPField(isPersistant = true)] private bool controlInverted = false;
        [KSPField(isPersistant = true)] bool firstLoad = true;
        [KSPField(isPersistant = true)] Vector3 rotorLocalPos;
        [KSPField(isPersistant = true)] Quaternion rotorLocalRot;

        private List<AttachNode> attachNodes = new List<AttachNode>();

        private float orgRotorMass;
        private Vector3 rotorOrgLocalPos;
        private Quaternion rotorOrgLocalRot;
        public List<FixedJoint> fixedJnts = new List<FixedJoint>();

        private KASModulePhysicChild rotorPhysicModule;
        private bool rotorActivated = false;
        private bool rotorLoaded = false;
        private bool rotorGoingTo = false;
        public Transform rotorTransform;
        public HingeJoint hingeJnt;

        public enum motorWay
        {
            Negative = 0,
            Positive = 1,
        }

        public override string GetInfo()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("<b>Speed</b>: {0:F1}", speed); sb.AppendLine();
            sb.AppendFormat("<b>Torque</b>: {0:F1}", force); sb.AppendLine();
            if (hasLimit)
            {
                sb.AppendFormat("<b>Minimum angle</b>: {0:F0}", limitMin); sb.AppendLine();
                sb.AppendFormat("<b>Maximum angle</b>: {0:F0}", limitMax); sb.AppendLine();
            }
            sb.AppendFormat("<b>Power consumption</b>: {0:F1}/s", powerDrain); sb.AppendLine();
            return sb.ToString();
        }
       
        public override void OnStart(StartState state)
        {
            KAS_Shared.DebugLog("OnStart(Rotor)");
            //Do not start from editor and at KSP first loading
            if (state == StartState.Editor || state == StartState.None) return;

            Events["ContextMenuMotorNegative"].guiName = negativeWayText + " (" + KASAddonControlKey.rotorNegativeKey + ")";
            Events["ContextMenuMotorPositive"].guiName = positiveWayText + " (" + KASAddonControlKey.rotorPositiveKey + ")";
            Actions["ActionGroupMotorNegative"].guiName = negativeWayText;
            Actions["ActionGroupMotorPositive"].guiName = positiveWayText;

            KAS_Shared.DebugLog("Loading rotor sounds...");
            KAS_Shared.createFXSound(this.part, fxSndMotorStart, motorStartSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndMotor, motorSndPath, true);
            KAS_Shared.createFXSound(this.part, fxSndMotorStop, motorStopSndPath, false);
        }

        public void OnPartUnpack()
        {
            if (!rotorLoaded) LoadRotor();
        }

        void Update()
        {
            base.OnUpdate();
            if (!HighLogic.LoadedSceneIsFlight) return;
            UpdateRotorPos();
            UpdateOrgPos();
        }
        
        private void UpdateRotorPos()
        {
            if (!hingeJnt) return;
            currentAngle = hingeJnt.angle;
            currentSpeed = rotorTransform.rigidbody.angularVelocity.magnitude;
            rotorLocalPos = KAS_Shared.GetLocalPosFrom(rotorTransform.transform, this.part.transform);
            rotorLocalRot = KAS_Shared.GetLocalRotFrom(rotorTransform.transform, this.part.transform);
            if (rotorGoingTo)
            {
                float angleDif = Math.Abs(hingeJnt.spring.targetPosition - currentAngle);
                if (angleDif < goToMinAngle && currentSpeed < goToMinSpeed)
                {
                    MotorStop();
                }
            }
        }
        
        private void UpdateOrgPos()
        {
            if (rotorLoaded)
            {
                if (this.part.hasIndirectParent(this.part.localRoot))
                {
                    KAS_Shared.UpdateChildsOrgPos(this.part, false);
                }
                if (this.part.hasIndirectChild(this.part.localRoot))
                {
                    KAS_Shared.UpdateChildsOrgPos(this.part, true);
                }
            }
        }

        public void LoadRotor()
        {
            KAS_Shared.DebugLog("LoadRotor(Rotor) - Find rotor transform...");
            rotorTransform = this.part.FindModelTransform(rotorTransformName);

            KAS_Shared.DebugLog("LoadRotor(Rotor) - Create physical object...");
            rotorPhysicModule = this.part.gameObject.GetComponent<KASModulePhysicChild>();
            if (!rotorPhysicModule)
            {
                KAS_Shared.DebugLog("LoadRotor(Rotor) - KASModulePhysicChild do not exist, adding it...");
                rotorPhysicModule = this.part.gameObject.AddComponent<KASModulePhysicChild>();
            }
            rotorPhysicModule.mass = rotorMass;
            rotorPhysicModule.physicObj = rotorTransform.gameObject;
            rotorPhysicModule.Start();

            orgRotorMass = this.part.mass;
            float newMass = this.part.mass - rotorMass;
            if (newMass > 0)
            {
                this.part.mass = newMass;
            }
            else
            {
                KAS_Shared.DebugWarning("LoadRotor(Rotor) - Mass of the rotor is greater than the part !");
            }

            KAS_Shared.DebugLog("LoadRotor - Save original rotor position...");
            rotorOrgLocalPos = KAS_Shared.GetLocalPosFrom(rotorTransform, this.part.transform);
            rotorOrgLocalRot = KAS_Shared.GetLocalRotFrom(rotorTransform, this.part.transform);

            KAS_Shared.DebugLog("LoadRotor - Disable collision...");
            if (rotorTransform.collider)
            {
                KAS_Shared.DisableVesselCollision(this.part.vessel, rotorTransform.collider);
            }
            else
            {
                KAS_Shared.DebugError("LoadRotor - Rotor transform do not have any collider !");
            }

            KAS_Shared.DebugLog("LoadRotor - Create hinge joint...");
            hingeJnt = this.part.gameObject.AddComponent<HingeJoint>();
            hingeJnt.connectedBody = rotorTransform.rigidbody;
            ResetLimitsConfig();
            ResetMotorConfig();
            ResetSpringConfig();
            ResetJointConfig();

            if (firstLoad)
            {
                firstLoad = false;
            }
            else
            {
                KAS_Shared.DebugLog("LoadRotor - Return rotor to the current position and rotation : " + rotorLocalPos + " | " + rotorLocalRot);
                KAS_Shared.SetPartLocalPosRotFrom(rotorTransform.transform, this.part.transform, rotorLocalPos, rotorLocalRot);
            }

            // Get rotor attach nodes
            attachNodes.Clear();
            ConfigNode node = KAS_Shared.GetBaseConfigNode(this);
            List<string> attachNodesSt = new List<string>(node.GetValues("attachNode"));
            foreach (String anString in attachNodesSt)
            {
                AttachNode an = this.part.findAttachNode(anString);
                if (an != null)
                {
                    KAS_Shared.DebugLog("LoadRotor - Rotor attach node added : " + an.id);
                    attachNodes.Add(an);
                    if (an.attachedPart)
                    {
                        KAS_Shared.DebugLog("LoadRotor - Setting rotor joint on : " + an.attachedPart.partInfo.title);
                        KAS_Shared.RemoveFixedJointBetween(this.part, an.attachedPart);
                        KAS_Shared.RemoveHingeJointBetween(this.part, an.attachedPart);
                        FixedJoint fjnt = an.attachedPart.gameObject.AddComponent<FixedJoint>();
                        fjnt.connectedBody = rotorTransform.rigidbody;
                        fixedJnts.Add(fjnt);
                    }
                }
            }   
            MotorStop();
            rotorLoaded = true;
        }

        public void ResetJointConfig()
        {
            hingeJnt.anchor = this.part.transform.InverseTransformPoint(rotorTransform.transform.position);
            hingeJnt.axis = axis;
            hingeJnt.breakForce = breakForce;
            hingeJnt.breakTorque = breakForce;
            hingeJnt.useLimits = hasLimit;
            hingeJnt.useMotor = true;
            hingeJnt.useSpring = false;
        }

        public void ResetLimitsConfig()
        {
            JointLimits lmt = new JointLimits();
            lmt.min = limitMin;
            lmt.minBounce = limitMinBounce;
            lmt.max = limitMax;
            lmt.maxBounce = limitMaxBounce;
            hingeJnt.limits = lmt;
        }

        public void ResetMotorConfig()
        {
            JointMotor mtr = new JointMotor();
            mtr.force = force;
            mtr.targetVelocity = 0;
            mtr.freeSpin = freeSpin;
            hingeJnt.motor = mtr;
        }

        public void ResetSpringConfig()
        {
            JointSpring spr = new JointSpring();
            spr.spring = spring;
            spr.damper = damper;
            hingeJnt.spring = spr;
        }

        private void MotorGoTo(float angle)
        {
            KAS_Shared.DebugLog("MotorGoTo(Rotor) - Go to angle : " + angle);
            if (!KAS_Shared.RequestPower(this.part, powerDrain)) return;

            fxSndMotorStart.audio.Play();       
            if (!fxSndMotor.audio.isPlaying) fxSndMotor.audio.Play();
            rotorGoingTo = true;

            hingeJnt.useSpring = true;
            hingeJnt.useMotor = false;
            hingeJnt.useLimits = hasLimit;
            rotorActivated = true;

            JointSpring spr = new JointSpring();
            spr.spring = spring;
            spr.targetPosition = angle;
            spr.damper = damper;
            hingeJnt.spring = spr;
        }

        private void MotorStart(motorWay way)
        {
            KAS_Shared.DebugLog("MotorStart(Rotor) - Start motor...");
            if (!hingeJnt) return;
            if (KAS_Shared.RequestPower(this.part, powerDrain))
            {
                //Sound
                if (hingeJnt.motor.targetVelocity == 0)
                {
                    fxSndMotorStart.audio.Play();
                }
                if (!fxSndMotor.audio.isPlaying) fxSndMotor.audio.Play();
                //Limit config
                ResetLimitsConfig();
                hingeJnt.useLimits = hasLimit;
                JointMotor mtr = new JointMotor();
                //Motor config
                mtr.force = force;
                mtr.freeSpin = freeSpin;
                if (way == motorWay.Negative)
                {
                    if (controlInverted) mtr.targetVelocity = speed;
                    else mtr.targetVelocity = -speed;
                    stateField = "Going " + negativeWayText;
                }
                if (way == motorWay.Positive)
                {
                    if (controlInverted) mtr.targetVelocity = -speed;
                    else mtr.targetVelocity = speed;
                    stateField = "Going " + positiveWayText;
                }
                hingeJnt.motor = mtr;
                //misc
                hingeJnt.useSpring = false;
                hingeJnt.useMotor = true;
                rotorActivated = true;
            }
            else
            {
                if (this.part.vessel == FlightGlobals.ActiveVessel)
                {
                    ScreenMessages.PostScreenMessage(this.part.partInfo.title + " stopped ! Insufficient Power", 5, ScreenMessageStyle.UPPER_CENTER);
                }
                stateField = "Insufficient Power";
            }
        }

        private void MotorRelease()
        {
            KAS_Shared.DebugLog("MotorRelease(Rotor) - Release motor...");
            if (!hingeJnt) return;
            //Limit config
            ResetLimitsConfig();
            hingeJnt.useLimits = hasLimit;
            //Misc
            hingeJnt.useSpring = false;
            hingeJnt.useMotor = false;
            rotorActivated = false;   
        }

        private void MotorStop()
        {
            KAS_Shared.DebugLog("MotorStart(Rotor) - Stop motor...");
            if (!hingeJnt) return;
            //Sound
            if (hingeJnt.motor.targetVelocity != 0)
            {
                fxSndMotorStop.audio.Play();
            }
            if (fxSndMotor.audio.isPlaying) fxSndMotor.audio.Stop();   
            //Motor config
            JointMotor mtr = new JointMotor();
            mtr.force = force;
            mtr.freeSpin = freeSpin;
            mtr.targetVelocity = 0;
            hingeJnt.motor = mtr;
            //Limit config (workaround, motor don't seem to keep position correctly with mass attached)
            JointLimits lmt = new JointLimits();
            lmt.min = hingeJnt.angle - stopOffset;
            lmt.minBounce = 0;
            lmt.max = hingeJnt.angle + stopOffset;
            lmt.maxBounce = 0;
            hingeJnt.limits = lmt;
            //Misc
            hingeJnt.useLimits = true;
            hingeJnt.useSpring = false;
            hingeJnt.useMotor = false;//true
            rotorActivated = false;
            rotorGoingTo = false;
            stateField = "Idle";
        }

        public void RefreshCtrlState()
        {
            KAS_Shared.DebugLog("RefreshCtrlState(Rotor)");
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

        [KSPEvent(name = "ContextMenuToggleControl", active = true, guiActive = true, guiName = "Toggle Control")]
        public void ContextMenuToggleControl()
        {
            controlActivated = !controlActivated;
            RefreshCtrlState();
        }

        [KSPEvent(name = "ContextMenuInvertControl", active = true, guiActive = true, guiName = "Invert control")]
        public void ContextMenuInvertControl()
        {
            controlInverted = !controlInverted;
            RefreshCtrlState();
        }
  
        [KSPEvent(name = "ContextMenuMotorRelease", active = true, guiActive = true, guiName = "Release")]
        public void ContextMenuMotorRelease()
        {
            MotorRelease();
        }

        [KSPEvent(name = "ContextMenuMotorNegative", active = true, guiActive = true, guiName = "Left")]
        public void ContextMenuMotorNegative()
        {
            if (!rotorActivated)
            {
                MotorStart(motorWay.Negative);
            }
            else
            {
                MotorStop();
            }
        }

        [KSPEvent(name = "ContextMenuMotorPositive", active = true, guiActive = true, guiName = "Right")]
        public void ContextMenuMotorPositive()
        {
            if (!rotorActivated)
            {
                MotorStart(motorWay.Positive);
            }
            else
            {
                MotorStop();
            }
        }

        [KSPEvent(name = "ContextMenuMotorGoto0", active = true, guiActive = true, guiName = "Go To 0°")]
        public void ContextMenuMotorGoto0()
        {
            MotorGoTo(0);
        }

        [KSPEvent(name = "ContextMenuMotorGoto90", active = true, guiActive = true, guiName = "Go To 90°")]
        public void ContextMenuMotorGoto90()
        {
            MotorGoTo(90);
        }

        [KSPEvent(name = "ContextMenuMotorGotoM90", active = true, guiActive = true, guiName = "Go To -90°")]
        public void ContextMenuMotorGotoM90()
        {
            MotorGoTo(-90);
        }


        [KSPEvent(name = "ContextMenuMotorGoto180", active = true, guiActive = true, guiName = "Go To 180°")]
        public void ContextMenuMotorGoto180()
        {
            MotorGoTo(180);
        }

        [KSPAction("Left", actionGroup = KSPActionGroup.None)]
        public void ActionGroupMotorNegative(KSPActionParam param)
        {
                ContextMenuMotorNegative();
        }

        [KSPAction("Right", actionGroup = KSPActionGroup.None)]
        public void ActionGroupMotorPositive(KSPActionParam param)
        {
                ContextMenuMotorPositive();
        }

        [KSPAction("Toggle key control", actionGroup = KSPActionGroup.None)]
        public void ActionGroupToggleKeyControl(KSPActionParam param)
        {
            controlActivated = !controlActivated;
            RefreshCtrlState();
        }

        [KSPAction("Enable key control", actionGroup = KSPActionGroup.None)]
        public void ActionGroupEnableKeyControl(KSPActionParam param)
        {
            controlActivated = true;
            RefreshCtrlState();
        }

        [KSPAction("Disable key control", actionGroup = KSPActionGroup.None)]
        public void ActionGroupDisableKeyControl(KSPActionParam param)
        {
            controlActivated = false;
            RefreshCtrlState();
        }

        [KSPAction("Toggle inverted key", actionGroup = KSPActionGroup.None)]
        public void ActionGroupToggleInvertedKeyControl(KSPActionParam param)
        {
            controlInverted = !controlInverted;
            RefreshCtrlState();
        }

        [KSPAction("Enable inverted key", actionGroup = KSPActionGroup.None)]
        public void ActionGroupEnableInvertedKeyControl(KSPActionParam param)
        {
            controlInverted = true;
            RefreshCtrlState();
        }

        [KSPAction("Disable inverted key", actionGroup = KSPActionGroup.None)]
        public void ActionGroupDisableInvertedKeyControl(KSPActionParam param)
        {
            controlInverted = false;
            RefreshCtrlState();
        }

        public void EventRotorNegative(bool activated)
        {
            if (!controlActivated) return;
            if (!activated)
            {
                MotorStop();
                return;
            }
            if (controlInverted) ContextMenuMotorPositive();
            else ContextMenuMotorNegative();
        }

        public void EventRotorPositive(bool activated)
        {
            if (!controlActivated) return;
            if (!activated)
            {
                MotorStop();
                return;
            }
            if (controlInverted) ContextMenuMotorNegative();
            else ContextMenuMotorPositive();
        }
        
    }
}