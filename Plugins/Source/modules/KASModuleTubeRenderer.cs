// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using UnityEngine;
using KASAPIv1;

namespace KAS {

public class KASModuleTubeRenderer : PartModule, ILinkRenderer {
  // FIXME: docstring and class comments
  protected struct JointNode {
    public GameObject sphere;
    public GameObject pipe;
    readonly Transform node;

    public Vector3 position {
      get {
        return sphere != null ? sphere.transform.position : node.position;
      }
    }

    public JointNode(Transform node) {
      this.node = node;
      sphere = null;
      pipe = null;
    }

    public void DestroyPrimitives() {
      if (sphere != null) {
        sphere.DestroyGameObject();
        sphere = null;
      }
      if (pipe != null) {
        pipe.DestroyGameObject();
        pipe = null;
      }
    }

    public void SetJointColor(Color color) {
      SetColor(sphere, color);
      SetColor(pipe, color);
    }

    public void UpdateJointMaterial(Material newMaterial) {
      UpdateMaterial(sphere, newMaterial);
      UpdateMaterial(pipe, newMaterial);
    }
  }

  #region ILinkTubeRenderer config properties implementation
  /// <inheritdoc/>
  public string cfgRendererName { get { return rendererName; } }
  /// <inheritdoc/>
  public LinkJointType cfgSourceJointType { get { return sourceJointType; } }
  /// <inheritdoc/>
  public float cfgSourceJointOffset { get { return sourceJointOffset; } }
  /// <inheritdoc/>
  public LinkJointType cfgTargetJointType { get { return targetJointType; } }
  /// <inheritdoc/>
  public float cfgTargetJointOffset { get { return targetJointOffset; } }
  /// <inheritdoc/>
  public float cfgPipeTextureSamplesPerMeter { get { return pipeTextureSamplesPerMeter; } }
  /// <inheritdoc/>
  public string cfgPipeTexturePath { get { return pipeTexturePath; } }
  /// <inheritdoc/>
  public string cfgShaderName { get { return shaderName; } }
  /// <inheritdoc/>
  public LinkTextureRescaleMode cfgPipeRescaleMode { get { return pipeTextureRescaleMode; } }
  /// <inheritdoc/>
  public Color cfgColor {
    get { return _color; }
    set {
      _color = value;
      SetCurrentColor(value);
    }
  }
  Color _color;
  /// <inheritdoc/>
  public float cfgPipeDiameter { get { return pipeDiameter; } }
  /// <inheritdoc/>
  public float cfgSphereDiameter { get { return sphereDiameter; } }
  /// <inheritdoc/>
  public virtual Color? colorOverride {
    get { return _colorOverride; }
    set {
      _colorOverride = value;
      SetCurrentColor(value ?? color);
    }
  }
  Color? _colorOverride;
  /// <inheritdoc/>
  public virtual string shaderNameOverride {
    get { return _shaderNameOverride; }
    set {
      _shaderNameOverride = value;
      if (isStarted) {
        var newMaterial = CreateMaterial();
        sourceJointNode.UpdateJointMaterial(newMaterial);
        targetJointNode.UpdateJointMaterial(newMaterial);
        UpdateMaterial(linkPipeMR, newMaterial);
      }
    }
  }
  string _shaderNameOverride;
  /// <inheritdoc/>
  public virtual bool isPhysicalCollider {
    get {
      var collider = linkPipe.GetComponent<Collider>();
      return collider != null && !collider.isTrigger;
    }
    set {
      var collider = linkPipe.GetComponent<Collider>();
      if (collider != null) {
        collider.isTrigger = !value;
      }
    }
  }
  /// <inheritdoc/>
  public bool isStarted { get { return linkPipe != null; } }


  public Transform startSocketTransfrom { get; private set; }
  public Transform endSocketTransfrom { get; private set; }
  #endregion

  void SetCurrentColor(Color newColor) {
    if (isStarted) {
      sourceJointNode.SetJointColor(newColor);
      targetJointNode.SetJointColor(newColor);
      SetColor(linkPipeMR, newColor);
    }
  }

