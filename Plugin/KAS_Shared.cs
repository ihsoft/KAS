using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;

namespace KAS
{
    static public class KAS_Shared
    {
        //Debug log yes/no
        private static bool debugLogActive = true;

        //Shared Struct
        public struct cableControl
        {
            public bool active;
            public bool starting;
            public bool isrunning;
            public bool full;
        }

        private static List<Part> childList;
        private static List<Part> parentList;

        private static Vector3 vectTest = Vector3.forward;
        private static float testDegree = 180;
        private static bool inputLock = false;

        public static void DebugLog(string text)
        {
            if (debugLogActive) Debug.Log("[KAS] " + text);
        }

        public static void DebugLog(string text, UnityEngine.Object context)
        {
            if (debugLogActive) Debug.Log("[KAS] " + text, context);
        }

        public static void DebugWarning(string text)
        {
            if (debugLogActive)
            {
                Debug.LogWarning("[KAS] " + text);
            }
        }

        public static void DebugError(string text)
        {
            if (debugLogActive)
            {
                Debug.LogError("[KAS] " + text);
            }
        }

        public static Vector3 ParseCfgVector3(string vectorString)
        {
            string[] strArray = vectorString.Split(new char[] { '(', ',', ' ', ')' }, StringSplitOptions.RemoveEmptyEntries);
            if (strArray.Length == 3)
            {
                try
                {
                    Vector3 parsedVector = new Vector3(float.Parse(strArray[0]), float.Parse(strArray[1]), float.Parse(strArray[2]));
                    return parsedVector;
                }
                catch (Exception exception)
                {
                    PDebug.Error(exception.Message);
                }
            }
            return new Vector3(0, 0, 0);
        }

        public static void MoveAbove(Transform fromTransform, Vector3 fromLocalPos, Vector3 fromOrientation, RaycastHit hit)
        {
            fromTransform.rotation = Quaternion.FromToRotation(fromOrientation, -hit.normal);
            fromTransform.position = fromTransform.position - (fromTransform.TransformPoint(fromLocalPos) - hit.point);
        }

        public static void MoveAlignLight(Vessel fromVessel, Transform fromTransform, Vessel toVessel, Transform toTransform)
        {
            toVessel.SetRotation(toVessel.transform.rotation);
            fromVessel.SetRotation(Quaternion.FromToRotation(fromTransform.forward, -toTransform.forward) * fromVessel.transform.rotation);
            fromVessel.SetPosition(fromVessel.transform.position - (fromTransform.position - toTransform.position), true);
        }

        public static void MoveAlignLight(Part fromPart, Transform fromTransform, Part toPart, Transform toTransform, List<Part> partToMoveWith = null)
        {
            Quaternion rot = Quaternion.FromToRotation(fromTransform.forward, -toTransform.forward) * fromPart.transform.rotation;
            Vector3 pos = (fromPart.transform.position - (fromTransform.position - toTransform.position));
            if (partToMoveWith == null)
            {
                fromPart.transform.rotation = rot;
                fromPart.transform.position = pos;
            }
            else
            {
                KAS_Shared.MovePartWith(fromPart, partToMoveWith, pos, rot);
            }
        }

        public static void MovePartWith(Part rootPart, List<Part> moveWithParts, Vector3 position, Quaternion rotation)
        {
            Dictionary<Part, Transform> partsParent = new Dictionary<Part, Transform>();

            // Save original parts parent (just in case) and child them to rootPart
            foreach (Part p in moveWithParts)
            {
                if (p == rootPart) continue;
                partsParent.Add(p, p.transform.parent);
                p.transform.parent = rootPart.transform;
            }

            // Move root part
            rootPart.transform.position = position;
            rootPart.transform.rotation = rotation;

            // Reset original parts parent
            foreach (KeyValuePair<Part, Transform> pParent in partsParent)
            {
                pParent.Key.transform.parent = pParent.Value;
            }
        }

        public static void AddNodeTransform(Part p, AttachNode attachNode)
        {
            Quaternion rotation = Quaternion.LookRotation(attachNode.orientation, Vector3.up);

            if (attachNode.nodeType == AttachNode.NodeType.Surface)
            {
                rotation = Quaternion.Inverse(rotation);
            }

            if (attachNode.nodeTransform == null)
            {
                Transform nodeTransform = new GameObject("KASNodeTransf").transform;
                nodeTransform.parent = p.transform;
                nodeTransform.localPosition = attachNode.position;
                nodeTransform.localRotation = rotation;
                attachNode.nodeTransform = nodeTransform;
            }
            else
            {
                attachNode.nodeTransform.localPosition = attachNode.position;
                attachNode.nodeTransform.localRotation = rotation;
                KAS_Shared.DebugLog("AddTransformToAttachNode - Node : " + attachNode.id + " already have a nodeTransform, only update");
            }
        }

