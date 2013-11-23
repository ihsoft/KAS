using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KAS
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KASAddonPointer : MonoBehaviour
    {
        public static string bipWrongSndPath = "KAS/Sounds/bipwrong";
        public GameObject audioGo = new GameObject();
        public AudioSource audioBipWrong = new AudioSource();

        public enum PointerMode
        {
            MoveAndAttach = 0,
            CopyAndAttach = 1,
        }

        public static GameObject soundGo;


        // Pointer parameters
        private static bool allowPart = false;
        private static bool allowEva = false;
        private static bool allowStatic = false;
        private static PointerMode pointerMode;
        private static Part partToAttach;
        private static float maxDist = 2f;
        private static Transform sourceTransform;
        private static bool msgOnly = false;

        private static bool running = false;
        private static GameObject pointer;
        private static List<MeshRenderer> allModelMr;
        private static Vector3 customRot = new Vector3(0f, 0f, 0f);
        private static Transform pointerNodeTransform;

        public static bool isRunning
        {
            get { return running; }
        }

        void Awake()
        {
            audioBipWrong = audioGo.AddComponent<AudioSource>();
            audioBipWrong.volume = GameSettings.UI_VOLUME;
            audioBipWrong.panLevel = 0;  //set as 2D audiosource

            if (GameDatabase.Instance.ExistsAudioClip(bipWrongSndPath))
            {
                audioBipWrong.clip = GameDatabase.Instance.GetAudioClip(bipWrongSndPath);
            }
            else
            {
                KAS_Shared.DebugError("Awake(AttachPointer) Bip wrong sound not found in the game database !");
                ScreenMessages.PostScreenMessage("Sound file : " + bipWrongSndPath + " as not been found, please check your KAS installation !", 10, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        public static void StartPointer(Part partToMoveAndAttach, PointerMode mode, bool partIsValid, bool evaIsValid, bool staticIsValid, float maxDistance, Transform from, bool sendMsgOnly)
        {
            if (!running)
            {
                KAS_Shared.DebugLog("StartPointer(pointer)");
                customRot = Vector3.zero;
                allowPart = partIsValid;
                allowEva = evaIsValid;
                allowStatic = staticIsValid;
                msgOnly = sendMsgOnly;
                partToAttach = partToMoveAndAttach;
                maxDist = maxDistance;
                pointerMode = mode;
                sourceTransform = from;
                ShowAttachHelpMsg();
                running = true;             
            }
        }

        public static void StopPointer()
        {
            KAS_Shared.DebugLog("StopPointer(pointer)");
            running = false;
        }

        void Update()
        {
            UpdatePointer();
        }

        public void UpdatePointer()
        {
            if (!running)
            {
                if (pointer) UnityEngine.Object.Destroy(pointer);
                return;
            }

            //Cast ray
            Ray ray = FlightCamera.fetch.mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 500, 557059))
            {
                if (pointer) UnityEngine.Object.Destroy(pointer);
                return;
            }

            //Create pointer if needed
            if (!pointer)
            {
                GameObject modelGo = partToAttach.FindModelTransform("model").gameObject;
                pointer = Mesh.Instantiate(modelGo) as GameObject;
                foreach (Collider col in pointer.GetComponentsInChildren<Collider>())
                {
                    UnityEngine.Object.Destroy(col);
                }

                allModelMr = new List<MeshRenderer>();
                // Remove attached tube mesh renderer if any
                List<MeshRenderer> tmpAllModelMr = new List<MeshRenderer>(pointer.GetComponentsInChildren<MeshRenderer>() as MeshRenderer[]);
                foreach (MeshRenderer mr in tmpAllModelMr)
                {
                    if (mr.name == "KAStube" || mr.name == "KASsrcSphere" || mr.name == "KASsrcTube" || mr.name == "KAStgtSphere" || mr.name == "KAStgtTube")
                    {
                        Destroy(mr);
                        continue;
                    }
                    allModelMr.Add(mr);
                    mr.material = new Material(Shader.Find("Transparent/Diffuse"));
                }
                pointerNodeTransform = new GameObject("KASPointerPartNode").transform;
                pointerNodeTransform.parent = pointer.transform;
                pointerNodeTransform.localPosition = partToAttach.srfAttachNode.position;
                pointerNodeTransform.rotation = KAS_Shared.DirectionToQuaternion(pointer.transform, partToAttach.srfAttachNode.orientation);
                }

            //Set default color
            Color color = Color.green;

            // Check if object is valid
            bool isValidObj = false;
            Part hitPart = null;
            KerbalEVA hitEva = null;
            if (hit.rigidbody)
            {
                hitPart = hit.rigidbody.GetComponent<Part>();
                hitEva = hit.rigidbody.GetComponent<KerbalEVA>();
                if (hitPart && allowPart && !hitEva & hitPart != partToAttach) isValidObj = true;
                if (hitEva && allowEva) isValidObj = true;
            }
            if (!hitPart && !hitEva && allowStatic) isValidObj = true;

            //Check distance
            bool isValidSourceDist = true;
            if (sourceTransform)
            {
                isValidSourceDist = Vector3.Distance(FlightGlobals.ActiveVessel.transform.position, sourceTransform.position) <= maxDist;
            }
            bool isValidTargetDist = Vector3.Distance(FlightGlobals.ActiveVessel.transform.position, hit.point) <= maxDist;

            //Set color
            if (!isValidObj)
            {
                color = Color.red;
            }
            else if (!isValidSourceDist || !isValidTargetDist)
            {
                color = Color.yellow;
            }
            color.a = 0.5f;
            foreach (MeshRenderer mr in allModelMr) mr.material.color = color;

            //Rotation keys
            if (Input.GetKeyDown(KASAddonControlKey.rotateLeftKey.ToLower()))
            {
                RotatePointer(-15);
            }
            if (Input.GetKeyDown(KASAddonControlKey.rotateRightKey.ToLower()))
            {
                RotatePointer(+15);
            }

            KAS_Shared.MoveAlign(pointer.transform, pointerNodeTransform, hit);
            pointer.transform.rotation *= Quaternion.Euler(customRot);
            
            //Attach on click
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                KAS_Shared.DebugLog("Attachment started...");
                if (!isValidObj)
                {
                    ScreenMessages.PostScreenMessage("Can't attach, target is not allowed !");
                    audioBipWrong.Play();
                    return;
                }

                if (!isValidSourceDist)
                {
                    ScreenMessages.PostScreenMessage("Can't attach, too far from source !");
                    audioBipWrong.Play();
                    return;
                }

                if (!isValidTargetDist)
                {
                    ScreenMessages.PostScreenMessage("Can't attach, too far from target !");
                    audioBipWrong.Play();
                    return;
                }

                KASModuleGrab modulegrab = partToAttach.GetComponent<KASModuleGrab>();
                

                //Move and attach mode
                if (pointerMode == PointerMode.MoveAndAttach)
                {
                    // Drop and detach part if needed
                    if (modulegrab)
                    {
                        if (modulegrab.grabbed) modulegrab.Drop();
                        modulegrab.Detach();
                    }

                    KASModuleWinch connectedWinch = KAS_Shared.GetConnectedWinch(partToAttach);
                    if (!connectedWinch)
                    {
                        KAS_Shared.DecoupleFromAll(partToAttach);
                    }

                    //Move part
                    partToAttach.transform.position = pointer.transform.position;
                    partToAttach.transform.rotation = pointer.transform.rotation;

                    if (connectedWinch)
                    {
                        //Set cable lenght to real lenght
                        connectedWinch.cableJointLength = connectedWinch.cableRealLenght;                    
                    }

                    if (msgOnly)
                    {
                        KAS_Shared.DebugLog("UpdatePointer(Pointer) Attach using send message");
                        if (hitPart) partToAttach.SendMessage("OnAttachPart", hitPart, SendMessageOptions.DontRequireReceiver);
                        else partToAttach.SendMessage("OnAttachStatic", SendMessageOptions.DontRequireReceiver);
                    }
                    else
                    {
                        KAS_Shared.DebugLog("UpdatePointer(Pointer) Attach with couple or static method");
                        if (!hitPart && !hitEva)
                        {
                            if (modulegrab)
                            {
                                modulegrab.AttachStatic();
                                modulegrab.fxSndAttachStatic.audio.Play();
                            }
                            else
                            {
                                KAS_Shared.DebugWarning("UpdatePointer(Pointer) No grab module found, part cannot be attached on static");
                            }
                        }
                        else
                        {
                            partToAttach.Couple(hitPart);
                            if (modulegrab)
                            {
                                modulegrab.fxSndAttachPart.audio.Play();
                            }
                            else
                            {
                                KAS_Shared.DebugWarning("UpdatePointer(Pointer) No grab module found, cannot fire sound");
                            }
                        }
                        partToAttach.SendMessage("OnAttach", SendMessageOptions.DontRequireReceiver);     
                    }
                }

                if (pointerMode == PointerMode.CopyAndAttach)
                {
                    // Not tested !
                    Part newPart = KAS_Shared.CreatePart(partToAttach.partInfo, pointer.transform.position, pointer.transform.rotation, partToAttach);
                    if (msgOnly)
                    {
                        if (hitPart) StartCoroutine(WaitAndSendMsg(newPart, pointer.transform.position, pointer.transform.rotation, hitPart));
                        else StartCoroutine(WaitAndSendMsg(newPart, pointer.transform.position, pointer.transform.rotation));
                    }
                    else
                    {         
                        if (!hitPart && !hitEva)
                        {
                            StartCoroutine(WaitAndAttach(newPart, pointer.transform.position, pointer.transform.rotation, hitPart));
                        }
                        else
                        {
                            StartCoroutine(WaitAndAttach(newPart, pointer.transform.position, pointer.transform.rotation));
                        }
                    }
                }
                running = false;
            }
        }

        private IEnumerator WaitAndAttach(Part partToAttach, Vector3 position, Quaternion rotation, Part toPart = null)
        {
            while (!partToAttach.rigidbody)
            {
                KAS_Shared.DebugLog("WaitAndAttach(Pointer) - Waiting rigidbody to initialize...");
                yield return new WaitForFixedUpdate();
            }
            KASModuleGrab moduleGrab = partToAttach.GetComponent<KASModuleGrab>();
            if (toPart)
            {
                KAS_Shared.DebugLog("WaitAndAttach(Pointer) - Rigidbody initialized, setting velocity...");
                partToAttach.rigidbody.velocity = toPart.rigidbody.velocity;
                partToAttach.rigidbody.angularVelocity = toPart.rigidbody.angularVelocity;
                KAS_Shared.DebugLog("WaitAndAttach(Pointer) - Waiting velocity to apply by waiting 0.1 seconds...");
                yield return new WaitForSeconds(0.1f);
                partToAttach.transform.position = position;
                partToAttach.transform.rotation = rotation;
                partToAttach.Couple(toPart);
                if (moduleGrab)
                {
                    moduleGrab.fxSndAttachPart.audio.Play();
                }
                else
                {
                    KAS_Shared.DebugWarning("UpdatePointer(Pointer) No grab module found, cannot fire sound");
                }
            }
            else
            {
                partToAttach.transform.position = position;
                partToAttach.transform.rotation = rotation;
                if (moduleGrab)
                {
                    moduleGrab.AttachStatic();
                    moduleGrab.fxSndAttachStatic.audio.Play();
                }
                else
                {
                    KAS_Shared.DebugWarning("UpdatePointer(Pointer) No grab module found, part cannot be attached on static");
                }
            }
        }

        private IEnumerator WaitAndSendMsg(Part partToAttach, Vector3 position, Quaternion rotation, Part toPart = null)
        {
            while (!partToAttach.rigidbody)
            {
                KAS_Shared.DebugLog("WaitAndAttach(Pointer) - Waiting rigidbody to initialize...");
                yield return new WaitForFixedUpdate();
            }
            if (toPart)
            {
                KAS_Shared.DebugLog("WaitAndAttach(Pointer) - Rigidbody initialized, setting velocity...");
                partToAttach.rigidbody.velocity = toPart.rigidbody.velocity;
                partToAttach.rigidbody.angularVelocity = toPart.rigidbody.angularVelocity;
                KAS_Shared.DebugLog("WaitAndAttach(Pointer) - Waiting velocity to apply by waiting 0.1 seconds...");
                yield return new WaitForSeconds(0.1f);
                partToAttach.transform.position = position;
                partToAttach.transform.rotation = rotation;
                partToAttach.SendMessage("OnAttachPart", toPart, SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                partToAttach.transform.position = position;
                partToAttach.transform.rotation = rotation;
                partToAttach.SendMessage("OnAttachStatic", SendMessageOptions.DontRequireReceiver);
            }
        }

        private static void RotatePointer(float dist)
        {
            //left (-1.0, 0.0, 0.0) | right (1.0, 0.0, 0.0)   
            if (Vector3.Normalize(partToAttach.srfAttachNode.orientation) == Vector3.left || Vector3.Normalize(partToAttach.srfAttachNode.orientation) == Vector3.right)
            {
                customRot.Set(customRot.x + dist, customRot.y, customRot.z);
            }
            //down (0.0, -1.0, 0.0) | up (0.0, 1.0, 0.0)
            if (Vector3.Normalize(partToAttach.srfAttachNode.orientation) == Vector3.down || Vector3.Normalize(partToAttach.srfAttachNode.orientation) == Vector3.up)
            {
                customRot.Set(customRot.x, customRot.y + dist, customRot.z);
            }
            //back (0.0, 0.0, -1.0) | forward (0.0, 0.0, 1.0)
            if (Vector3.Normalize(partToAttach.srfAttachNode.orientation) == Vector3.back || Vector3.Normalize(partToAttach.srfAttachNode.orientation) == Vector3.forward)
            {
                customRot.Set(customRot.x, customRot.y, customRot.z + dist);
            }
            //battery orientation, illuminator (0.0, 0.0, -1.0) orient nok rotate nok
            //radial cport/pipe/strut/round rcs orientation (0.0, -1.3, 0.0) orient ok rotate ok
            //telus bay / rover  orientation (1.0, 0.0, 0.0) orient ok rotate nok
            // rcs block orientation (0.1, 0.0, 0.0) orient ok rotate nok
        }
        
        public static void ShowAttachHelpMsg()
        {
            ScreenMessages.PostScreenMessage("Attach pointer enabled. Press " + KASAddonControlKey.rotateLeftKey + "/" + KASAddonControlKey.rotateRightKey + " to rotate and mouse click to attach. Press echap, space, mouse2 or " + KASAddonControlKey.attachKey + " to cancel.", 5, ScreenMessageStyle.UPPER_CENTER);
        }

    }
}