  // These fileds must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  [KSPField]
  public string rendererName = string.Empty;
  [KSPField]
  public LinkJointType sourceJointType = LinkJointType.RoundedWithOffset;
  [KSPField]
  public float sourceJointOffset = 0.15f;
  [KSPField]
  public LinkJointType targetJointType = LinkJointType.RoundedWithOffset;
  [KSPField]
  public float targetJointOffset = 0.15f;
  [KSPField]
  public float pipeTextureSamplesPerMeter = 2f;
  [KSPField]
  public string pipeTexturePath = "KAS/Textures/pipe";
  //public string pipeTexturePath = "KAS/Textures/piston180";
  [KSPField]
  public string shaderName = "Diffuse";
  [KSPField]
  public LinkTextureRescaleMode pipeTextureRescaleMode = LinkTextureRescaleMode.None;
  [KSPField]
  public LinkCollider colliderType = LinkCollider.None;

  [KSPField]
  public Color color = Color.white;
  [KSPField]
  public float pipeDiameter = 0.15f;
  [KSPField]
  public float sphereDiameter = 0.15f;

  // FIXME: calculate piston postions with respect to sphere radius
  [KSPField]
  public float prefabPipeLength = 0.2f + 0.02f*2 + 0.15f/2;
  [KSPField]
  public Vector3 prefabPipeStartPosition = Vector3.zero;
  [KSPField]
  public Vector3 prefabPipeStartDirection = Vector3.forward;

  // FIXME: drop
  [KSPField]
  public float[] testArray = new float[0];
  
  
  #endregion

  // FIXME: everyting is RO

  //Internal or protected
  protected Texture2D tubeTexture { get; private set; }
  protected GameObject linkPipe {
    get { return _linkPipe; }
    private set {
      _linkPipe = value;
      linkPipeMR = _linkPipe != null ? _linkPipe.GetComponent<Renderer>() : null;
    }
  }
  GameObject _linkPipe;
  protected Renderer linkPipeMR { get; private set; }
  protected JointNode sourceJointNode { get; private set; }
  protected JointNode targetJointNode { get; private set; }

  // FIXME: move to common API
  const string KspPartShaderName = "KSP/Bumped Specular";

  #region PartModule overrides
  protected Transform partModelTransform {
    get {
      if (_partModelTransform == null) {
        _partModelTransform = part.FindModelTransform("model");
      }
      return _partModelTransform;
    }
  }
  Transform _partModelTransform;

  const string StartSocketObjName = "PipeStart";
  const string EndSocketObjName = "PipeEnd";
  const string PipeMeshName = "PipeMesh";

  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    LoadTexture();
    Debug.LogWarningFormat("** ON LOAD: {0}", part.name);
    if (HighLogic.LoadedScene == GameScenes.LOADING) {
      //FIXME
      Debug.LogWarningFormat("*** LOADING PART: {0}, texture {1}", part.name, pipeTexturePath);

      var nodeTransfrom = new GameObject("SourceAttachNode").transform;
      nodeTransfrom.parent = partModelTransform;
      nodeTransfrom.localPosition = prefabPipeStartPosition;
      //FIXME: SetLookRotation
      nodeTransfrom.localRotation = Quaternion.LookRotation(prefabPipeStartDirection);
      
      startSocketTransfrom = new GameObject(StartSocketObjName).transform;
      startSocketTransfrom.parent = nodeTransfrom;
      endSocketTransfrom = new GameObject(EndSocketObjName).transform;
      endSocketTransfrom.parent = startSocketTransfrom;
      endSocketTransfrom.localPosition = new Vector3(0, 0, prefabPipeLength);
      endSocketTransfrom.localRotation.SetLookRotation(-prefabPipeStartDirection);
      CreateModelMeshes();
    }
  }

  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
    //FIXME: check for prefab mode
    Debug.LogWarningFormat("** ON START: {0}", part.name);
    startSocketTransfrom = part.FindModelTransform(StartSocketObjName);
    endSocketTransfrom = part.FindModelTransform(EndSocketObjName);
    linkPipe = part.FindModelTransform(PipeMeshName).gameObject;
    Debug.LogWarningFormat("** Loaded socket objects: {0} / {1}",
                           startSocketTransfrom, endSocketTransfrom);