        public static Quaternion DirectionToQuaternion(Transform transf, Vector3 nodeDirection)
        {
            Vector3 refDirection = Vector3.up;
            Vector3 alterDirection = Vector3.forward;

            Vector3 nodeDir = transf.TransformDirection(Vector3.Normalize(nodeDirection));
            Vector3 refDir = transf.TransformDirection(refDirection);
            Vector3 alterDir = transf.TransformDirection(alterDirection);

            if (nodeDir == refDir)
            {
                return Quaternion.LookRotation(nodeDir, alterDir);
            }
            if (nodeDir == -refDir)
            {
                return Quaternion.LookRotation(nodeDir, -alterDir);
            }
            if (nodeDir == Vector3.zero)
            {
                return transf.rotation;
            }
            return Quaternion.LookRotation(nodeDir, refDir);
        }

        public static void MoveAlign(Transform source, Transform childNode, RaycastHit hit, Quaternion adjust)
        {
            Vector3 refDirection = Vector3.up;
            Vector3 alterDirection = Vector3.forward;

            Vector3 refDir = hit.transform.TransformDirection(refDirection);
            Vector3 alterDir = hit.transform.TransformDirection(alterDirection);
            Quaternion rotation;

            if (hit.normal == refDir)
            {
                rotation = Quaternion.LookRotation(hit.normal, alterDir);
            }
            else if (hit.normal == -refDir)
            {
                rotation = Quaternion.LookRotation(hit.normal, -alterDir);
            }
            else
            {
                rotation = Quaternion.LookRotation(hit.normal, refDir);
            }

            MoveAlign(source, childNode, hit.point, rotation * adjust);
        }

        public static void MoveAlign(Transform source, Transform childNode, Transform target)
        {
            MoveAlign(source, childNode, target.position, target.rotation);
        }

        public static void MoveAlign(Transform source, Transform childNode, Vector3 targetPos, Quaternion targetRot)
        {
            source.rotation = targetRot * Quaternion.Inverse(childNode.localRotation);
            source.position = source.position - (childNode.position - targetPos);
        }

        public static void MoveRelatedTo(Transform fromTransform, Transform toTransform, Vector3 position, Vector3 direction)
        {
            fromTransform.rotation = toTransform.rotation * Quaternion.Euler(direction);
            fromTransform.position = toTransform.TransformPoint(position);
        }

        public static Collider GetEvaCollider(Vessel evaVessel, string colliderName)
        {
            KerbalEVA kerbalEva = evaVessel.rootPart.gameObject.GetComponent<KerbalEVA>();
            Collider evaCollider = null;
            if (kerbalEva)
            {
                foreach (Collider col in kerbalEva.characterColliders)
                {
                    if (col.name == colliderName)
                    {
                        evaCollider = col;
                        break;
                    }
                }
            }
            return evaCollider;
        }

        public static void CreatePhysicObject(Transform transf, float mass, Rigidbody copyRbVelFrom = null)
        {
            transf.gameObject.AddComponent<Rigidbody>();
            transf.rigidbody.mass = mass;
            transf.transform.parent = null;
            transf.rigidbody.useGravity = true;
            if (copyRbVelFrom)
            {
                transf.rigidbody.velocity = copyRbVelFrom.velocity;
                transf.rigidbody.angularVelocity = copyRbVelFrom.angularVelocity;
            }
            FlightGlobals.addPhysicalObject(transf.gameObject);
        }

        public static void RemovePhysicObject(Part p, Transform transf)
        {
            FlightGlobals.removePhysicalObject(transf.gameObject);
            UnityEngine.Object.Destroy(transf.rigidbody);
            transf.parent = p.transform;
        }

        public static void ResetCollisionEnhancer(Part p, bool create_new = true)
        {
            if (p.collisionEnhancer)
            {
                UnityEngine.Object.DestroyImmediate(p.collisionEnhancer);
            }

            if (create_new)
            {
                p.collisionEnhancer = p.gameObject.AddComponent<CollisionEnhancer>();
            }
        }

