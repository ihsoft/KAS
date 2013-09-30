using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KAS
{
    public class KASModuleAnchor : PartModule
    {
        [KSPField] public float groundDrag = 100;
        [KSPField] public float bounciness = 0.0f;
        [KSPField] public float dynamicFriction = 0.8f;
        [KSPField] public float staticFriction = 0.8f;

        private bool groundHit = false;
        private float orgAngularDrag;
        private float orgMaximum_drag;
        private float orgMinimum_drag;

        private float orgBounciness;
        private float orgDynamicFriction;
        private float orgStaticFriction;
        PhysicMaterialCombine orgFrictionCombine;

        public override string GetInfo()
        {
            string info = base.GetInfo();
            info += "---- Anchor ----";
            info += "\n";
            info += "Ground drag : " + groundDrag;
            info += "\n";
            info += "Bounciness : " + bounciness;
            info += "\n";
            info += "Dynamic friction: " + dynamicFriction;
            info += "\n";
            info += "Static friction : " + staticFriction;
            return info;
        }

        public void OnPartUnpack()
        {
            //Remove part Buoyancy if any
            PartBuoyancy partB = base.GetComponent<PartBuoyancy>();
            if (partB)
            {
                Destroy(partB);
            }
        }

        void Update()
        {
            base.OnUpdate();
            if (!HighLogic.LoadedSceneIsFlight) return;
            UpdateGroundContact();
        }

        private void UpdateGroundContact()
        {
            if (this.part.GroundContact)
            {
                if (!groundHit)
                {
                    KAS_Shared.DebugLog("UpdateGroundContact(Anchor) - Part hit ground ! Set part friction to : " + dynamicFriction);
                    orgBounciness = this.part.collider.material.bounciness;
                    orgDynamicFriction = this.part.collider.material.dynamicFriction;
                    orgStaticFriction = this.part.collider.material.staticFriction;
                    orgFrictionCombine = this.part.collider.material.frictionCombine;
                    this.part.collider.material.bounciness = bounciness;
                    this.part.collider.material.dynamicFriction = dynamicFriction;
                    this.part.collider.material.staticFriction = staticFriction;
                    this.part.collider.material.frictionCombine = PhysicMaterialCombine.Maximum;

                    KAS_Shared.DebugLog("UpdateGroundContact(Anchor) - Set part drag to : " + groundDrag);
                    orgAngularDrag = this.part.angularDrag;
                    orgMaximum_drag = this.part.maximum_drag;
                    orgMinimum_drag = this.part.minimum_drag;
                    this.part.angularDrag = groundDrag;
                    this.part.maximum_drag = groundDrag;
                    this.part.minimum_drag = groundDrag;
    
                    groundHit = true;
                }
            }
            else
            {
                if (groundHit)
                {
                    KAS_Shared.DebugLog("UpdateGroundContact(Anchor) - Part hit ground ! Set part material to (Bou,dfrict,sfrict,combine) : " + orgBounciness + " | " + orgDynamicFriction + " | " + orgStaticFriction + " | " + orgFrictionCombine);
                    this.part.collider.material.bounciness = orgBounciness;
                    this.part.collider.material.dynamicFriction = orgDynamicFriction;
                    this.part.collider.material.staticFriction = orgStaticFriction;
                    this.part.collider.material.frictionCombine = orgFrictionCombine;
           
                    KAS_Shared.DebugLog("UpdateGroundContact(Anchor) - Set part drag to (Ang,max,min) : " + orgAngularDrag + " | " + orgMaximum_drag + " | " + orgMinimum_drag);
                    this.part.angularDrag = orgAngularDrag;
                    this.part.maximum_drag = orgMaximum_drag;
                    this.part.minimum_drag = orgMinimum_drag;
                    
                    groundHit = false;
                }
            }
        }
    }
}