    //FIXME
    var pipe = partModelTransform.Find(PipeMeshName).gameObject;
    Debug.LogWarningFormat("** GET scale on {0}: {1}",
                           pipe, pipe.GetComponent<Renderer>().material.mainTextureScale);
  }

  // FIMXE: probably is not needed as a function
  void CreatePipe() {
    linkPipe = CreatePrimitive(PrimitiveType.Cylinder, pipeDiameter, colliderType: colliderType);
    linkPipe.name = PipeMeshName;
  }

  protected virtual void CreateModelMeshes() {
    sourceJointNode = CreateJointNode(sourceJointType, startSocketTransfrom, sourceJointOffset);
    targetJointNode = CreateJointNode(targetJointType, endSocketTransfrom, targetJointOffset);
    linkPipe = CreatePrimitive(PrimitiveType.Cylinder, pipeDiameter, colliderType: colliderType);
    linkPipe.name = PipeMeshName;
    //FIXME
    Debug.LogWarningFormat("**** park length: {0}", (endSocketTransfrom.position - startSocketTransfrom.position).magnitude);
    Debug.LogWarningFormat("**** joint node length: {0}", (targetJointNode.position - sourceJointNode.position).magnitude);
    SetupPipe(linkPipe.transform, sourceJointNode.position, targetJointNode.position);
    RescaleTextureToLength(linkPipe, renderer: linkPipeMR);
  }

  /// <inheritdoc/>
  public override void OnUpdate() {
    base.OnUpdate();
    UpdateLink();
  }
  #endregion

  /// <remarks>Overridden from <see cref="MonoBehaviour"/>.</remarks>
  protected virtual void OnDestroy() {
    StopRenderer();
  }

  #region ILinkTubeRenderer implementation
  /// <inheritdoc/>
  public virtual void StartRenderer(Transform source, Transform target) {
    if (isStarted) {
      Debug.LogWarning("Renderer already started. Stopping...");
      StopRenderer();
    }
    // Reset any overrides.
    colorOverride = null;
    shaderNameOverride = null;
    // Create joints and link pipe meshes.
    sourceJointNode = CreateJointNode(sourceJointType, source, sourceJointOffset);
    targetJointNode = CreateJointNode(targetJointType, target, targetJointOffset);
    linkPipe = CreatePrimitive(PrimitiveType.Cylinder, pipeDiameter, part.transform,
                               colliderType: colliderType);
    SetupPipe(linkPipe.transform, sourceJointNode.position, targetJointNode.position);
    RescaleTextureToLength(linkPipe, renderer: linkPipeMR);
    part.HighlightRenderers = null;
    part.SetHighlightDefault();  // Force renderes list recreated.
  }
  
  /// <inheritdoc/>
  public virtual void StopRenderer() {
    if (isStarted) {
      sourceJointNode.DestroyPrimitives();
      targetJointNode.DestroyPrimitives();
      if (linkPipe != null) {
        linkPipe.DestroyGameObject();
        linkPipe = null;
      }
    }
  }

  /// <inheritdoc/>
  public virtual void UpdateLink() {
    //if (isStarted) {
      //FIXME
      Debug.LogWarningFormat("Update link");
      SetupPipe(linkPipe.transform, sourceJointNode.position, targetJointNode.position);
      //FIXME: support target and center modes
      if (pipeTextureRescaleMode != LinkTextureRescaleMode.None) {
        RescaleTextureToLength(linkPipe, renderer: linkPipeMR);
      }
    //}
  }

  /// <inheritdoc/>
  /// <remarks>It does a very simple check by moving a sphere along the link direction. Sphere
  /// radius matches pipe's radius. If link's mesh is more complex than a plain pipe then this
  /// method may produce both false positives and false negatives.</remarks>
  public virtual string CheckColliderHits(Transform source, Transform target) {
    if (colliderType != LinkCollider.None) {
      RaycastHit hit;
      var direction = (target.position - source.position).normalized;
      // To not hit the source part shift raycast sphere by one radius towards the target. 
      var origin = source.position + direction * pipeDiameter / 2;
      // To not hit the target part reduce max distance by TWO radiuses (origin is already shifted).
      // If, however, the target is hit just igore it: it's the last hit point on the line, so
      // nothing else could get affected.
      //FIXME: drop this BS with adjustments, juts skip src&trg hits via collider ignore.
      var maxDistance = direction.magnitude - pipeDiameter; 
      //FIXME: drop debug code
      Physics.SphereCast(origin, pipeDiameter, direction, out hit, maxDistance);
      if (Physics.SphereCast(origin, pipeDiameter, direction, out hit, maxDistance)
          && (hit.rigidbody.gameObject == target.gameObject
              || hit.rigidbody.gameObject == source.gameObject)) {
        //FIXME
        Debug.LogWarningFormat("OOOOOOOOOPS! Hit vessel {0}",
                               hit.rigidbody.gameObject.GetComponent<Part>().vessel);
      }
      if (Physics.SphereCast(origin, pipeDiameter, direction, out hit, maxDistance)
          && hit.rigidbody.gameObject != target.gameObject
          && hit.rigidbody.gameObject != source.gameObject) {
        var hitPart = hit.rigidbody.gameObject.GetComponent<Part>();
        //var evaModule = hitPart.GetComponent<KerbalEVA>();
        //FIXME: move to constant
        return string.Format("Link would collide with {0} ({1:F2}m from source)",
                             hitPart.partInfo.title,
                             hit.distance);
      }
    }
    return null;
  }
  #endregion

  #region New inheritable methods
  protected virtual JointNode CreateJointNode(
      LinkJointType type, Transform node, float jointOffset = 0) {
    var res = new JointNode(node);
    if (type != LinkJointType.None) {
      res.sphere = CreatePrimitive(PrimitiveType.Sphere, sphereDiameter, node);
      RescaleTextureToLength(res.sphere);
      if (type == LinkJointType.RoundedWithOffset) {
        // Raise connection sphere over the node.
        res.sphere.transform.localPosition += new Vector3(0, 0, jointOffset);
        // Connect sphere and the node with a pipe.
        res.pipe = CreatePrimitive(PrimitiveType.Cylinder, sphereDiameter, node);
        SetupPipe(res.pipe.transform, node.transform.position, res.sphere.transform.position);
        RescaleTextureToLength(res.pipe);
      }
    }
    return res;
  }

  protected virtual JointNode LoadJointNode(Transform node) {
    var res = new JointNode(node);
    res.sphere = node.Find("Sphere").gameObject;
    var cylinder = node.Find("Cylinder");
    res.pipe = cylinder != null ? cylinder.gameObject : null;
    return res;
  }
  #endregion

  #region New utility methods
  //FIXME: docstring
  protected static void UpdateMaterial(GameObject obj, Material newMaterial) {
    if (obj != null) {
      UpdateMaterial(obj.GetComponent<Renderer>(), newMaterial);
    }
  }
  
  //FIXME: docstring
  protected static void UpdateMaterial(Renderer renderer, Material newMaterial) {
    if (renderer != null) {
      var oldScale = renderer.material.mainTextureScale;
      renderer.material = newMaterial;
      renderer.material.mainTextureScale = oldScale;
    }
  }

  //FIXME: docstring
  protected static void SetColor(GameObject obj, Color color) {
    if (obj != null) {
      SetColor(obj.GetComponent<Renderer>(), color);
    }
  }

  //FIXME: docstring
  protected static void SetColor(Renderer renderer, Color color) {
    if (renderer != null) {
      renderer.material.color = color;
    }
  }

  /// <summary>Creates a primitive mesh and attaches it to the part's model.</summary>
  /// <param name="type">Type of the primitive.</param>
  /// <param name="scale">Local scale of the primitive transform.</param>
  /// <param name="parent">Parent transform to attach primitive to. If <c>null</c> then new
  /// primitive will be attached to the part's model.</param>
  /// <param name="colliderType">Defines how primitive collider (if any) will be calculated. See
  /// <see cref="LinkCollider"/> for more information.</param>
  /// <returns>Game object of the new primitive.</returns>
  /// <seealso cref="LinkCollider"/>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/GameObject.CreatePrimitive.html"/>
  protected GameObject CreatePrimitive(PrimitiveType type, float scale,
                                       Transform parent = null,
                                       LinkCollider colliderType = LinkCollider.None) {
    var primitive = GameObject.CreatePrimitive(type);
    DestroyImmediate(primitive.GetComponent<Collider>());
    primitive.transform.parent = parent ?? part.FindModelTransform("model");
    primitive.transform.localPosition = Vector3.zero;
    primitive.transform.localRotation = Quaternion.identity;
    primitive.transform.localScale = new Vector3(scale, scale, scale);
    primitive.GetComponent<MeshRenderer>().material = CreateMaterial();

    // Adjust primitives with length so what it's aligned along Z axis and the length is exactly x1
    // of the scale.
    if (type == PrimitiveType.Cylinder) {
      var rotation = Quaternion.Euler(90, 0, 0);
      var meshFilter = primitive.GetComponent<MeshFilter>();
      var primitiveScale = new Vector3(1, 1, 0.5f);
      // For some reason shared mesh refuses to properly react to the vertices updates (Unity
      // optimziation?), so create a mesh copy and adjust it. It results in a loss of a bit of
      // performance and memory but given it's only done on the scene load it's fine.
      var mesh = meshFilter.mesh;  // Do NOT use sharedMesh here!
      // Changing of mesh vertices/normals *must* follow read/modify/store contract. Read Unity docs
      // for more details.
      var vertices = mesh.vertices;
      var normals = mesh.normals;
      for (var i = 0; i < mesh.vertexCount; ++i) {
        vertices[i] = rotation * vertices[i];
        vertices[i].Scale(primitiveScale);
        normals[i] = rotation * normals[i];
      }
      mesh.vertices = vertices;
      mesh.normals = normals;
      mesh.RecalculateBounds();
      mesh.RecalculateNormals();
      mesh.Optimize();  // We're not going to modify it further.
      meshFilter.sharedMesh = mesh;
    }

    // Add collider of the requested type.
    if (colliderType == LinkCollider.Mesh) {
      var collider = primitive.AddComponent<MeshCollider>();
      collider.convex = true;
    } else if (colliderType == LinkCollider.Capsule) {
      var collider = primitive.AddComponent<CapsuleCollider>();
      collider.direction = 2;  // Z axis
      collider.height = 1;
      collider.radius = 1;
    }

    return primitive;
  }

  protected void RescaleTextureToLength(GameObject obj, Renderer renderer = null) {
    //var newScale = obj.transform.localScale.z * pipeTextureSamplesPerMeter;
    var newScale = 1.0f;
    var mr = renderer ?? obj.GetComponent<Renderer>();
    mr.material.mainTextureScale = new Vector2(mr.material.mainTextureScale.x, newScale);
    // FIXME: drop
    Debug.LogWarningFormat("** set scale on {0}: {1}", obj, mr.material.mainTextureScale);
  }

  protected Material CreateMaterial() {
    var material = new Material(Shader.Find(shaderNameOverride ?? KspPartShaderName));
    //FIXME: set emissive flags to black.
    material.mainTexture = tubeTexture;
    material.color = colorOverride ?? color;
    return material;
  }

  protected static void SetupPipe(Transform obj, Vector3 fromPos, Vector3 toPos) {
    obj.position = (fromPos + toPos) / 2;
    obj.LookAt(toPos);
    obj.localScale =
        new Vector3(obj.localScale.x, obj.localScale.y, Vector3.Distance(fromPos, toPos));
  }
  #endregion

  #region Private service methods
  void LoadTexture() {
    tubeTexture = GameDatabase.Instance.GetTexture(pipeTexturePath, false);
    if (tubeTexture == null) {
      // Use "red" texture if no file fiound.
      Debug.LogWarningFormat("Cannot load texture: {0}", pipeTexturePath);
      tubeTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
      tubeTexture.SetPixels(new[] {Color.red});
      tubeTexture.Apply();
    }
    tubeTexture.Compress(true /* highQuality */);
  }
  #endregion
}

}  // namespace