        public static List<KASModuleWinch> GetAllWinch(Vessel fromVessel = null)
        {
            List<KASModuleWinch> returnedWinches = new List<KASModuleWinch>();

            List<KASModuleWinch> winches = new List<KASModuleWinch>(GameObject.FindObjectsOfType(typeof(KASModuleWinch)) as KASModuleWinch[]);
            foreach (KASModuleWinch winch in winches)
            {
                if (fromVessel)
                {
                    if (winch.vessel == fromVessel)
                    {
                        returnedWinches.Add(winch);
                    }
                }
                else
                {
                    returnedWinches.Add(winch);
                }
            }
            return returnedWinches;
        }

        public static void SendMsgToWinch(String methodeName, object value = null, Vessel vess = null)
        {
            List<KASModuleWinch> winches = new List<KASModuleWinch>();
            if (vess)
            {
                winches = GetAllWinch(vess);
            }
            else
            {
                winches = GetAllWinch();
            }
            foreach (KASModuleWinch winch in winches)
            {
                if (value != null)
                {
                    winch.SendMessage(methodeName, value, SendMessageOptions.DontRequireReceiver);
                }
                else
                {
                    winch.SendMessage(methodeName, SendMessageOptions.DontRequireReceiver);
                }
            }
        }

        public static List<KASModuleRotor> GetAllRotor(Vessel fromVessel = null)
        {
            List<KASModuleRotor> returnedRotors = new List<KASModuleRotor>();

            List<KASModuleRotor> rotors = new List<KASModuleRotor>(GameObject.FindObjectsOfType(typeof(KASModuleRotor)) as KASModuleRotor[]);
            foreach (KASModuleRotor rotor in rotors)
            {
                if (fromVessel)
                {
                    if (rotor.vessel == fromVessel)
                    {
                        returnedRotors.Add(rotor);
                    }
                }
                else
                {
                    returnedRotors.Add(rotor);
                }
            }
            return returnedRotors;
        }

        public static void SendMsgToRotor(String methodeName, object value = null, Vessel vess = null)
        {
            List<KASModuleRotor> rotors = new List<KASModuleRotor>();
            if (vess)
            {
                rotors = GetAllRotor(vess);
            }
            else
            {
                rotors = GetAllRotor();
            }
            foreach (KASModuleRotor rotor in rotors)
            {
                if (value != null)
                {
                    rotor.SendMessage(methodeName, value, SendMessageOptions.DontRequireReceiver);
                }
                else
                {
                    rotor.SendMessage(methodeName, SendMessageOptions.DontRequireReceiver);
                }
            }
        }

        public static List<KASModuleTelescopicArm> GetAllTelescopicArm(Vessel fromVessel = null)
        {
            List<KASModuleTelescopicArm> returnedTelescopicArms = new List<KASModuleTelescopicArm>();

            List<KASModuleTelescopicArm> telescopicArms = new List<KASModuleTelescopicArm>(GameObject.FindObjectsOfType(typeof(KASModuleTelescopicArm)) as KASModuleTelescopicArm[]);
            foreach (KASModuleTelescopicArm telescopicArm in telescopicArms)
            {
                if (fromVessel)
                {
                    if (telescopicArm.vessel == fromVessel)
                    {
                        returnedTelescopicArms.Add(telescopicArm);
                    }
                }
                else
                {
                    returnedTelescopicArms.Add(telescopicArm);
                }
            }
            return returnedTelescopicArms;
        }

        public static void SendMsgToTelescopicArm(String methodeName, object value = null, Vessel vess = null)
        {
            List<KASModuleTelescopicArm> telescopicArms = new List<KASModuleTelescopicArm>();
            if (vess)
            {
                telescopicArms = GetAllTelescopicArm(vess);
            }
            else
            {
                telescopicArms = GetAllTelescopicArm();
            }
            foreach (KASModuleTelescopicArm telescopicArm in telescopicArms)
            {
                if (value != null)
                {
                    telescopicArm.SendMessage(methodeName, value, SendMessageOptions.DontRequireReceiver);
                }
                else
                {
                    telescopicArm.SendMessage(methodeName, SendMessageOptions.DontRequireReceiver);
                }
            }
        }


