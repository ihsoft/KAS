using UnityEngine;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace KAS
{
    public class KAS_Tube : MonoBehaviour
    {
        // Tube source parameters
        public Transform srcNode;
        public tubeJointType srcJointType = tubeJointType.None;
        // Tube target parameters
        public Transform tgtNode;
        public tubeJointType tgtJointType = tubeJointType.None;
        // Common
        public Color color = Color.white;
        public bool updateContinually = true;
        public float tubeScale = 0.15f;
        public float sphereScale = 0.15f;
        public float tubeTexTilingOffset = 2;
        public Texture2D tubeTexture;
        public Texture2D sphereTexture;
        public Texture2D tubeJoinedTexture;
        public Vector3 nodeAngle = new Vector3(0f, 0f, 0f);
        public string shaderName = "Diffuse";
        public enum tubeJointType
        {
            None,
            Rounded,
            ShiftedAndRounded,
            Joined,
        }

        //Internal
        private Vector3 tubeAngle = new Vector3(90f, 0f, 0f);
        private bool tubeLoaded = false;

        private GameObject tube;
        private GameObject srcSphere;
        private GameObject tgtSphere;
        private GameObject srcTubeSphere;
        private GameObject tgtTubeSphere;
        private MeshRenderer tubeMR;
        private MeshRenderer srcSphereMR;
        private MeshRenderer srcTubeSphereMR;
        private MeshRenderer tgtSphereMR;
        private MeshRenderer tgtTubeSphereMR;

        void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (tubeLoaded)
            {
                if (updateContinually)
                {
                    UpdateTube();
                }
            }
        }

        public void Load()
        {
            //Create tube primitive
            tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.name = "KAStube";
            DestroyImmediate(tube.GetComponent<Collider>());
            tube.transform.localScale = new Vector3(tubeScale, tubeScale, tubeScale);
            tubeMR = tube.GetComponent<MeshRenderer>();
            tubeMR.name = "KAStube";
            tubeMR.material = new Material(Shader.Find(shaderName));
            tubeMR.material.mainTexture = tubeTexture;
            tubeMR.material.color = color;


            if (srcJointType != tubeJointType.None)
            {
                //Create sphere primitive at source
                srcSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                srcSphere.name = "KASsrcSphere";
                DestroyImmediate(srcSphere.GetComponent<Collider>());
                srcSphere.transform.localScale = new Vector3(sphereScale, sphereScale, sphereScale);
                srcSphere.transform.parent = srcNode;
                srcSphere.transform.localPosition = Vector3.zero;
                srcSphere.transform.localRotation = Quaternion.identity * Quaternion.Euler(new Vector3(90f, 0f, 0f));
                if (srcJointType == tubeJointType.ShiftedAndRounded || srcJointType == tubeJointType.Joined)
                {
                    srcSphere.transform.localPosition += new Vector3(0f, 0f, tubeScale / 2);
                }
                srcSphereMR = srcSphere.GetComponent<MeshRenderer>();
                srcSphereMR.name = "KASsrcSphere";
                srcSphereMR.material = new Material(Shader.Find(shaderName));
                srcSphereMR.material.mainTexture = sphereTexture;
                srcSphereMR.material.color = color;

                if (srcJointType == tubeJointType.Joined)
                {
                    //Create joined tube primitive at source
                    srcTubeSphere = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    srcTubeSphere.name = "KASsrcTube";
                    DestroyImmediate(srcTubeSphere.GetComponent<Collider>());
                    srcTubeSphere.transform.localScale = new Vector3(tubeScale, tubeScale, tubeScale);
                    srcTubeSphere.transform.parent = srcNode;
                    srcTubeSphere.transform.localPosition = Vector3.zero;
                    srcTubeSphere.transform.localRotation = Quaternion.identity;
                    srcTubeSphereMR = srcTubeSphere.GetComponent<MeshRenderer>();
                    srcTubeSphereMR.name = "KASsrcTube";
                    srcTubeSphereMR.material = new Material(Shader.Find(shaderName));
                    srcTubeSphereMR.material.mainTexture = tubeJoinedTexture;
                    srcTubeSphereMR.material.color = color;
                    ScaleBetweenPoints(srcTubeSphere.transform, srcNode.position, srcSphere.transform.position, tubeAngle, srcTubeSphereMR.material, tubeTexTilingOffset);
                }
            }

            if (tgtJointType != tubeJointType.None)
            {
                //Create sphere primitive at target
                tgtSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tgtSphere.name = "KAStgtSphere";
                DestroyImmediate(tgtSphere.GetComponent<Collider>());
                tgtSphere.transform.localScale = new Vector3(sphereScale, sphereScale, sphereScale);
                tgtSphere.transform.parent = tgtNode;
                tgtSphere.transform.localPosition = Vector3.zero;
                tgtSphere.transform.localRotation = Quaternion.identity * Quaternion.Euler(new Vector3(90f, 0f, 0f));
                if (tgtJointType == tubeJointType.ShiftedAndRounded || tgtJointType == tubeJointType.Joined)
                {
                    tgtSphere.transform.localPosition += new Vector3(0f, 0f, tubeScale / 2);
                }
                tgtSphereMR = tgtSphere.GetComponent<MeshRenderer>();
                tgtSphereMR.name = "KAStgtSphere";
                tgtSphereMR.material = new Material(Shader.Find(shaderName));
                tgtSphereMR.material.mainTexture = sphereTexture;
                tgtSphereMR.material.color = color;
                if (tgtJointType == tubeJointType.Joined)
                {
                    //Create joined tube primitive at target
                    tgtTubeSphere = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    tgtTubeSphere.name = "KAStgtTube";
                    DestroyImmediate(tgtTubeSphere.GetComponent<Collider>());
                    tgtTubeSphere.transform.localScale = new Vector3(tubeScale, tubeScale, tubeScale);
                    tgtTubeSphere.transform.parent = tgtNode;
                    tgtTubeSphere.transform.localPosition = Vector3.zero;
                    tgtTubeSphere.transform.localRotation = Quaternion.identity;
                    tgtTubeSphereMR = tgtTubeSphere.GetComponent<MeshRenderer>();
                    tgtTubeSphereMR.name = "KAStgtTube";
                    tgtTubeSphereMR.material = new Material(Shader.Find(shaderName));
                    tgtTubeSphereMR.material.mainTexture = tubeJoinedTexture;
                    tgtTubeSphereMR.material.color = color;
                    ScaleBetweenPoints(tgtTubeSphere.transform, tgtNode.position, tgtSphere.transform.position, tubeAngle, tgtTubeSphereMR.material, tubeTexTilingOffset);
                }
            }
            UpdateTube();
            tubeLoaded = true;
        }

        public void UnLoad()
        {
            if (tube) Destroy(tube);
            if (srcSphere) Destroy(srcSphere);
            if (tgtSphere) Destroy(tgtSphere);
            if (tgtTubeSphere) Destroy(tgtTubeSphere);
            if (srcTubeSphere) Destroy(srcTubeSphere);
            tubeLoaded = false;
        }

        void UpdateTube()
        {
            if (!srcNode || !tgtNode)
            {
                UnLoad();
                return;
            }

            Vector3 tmpSrcNode = new Vector3(0f, 0f, 0f);
            if (srcJointType == tubeJointType.ShiftedAndRounded || srcJointType == tubeJointType.Joined)
            {
                tmpSrcNode = srcSphere.transform.position;
            }
            else
            {
                tmpSrcNode = srcNode.position;
            }

            Vector3 tmpTgtNode = new Vector3(0f, 0f, 0f);
            if (tgtJointType == tubeJointType.ShiftedAndRounded || tgtJointType == tubeJointType.Joined)
            {
                tmpTgtNode = tgtSphere.transform.position;
            }
            else
            {
                tmpTgtNode = tgtNode.position;
            }

            ScaleBetweenPoints(tube.transform, tmpSrcNode, tmpTgtNode, tubeAngle, tubeMR.material, tubeTexTilingOffset);
        }

        public void SetColor(Color colorToSet)
        {
            if (tubeMR) tubeMR.material.color = colorToSet;
            if (srcSphereMR) srcSphereMR.material.color = colorToSet;
            if (srcTubeSphereMR) srcTubeSphereMR.material.color = colorToSet;
            if (tgtSphereMR) tgtSphereMR.material.color = colorToSet;
            if (tgtTubeSphereMR) tgtTubeSphereMR.material.color = colorToSet;
        }

        public void SetShader(string shaderName)
        {
            if (tubeMR) tubeMR.material = new Material(Shader.Find(shaderName));
            if (srcSphereMR) srcSphereMR.material = new Material(Shader.Find(shaderName));
            if (srcTubeSphereMR) srcTubeSphereMR.material = new Material(Shader.Find(shaderName));
            if (tgtSphereMR) tgtSphereMR.material = new Material(Shader.Find(shaderName));
            if (tgtTubeSphereMR) tgtTubeSphereMR.material = new Material(Shader.Find(shaderName));
        }

        public void SetTexture(Texture2D textureToSet)
        {
            if (tubeMR) tubeMR.material.mainTexture = textureToSet;
            if (srcSphereMR) srcSphereMR.material.mainTexture = textureToSet;
            if (srcTubeSphereMR) srcTubeSphereMR.material.mainTexture = textureToSet;
            if (tgtSphereMR) tgtSphereMR.material.mainTexture = textureToSet;
            if (tgtTubeSphereMR) tgtTubeSphereMR.material.mainTexture = textureToSet;
        }

        public void SetTubeScale(float scale)
        {
            tube.transform.localScale = new Vector3(scale, scale, scale);

            if (srcSphere)
            {
                srcSphere.transform.parent = null;
                srcSphere.transform.localScale = new Vector3(scale, scale, scale);
                srcSphere.transform.parent = srcNode;
            }

        }

        public void SetSphereScale(float scale)
        {
            srcSphere.transform.parent = null;
            srcSphere.transform.localScale = new Vector3(scale, scale, scale);
            srcSphere.transform.parent = srcNode;

            tgtSphere.transform.parent = null;
            tgtSphere.transform.localScale = new Vector3(scale, scale, scale);
            tgtSphere.transform.parent = tgtNode;
        }

        public void SetTubeSphereScale(float scale)
        {
            srcTubeSphere.transform.parent = null;
            srcTubeSphere.transform.localScale = new Vector3(scale, scale, scale);
            srcTubeSphere.transform.parent = srcNode;

            tgtTubeSphere.transform.parent = null;
            tgtTubeSphere.transform.localScale = new Vector3(scale, scale, scale);
            tgtTubeSphere.transform.parent = tgtNode;
        }

        public void DisableCollision(bool active)
        {
            tube.GetComponent<Collider>().isTrigger = active;
        }

        void ScaleBetweenPoints(Transform obj, Vector3 srcPos, Vector3 tgtPos, Vector3 angle, Material material = null, float textureTilingOffset = 0)
        {
            obj.position = (srcPos + tgtPos) / 2;
            obj.LookAt(tgtPos);
            obj.Rotate(angle, Space.Self);
            obj.localScale = new Vector3(obj.localScale.x, Vector3.Distance(obj.position, tgtPos), obj.localScale.z);
            if (material && textureTilingOffset != 0)
            {
                material.mainTextureScale = new Vector2(material.mainTextureScale.x, Vector3.Distance(obj.position, tgtPos) * textureTilingOffset);
            }
        }

        void OnDestroy()
        {
            UnLoad();
        }
    }
}
