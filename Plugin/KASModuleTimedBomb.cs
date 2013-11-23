using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KAS
{
    public class KASModuleTimedBomb : PartModule
    {
        [KSPField] public float delay = 5f;
        [KSPField] public float explosionRadius = 10f;
        [KSPField] public string activateText = "Activate";
        [KSPField] public string timeStartSndPath = "KAS/Sounds/timeBombStart";
        [KSPField] public string timeLoopSndPath = "KAS/Sounds/timeBombLoop";
        [KSPField] public string timeEndSndPath = "KAS/Sounds/timeBombStart";

        public FXGroup fxSndTimeStart, fxSndTimeLoop, fxSndTimeEnd;
        private bool activated = false;

        public override string GetInfo()
        {
            string info = base.GetInfo();
            info += "---- Timed Bomb ----";
            info += "\n";
            info += "Delay : " + delay;
            info += "\n";
            info += "Explosion radius : " + explosionRadius;
            return info;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (state == StartState.Editor || state == StartState.None) return;
            Events["Activate"].guiName = activateText;
            KAS_Shared.createFXSound(this.part, fxSndTimeStart, timeStartSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndTimeLoop, timeLoopSndPath, true);
            KAS_Shared.createFXSound(this.part, fxSndTimeEnd, timeEndSndPath, false);
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            if (activated)
            {
                delay += -TimeWarp.deltaTime;
                if (delay < 1)
                {
                    if (!fxSndTimeEnd.audio.isPlaying)
                    {
                        fxSndTimeEnd.audio.Play();
                    }
                }
                if (delay < 0)
                {
                    fxSndTimeStart.audio.Stop();
                    fxSndTimeLoop.audio.Stop();
                    Explode(this.part.transform.position, explosionRadius);
                }
            }
        }

        public void Explode(Vector3 pos, float radius)
        {

            List<Collider> nearestColliders = new List<Collider>(Physics.OverlapSphere(pos, radius, 557059));
            foreach (Collider col in nearestColliders)
            {
                // Check if if the collider have a rigidbody
                if (!col.attachedRigidbody)
                {
                    continue;
                }
                // Check if it's a part
                Part p = col.attachedRigidbody.GetComponent<Part>();
                if (!p)
                {
                    continue;
                }
                p.explosionPotential = radius;
                p.explode();
                p.Die();
            }
        }

        [KSPEvent(name = "Activate", active = true, guiActive = false, guiActiveUnfocused = true, guiName = "Activate")]
        public void Activate()
        {
            if (!activated)
            {
                activated = true;
                fxSndTimeStart.audio.Play();
                fxSndTimeLoop.audio.Play();
                Events["Activate"].guiActiveUnfocused = false;
            }
        }
    }
}