        public static Part GetPartUnderCursor()
        {
            Ray ray;
            if (HighLogic.LoadedSceneIsFlight)
            {
                ray = FlightCamera.fetch.mainCamera.ScreenPointToRay(Input.mousePosition);
            }
            else
            {
                ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            }
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 557059))
            {
                return hit.transform.gameObject.GetComponent<Part>();
            }
            else
            {
                return null;
            }
        }

        public static Transform GetTransformUnderCursor()
        {
            Ray ray;
            if (HighLogic.LoadedSceneIsFlight)
            {
                ray = FlightCamera.fetch.mainCamera.ScreenPointToRay(Input.mousePosition);
            }
            else
            {
                ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            }
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 557059))
            {
                return hit.transform;
            }
            else
            {
                return null;
            }
        }


        public static KerbalEVA GetKerbalEvaUnderCursor()
        {
            Ray ray;
            if (HighLogic.LoadedSceneIsFlight)
            {
                ray = FlightCamera.fetch.mainCamera.ScreenPointToRay(Input.mousePosition);
            }
            else
            {
                ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            }
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 557059))
            {
                return hit.transform.gameObject.GetComponent<KerbalEVA>();
            }
            else
            {
                return null;
            }
        }

        public static void DisableEditorClickthrough(Rect guiWindowRect)
        {
            if (MouseIsOverWindow(guiWindowRect) && !inputLock)
            {
                EditorLogic.fetch.Lock(true, true, true, "KAS DisableEditorClickthrough");
                inputLock = true;
            }
            if (!MouseIsOverWindow(guiWindowRect) && inputLock)
            {
                EditorLogic.fetch.Unlock("KAS DisableEditorClickthrough");
                inputLock = false;

            }
        }

        public static bool MouseIsOverWindow(Rect guiWindowRect)
        {
            //if (showGUI != guiMode.None && guiWindowRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
            if (guiWindowRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
            {
                return true;
            }
            return false;
        }

        public static Part CreatePart(string partname, Vector3 position, Quaternion rotation, Part flagFromPart)
        {
            AvailablePart avPart = PartLoader.getPartInfoByName(partname);
            return CreatePart(avPart, position, rotation, flagFromPart);
        }

        public static Part CreatePart(AvailablePart avPart, Vector3 position, Quaternion rotation, Part flagFromPart)
        {
            UnityEngine.Object obj = UnityEngine.Object.Instantiate(avPart.partPrefab);
            if (!obj)
            {
                KAS_Shared.DebugError("CreatePart(Crate) Failed to instantiate " + avPart.partPrefab.name);
                return null;
            }

            Part newPart = (Part)obj;
            newPart.gameObject.SetActive(true);
            newPart.gameObject.name = avPart.name;
            newPart.partInfo = avPart;
            //newPart.highlightRecurse = true;
            newPart.SetMirror(Vector3.one);

            ShipConstruct newShip = new ShipConstruct();
            newShip.Add(newPart);
            newShip.SaveShip();
            newShip.shipName = avPart.title;
            //newShip.ty = 1;

            VesselCrewManifest vessCrewManifest = new VesselCrewManifest();
            Vessel currentVessel = FlightGlobals.ActiveVessel;

            Vessel v = newShip.parts[0].localRoot.gameObject.AddComponent<Vessel>();
            v.id = Guid.NewGuid();
            v.vesselName = newShip.shipName;
            v.Initialize(false);
            v.Landed = true;
            v.rootPart.flightID = ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState);
            v.rootPart.missionID = flagFromPart.missionID;
            v.rootPart.flagURL = flagFromPart.flagURL;

            //v.rootPart.collider.isTrigger = true;

            //v.landedAt = "somewhere";
                        
            Staging.beginFlight();
            newShip.parts[0].vessel.ResumeStaging();
            Staging.GenerateStagingSequence(newShip.parts[0].localRoot);
            Staging.RecalculateVesselStaging(newShip.parts[0].vessel);

            FlightGlobals.SetActiveVessel(currentVessel);

            v.SetPosition(position);
            v.SetRotation(rotation);

            // Solar panels from containers don't work otherwise
            for (int i = 0; i < newPart.Modules.Count; i++)
            {
                ConfigNode node = new ConfigNode();
                node.AddValue("name", newPart.Modules[i].moduleName);
                newPart.LoadModule(node, ref i);
            }

            return newPart;
        }

        public static ConfigNode SavePartSnapshot(Part part)
        {
            // Seems fine with a null vessel in 0.23 if some empty lists are allocated below
            ProtoPartSnapshot snapshot = new ProtoPartSnapshot(part, null);

            ConfigNode node = new ConfigNode("CONTENT_PART");

            snapshot.attachNodes = new List<AttachNodeSnapshot>();
            snapshot.srfAttachNode = new AttachNodeSnapshot("attach,-1");
            snapshot.symLinks = new List<ProtoPartSnapshot>();
            snapshot.symLinkIdxs = new List<int>();

            snapshot.Save(node);

            float dry_cost, fuel_cost;
            float total_cost = ShipConstruction.GetPartCosts(snapshot, part.partInfo, out dry_cost, out fuel_cost);
            node.AddValue("kas_total_mass", part.mass+part.GetResourceMass());
            node.AddValue("kas_total_cost", total_cost);

            // Prune unimportant data
            node.RemoveValues("parent");
            node.RemoveValues("position");
            node.RemoveValues("rotation");
            node.RemoveValues("istg");
            node.RemoveValues("dstg");
            node.RemoveValues("sqor");
            node.RemoveValues("sidx");
            node.RemoveValues("attm");
            node.RemoveValues("srfN");
            node.RemoveValues("attN");
            node.RemoveValues("connected");
            node.RemoveValues("attached");
            node.RemoveValues("flag");

            node.RemoveNodes("ACTIONS");

            // Remove modules that are not in prefab since they won't load anyway
            var module_nodes = node.GetNodes("MODULE");
            var prefab_modules = part.partInfo.partPrefab.GetComponents<PartModule>();

            node.RemoveNodes("MODULE");

            for (int i = 0; i < prefab_modules.Length && i < module_nodes.Length; i++)
            {
                var module = module_nodes[i];
                var name = module.GetValue("name") ?? "";

                node.AddNode(module);

                if (name == "KASModuleContainer")
                {
                    // Containers get to keep their contents
                    module.RemoveNodes("EVENTS");
                }
                else if (name.StartsWith("KASModule"))
                {
                    // Prune the state of the KAS modules completely
                    module.ClearData();
                    module.AddValue("name", name);
                    continue;
                }

                module.RemoveNodes("ACTIONS");
            }

            return node;
        }

        public struct RigidbodyInertia
        {
            public float mass;
            public Vector3 CoM, tensor;
            public Quaternion tensorRotation;

            public RigidbodyInertia(Rigidbody rb)
            {
                mass = rb.mass;
                CoM = rb.centerOfMass;
                tensor = rb.inertiaTensor;
                tensorRotation = rb.inertiaTensorRotation;
            }
            public void Restore(Rigidbody rb)
            {
                rb.mass = mass;
                rb.centerOfMass = CoM;
                rb.inertiaTensor = tensor;
                rb.inertiaTensorRotation = tensorRotation;
            }
        }

        public static ProtoPartSnapshot LoadProtoPartSnapshot(ConfigNode node)
        {
            ConfigNode node_copy = new ConfigNode();
            node.CopyTo(node_copy);

            node_copy.RemoveValues("kas_total_mass");
            node_copy.RemoveValues("kas_total_cost");

            return new ProtoPartSnapshot(node_copy, null, HighLogic.CurrentGame);
        }

        public static Part LoadPartSnapshot(Vessel vessel, ConfigNode node, Vector3 position, Quaternion rotation)
        {
            ProtoPartSnapshot snapshot = KAS_Shared.LoadProtoPartSnapshot(node);

            if (HighLogic.CurrentGame.flightState.ContainsFlightID(snapshot.flightID))
                snapshot.flightID = ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState);

            snapshot.parentIdx = 0;
            snapshot.position = position;
            snapshot.rotation = rotation;
            snapshot.stageIndex = 0;
            snapshot.defaultInverseStage = 0;
            snapshot.seqOverride = -1;
            snapshot.inStageIndex = -1;
            snapshot.attachMode = (int)AttachModes.SRF_ATTACH;
            snapshot.attached = true;
            snapshot.connected = true;
            snapshot.flagURL = vessel.rootPart.flagURL;

            // Save properties that may be messed up by new colliders
            RigidbodyInertia rb_backup = new RigidbodyInertia(vessel.rootPart.rb);

            Part new_part = snapshot.Load(vessel, false);

            vessel.Parts.Add(new_part);

            if (vessel.packed)
            {
                GameEvents.onVesselWasModified.Fire(vessel);
            }
            else
            {
                // Request initialization as nonphysical to prevent explosions
                new_part.physicalSignificance = Part.PhysicalSignificance.NONE;

                // Disable all sub-objects with colliders
                List<Collider> re_enable = new List<Collider>();

                foreach (var collider in new_part.GetComponentsInChildren<Collider>())
                {
                    if (collider.gameObject.activeSelf)
                    {
                        re_enable.Add(collider);
                        collider.gameObject.SetActive(false);
                    }
                }

                new_part.StartCoroutine(WaitAndUnpack(new_part, re_enable));
            }

            rb_backup.Restore(vessel.rootPart.rb);

            return new_part;
        }

        private static void FinishDelayedCreation(Part part, List<Collider> re_enable)
        {
            // Dissociate from parent and restore colliders
            part.transform.parent = null;

            foreach (var collider in re_enable)
            {
                collider.gameObject.SetActive(true);
            }

            // Create the rigid body
            part.PromoteToPhysicalPart();
            part.rb.mass = part.mass + part.GetResourceMass();
        }

        private static IEnumerator<YieldInstruction> WaitAndUnpack(Part part, List<Collider> re_enable)
        {
            while (!part.started && part.State != PartStates.DEAD)
            {
                yield return null;
            }

            if (part.vessel && part.State != PartStates.DEAD)
            {
                FinishDelayedCreation(part, re_enable);

                if (part.packed && !part.vessel.packed)
                {
                    part.Unpack();
                    part.InitializeModules();
                    part.ResumeVelocity();
                }

                GameEvents.onVesselWasModified.Fire(part.vessel);
            }
        }

        public static ConfigNode GetBaseConfigNode(PartModule partModule)
        {
            UrlDir.UrlConfig pConfig = null;
            foreach (UrlDir.UrlConfig uc in GameDatabase.Instance.GetConfigs("PART"))
            {
                if (uc.name.Replace('_', '.') == partModule.part.partInfo.name)
                {
                    pConfig = uc;
                    break;
                }
            }
            if (pConfig != null)
            {
                foreach (ConfigNode cn in pConfig.config.GetNodes("MODULE"))
                {
                    if (cn.GetValue("name") == partModule.moduleName)
                    {
                        return cn;
                    }
                }
            }
            return null;
        }

        public static float GetAngleFromDirAndPoints(Vector3 dir, Vector3 srcPos, Vector3 tgtPos)
        {
            Vector3 targetDir = tgtPos - srcPos;
            targetDir = targetDir.normalized;
            float dot = Vector3.Dot(targetDir, dir);
            float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
            return angle;   
        }

        public static void DisableVesselCollision(Vessel vess, Collider col)
        {
            foreach (Part vp in vess.parts)
            {
                Physics.IgnoreCollision(col, vp.collider, true);
            }
        }

        public static void RemoveAttachJointBetween(Part part1, Part part2)
        {
            if (part1.attachJoint && ((part1.attachJoint.Host == part1 && part1.attachJoint.Target == part2) || (part1.attachJoint.Host == part2 && part1.attachJoint.Target == part1)))
            {
                part1.attachJoint.DestroyJoint();
            }
            if (part2.attachJoint && ((part2.attachJoint.Host == part2 && part2.attachJoint.Target == part1) || (part2.attachJoint.Host == part1 && part2.attachJoint.Target == part2)))
            {
                part2.attachJoint.DestroyJoint();
            }
        }

        public static void RemoveFixedJointBetween(Part part1, Part part2)
        {
            List<FixedJoint> fixedJoints1 = new List<FixedJoint>(part1.GetComponents<FixedJoint>());
            foreach (FixedJoint fj1 in fixedJoints1)
            {
                if (fj1.connectedBody == part2.rigidbody)
                {
                    UnityEngine.Object.Destroy(fj1);
                }
            }
            List<FixedJoint> fixedJoints2 = new List<FixedJoint>(part2.GetComponents<FixedJoint>());
            foreach (FixedJoint fj2 in fixedJoints2)
            {
                if (fj2.connectedBody == part1.rigidbody)
                {
                    UnityEngine.Object.Destroy(fj2);
                }
            }
        }

        public static void RemoveHingeJointBetween(Part part1, Part part2)
        {
            List<HingeJoint> hingeJoints1 = new List<HingeJoint>(part1.GetComponents<HingeJoint>());
            foreach (HingeJoint hj1 in hingeJoints1)
            {
                if (hj1.connectedBody == part2.rigidbody)
                {
                    UnityEngine.Object.Destroy(hj1);
                }
            }
            List<HingeJoint> hingeJoints2 = new List<HingeJoint>(part2.GetComponents<HingeJoint>());
            foreach (HingeJoint hj2 in hingeJoints2)
            {
                if (hj2.connectedBody == part1.rigidbody)
                {
                    UnityEngine.Object.Destroy(hj2);
                }
            }
        }

        public static void ResetChildPartVesselCollision(Part p)
        {
            p.ResetCollisionIgnores();
            List<Part> pcs = KAS.KAS_Shared.GetAllChilds(p);
            foreach (Part pc in pcs)
            {
                pc.ResetCollisionIgnores();
            }
        }

        public static IEnumerator UpdateChildsOrgPosDelayed(Part p, float waitTime)
        {
            yield return new WaitForSeconds(waitTime);
            UpdateChildsOrgPos(p);
        }

        public static void UpdateChildsOrgPos(Part p, bool includeItself = false)
        {
            if (includeItself) p.UpdateOrgPosAndRot(p.localRoot);
            List<Part> pcs = KAS.KAS_Shared.GetAllChilds(p);
            foreach (Part pc in pcs)
            {
                pc.UpdateOrgPosAndRot(pc.localRoot);
            }
        }

        public static void SetAllChildsPartPos(string vesselID, Dictionary<uint, Vector3> partsPos)
        {
            foreach (KeyValuePair<uint, Vector3> partPos in partsPos)
            {
                Part curPart = GetPartByID(vesselID, partPos.Key.ToString());
                SetPartLocalPosFrom(curPart.transform,curPart.localRoot.transform,partPos.Value);
            }
        }

        public static void SetPartsPosition(Part rootPart, List<Part> parts, Vector3 position, bool usePristineCoords)
        { 
            if (!usePristineCoords)
            {
                Vector3 vector = position - rootPart.transform.position;
                foreach (Part p in parts)
                {
                    if (p == rootPart) continue;
                    if (p.physicalSignificance != Part.PhysicalSignificance.FULL) continue;
                    p.transform.position = p.transform.position + vector;            
                }
            }
            else
            {
                foreach (Part p in parts)
                {
                    p.transform.position = position + rootPart.transform.rotation * p.orgPos;
                }
            }
        }

        public static void SetPartsRotation(Part rootPart, List<Part> parts, Quaternion rotation)
        {
            foreach (Part p in parts)
            {
                rootPart.transform.rotation = rotation * rootPart.orgRot;
            }
            SetPartsPosition(rootPart, parts, rootPart.transform.position, true);
        }

        public static void DecoupleFromAll(Part p)
        {
            if (p.parent)
            {
                p.decouple();
            }
            if (p.children.Count != 0)
            {
                KAS_Shared.DecoupleAllChilds(p);
            }
        }

        public static void DecoupleAllChilds(Part p)
        {
            List<Part> partList = new List<Part>();
            foreach (Part pc in p.children)
            {
                partList.Add(pc);
            }
            foreach (Part pc2 in partList)
            {
                if (pc2.parent) pc2.decouple();
            }
        }

        public static KASModuleGrab GetGrabbedPartModule(Vessel evaVessel)
        {
            List<KASModuleGrab> allEvaGrab = new List<KASModuleGrab>(GameObject.FindObjectsOfType(typeof(KASModuleGrab)) as KASModuleGrab[]);
            foreach (KASModuleGrab evaGrab in allEvaGrab)
            {
                if (!evaGrab.grabbed) continue;
                if (!evaGrab.evaHolderPart) continue;
                if (evaGrab.evaHolderPart.vessel == evaVessel)
                {
                    return evaGrab;
                }
            }
            return null;
        }

        public static KASModuleWinch GetWinchModuleGrabbed(Vessel evaVessel)
        {
            List<KASModuleWinch> allWinches = new List<KASModuleWinch>(GameObject.FindObjectsOfType(typeof(KASModuleWinch)) as KASModuleWinch[]);
            foreach (KASModuleWinch winch in allWinches)
            {
                if (!winch.evaHolderPart) continue;
                if (winch.evaHolderPart.vessel == evaVessel)
                {
                    return winch;
                }
            }
            return null;
        }

        public static List<Part> GetAllParents(Part tgtPart, bool addSelf = false)
        {
            if (parentList == null)
            {
                parentList = new List<Part>();
            }
            else
            {
                parentList.Clear();
            }
            if (addSelf) childList.Add(tgtPart);
            GetAllParentsRecursive(tgtPart);
            return parentList;
        }

        private static Part GetAllParentsRecursive(Part p)
        {
            if (p.parent != null)
            {
                parentList.Add(GetAllParentsRecursive(p.parent));
            }
            return p;
        }

        public static List<Part> GetAllChilds(Part tgtPart, bool addSelf = false)
        {
            if (childList == null)
            {
                childList = new List<Part>();
            }
            else
            {
                childList.Clear();
            }
            if (addSelf) childList.Add(tgtPart);
            GetAllChildsRecursive(tgtPart);
            return childList;
        }

        private static Part GetAllChildsRecursive(Part p)
        {
            if (p.children != null || p.children.Count > 0)
            {
                foreach (Part q in p.children)
                {
                    childList.Add(GetAllChildsRecursive(q));
                }
            }
            return p;
        }

        public static Part GetPartByID(string vesselID, string partID)
        {
            Vessel searchVessel = FlightGlobals.Vessels.Find(ves => ves.id.ToString() == vesselID);
            if (searchVessel)
            {
                if (!searchVessel.loaded)
                {
                    KAS_Shared.DebugWarning("GetPartByID - Searched vessel are not loaded, loading it...");
                    searchVessel.Load();
                }
                return GetPartByID(searchVessel, partID);
            }
            else
            {
                KAS_Shared.DebugError("GetPartByID - Searched vessel not found !");
            }
            return null;
        }

        public static Part GetPartByID(Vessel searchVessel, string partID)
        {
            Part searchedPart = searchVessel.Parts.Find(p => p.flightID.ToString() == partID);
            if (searchedPart)
            {
                return searchedPart;
            }
            else
            {
                KAS_Shared.DebugError("GetPartByID - Searched part not found !");
                return null;
            }
        }

        public static KASModuleWinch GetConnectedWinch(Part p)
        {
            KASModulePort portModule = p.GetComponent<KASModulePort>();
            if (!portModule) return null;
            return portModule.winchConnected;
        }

        public static Vessel GetVesselByName(string name)
        {
            Vessel searchVessel = FlightGlobals.Vessels.Find(ves => ves.vesselName == name);
            if (searchVessel)
            {
                return searchVessel;
            }
            KAS_Shared.DebugError("GetVesselByName - Searched vessel not found !");
            return null;
        }

        public static bool RequestPower(Part prt, float power)
        {
            if (TimeWarp.deltaTime != 0)
            {
                float amount = prt.RequestResource("ElectricCharge", power * TimeWarp.deltaTime);
                return amount != 0;
            }
            else
            {
                return true;
            }         
        }

        public static Vector3 GetLocalPosFrom(Transform trf, Transform from)
        {
            return from.InverseTransformPoint(trf.position);
        }

        public static Quaternion GetLocalRotFrom(Transform trf, Transform from)
        {
            return Quaternion.Inverse(from.rotation) * trf.rotation;
        }

        public static void SetChildTrfParent(Part sourcePart, Transform parent)
        {
            List<Part> pcs = KAS_Shared.GetAllChilds(sourcePart);
            foreach (Part pc in pcs)
            {
                pc.transform.parent = parent;
            }
        }

        public static void SetPartLocalPosFrom(Transform trf, Transform from, Vector3 localPos)
        {
            trf.position = from.TransformPoint(localPos);
        }

        public static void SetPartLocalRotFrom(Transform trf, Transform from, Quaternion localRot)
        {
            trf.rotation = from.rotation * localRot;
        }

        public static void SetPartLocalPosRotFrom(Transform trf, Transform from, Vector3 localPos, Quaternion localRot)
        {
            SetPartLocalPosFrom(trf, from, localPos);
            SetPartLocalRotFrom(trf, from, localRot);
            DebugLog("Set " + trf.name + " position to : " + localPos + " | Rotation : " + localRot);
        }

        public static bool createFXSound(Part part, FXGroup group, string sndPath, bool loop, float maxDistance = 30f)
        {
            group.audio = part.gameObject.AddComponent<AudioSource>();
            group.audio.volume = GameSettings.SHIP_VOLUME;
            group.audio.rolloffMode = AudioRolloffMode.Linear;
            group.audio.dopplerLevel = 0f;
            group.audio.panLevel = 1f;
            group.audio.maxDistance = maxDistance;
            group.audio.loop = loop;
            group.audio.playOnAwake = false;
            if (GameDatabase.Instance.ExistsAudioClip(sndPath))
            {
                group.audio.clip = GameDatabase.Instance.GetAudioClip(sndPath);
                return true;
            }
            else
            {
                KAS_Shared.DebugError("Sound not found in the game database !");
                ScreenMessages.PostScreenMessage("Sound file : " + sndPath + " as not been found, please check your KAS installation !", 10, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }     
        }

        public static void InvalidateContextMenu(Part part)
        {
            foreach (var window in GameObject.FindObjectsOfType(typeof(UIPartActionWindow)).Cast<UIPartActionWindow>().Where(w => w.part == part))
            {
                window.displayDirty = true;
            }
        }

    }
}