// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using UnityEngine;
using KASAPIv1;

namespace KAS {

public class KASModuleTubeRenderer : PartModule, ILinkTubeRenderer {
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

    public void SetColor(Color color) {
      if (sphere != null) {
        sphere.GetComponent<MeshRenderer>().material.color = color;
      }
      if (pipe != null) {
        pipe.GetComponent<MeshRenderer>().material.color = color;
      }
    }

    public void UpdateJointMaterial(Material newMaterial) {
      if (sphere != null) {
        KASModuleTubeRenderer.UpdateMaterial(sphere.GetComponent<MeshRenderer>(), newMaterial);
      }
      if (pipe != null) {
        KASModuleTubeRenderer.UpdateMaterial(pipe.GetComponent<MeshRenderer>(), newMaterial);
      }
    }
  }

  #region ILinkTubeRenderer config propertiers implementation
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public string cfgRendererName { get { return rendererName; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public LinkJointType cfgSourceJointType { get { return sourceJointType; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public float cfgSourceJointOffset { get { return sourceJointOffset; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public LinkJointType cfgTargetJointType { get { return targetJointType; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public float cfgTargetJointOffset { get { return targetJointOffset; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public float cfgPipeTextureSamplesPerMeter { get { return pipeTextureSamplesPerMeter; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public string cfgPipeTexturePath { get { return pipeTexturePath; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public string cfgShaderName { get { return shaderName; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public LinkTextureRescaleMode cfgPipeRescaleMode { get { return pipeTextureRescaleMode; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public Color cfgColor {
    get { return _color; }
    set {
      _color = value;
      SetCurrentColor(value);
    }
  }
  Color _color;
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public float cfgPipeScale { get { return pipeDiameter; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public float cfgSphereScale { get { return sphereDiameter; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public virtual Color? colorOverride {
    get { return _colorOverride; }
    set {
      _colorOverride = value;
      SetCurrentColor(value ?? color);
    }
  }
  Color? _colorOverride;
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
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
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public bool isStarted { get { return linkPipe != null; } }
  #endregion

  void SetCurrentColor(Color newColor) {
    if (isStarted) {
      sourceJointNode.SetColor(newColor);
      targetJointNode.SetColor(newColor);
      linkPipeMR.material.color = newColor;
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
  #endregion
    
  // FIXME: everyting is RO

  //Internal or protected
  protected Texture2D tubeTexture { get; private set; }
  protected GameObject linkPipe { get; private set; }
  protected MeshRenderer linkPipeMR { get; private set; }
  protected JointNode sourceJointNode { get; private set; }
  protected JointNode targetJointNode { get; private set; }
  
  static void UpdateMaterial(Renderer renderer, Material newMaterial) {
    var oldScale = renderer.material.mainTextureScale;
    renderer.material = newMaterial;
    renderer.material.mainTextureScale = oldScale;
  }

  #region PartModule overrides
  /// <remarks>Overridden from <see cref="PartModule"/>.</remarks>
  public override void OnAwake() {
    base.OnAwake();
    LoadTexture();
  }

  /// <remarks>Overridden from <see cref="PartModule"/>.</remarks>
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
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public virtual void StartRenderer(Transform source, Transform target) {
    if (isStarted) {
      Debug.LogWarning("Renderer already started. Stopping...");
      StopRenderer();
    }
    // Reset any overrides.
    colorOverride = null;
    shaderNameOverride = null;

    sourceJointNode = CreateJointNode(sourceJointType, source, sourceJointOffset);
    targetJointNode = CreateJointNode(targetJointType, target, targetJointOffset);
    //FIXME
    //linkPipe = CreatePrimitive(PrimitiveType.Cylinder, pipeScale, part.transform);
    linkPipe = CreatePrimitive(PrimitiveType.Cylinder, pipeDiameter, part.transform,
                               colliderType: colliderType);
    linkPipeMR = linkPipe.GetComponent<MeshRenderer>();  // To speedup OnUpdate() handling.
    SetupPipe(linkPipe.transform, sourceJointNode.position, targetJointNode.position);
    RescaleTextureToLength(linkPipe, renderer: linkPipeMR);
  }
  
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public virtual void StopRenderer() {
    if (isStarted) {
      sourceJointNode.DestroyPrimitives();
      targetJointNode.DestroyPrimitives();
      if (linkPipe != null) {
        linkPipe.DestroyGameObject();
        linkPipe = null;
        linkPipeMR = null;
      }
    }
  }

  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public virtual void UpdateLink() {
    if (isStarted) {
      SetupPipe(linkPipe.transform, sourceJointNode.position, targetJointNode.position);
      //FIXME: support target and center modes
      if (pipeTextureRescaleMode != LinkTextureRescaleMode.None) {
        RescaleTextureToLength(linkPipe, renderer: linkPipeMR);
      }
    }
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
  #endregion

  //FIXME add docs
  //FIXME add create mesh without game object
  #region New utility methods
  protected GameObject CreatePrimitive(PrimitiveType type, float scale, Transform parent,
                                       LinkCollider colliderType = LinkCollider.None) {
    var primitive = GameObject.CreatePrimitive(type);
    DestroyImmediate(primitive.GetComponent<Collider>());
    primitive.transform.parent = parent;
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
      // optimziation?), so create a mesh copy and adjust it. It result it a loss of a bit of
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

  protected void RescaleTextureToLength(
      GameObject obj, MeshRenderer renderer = null, float baseLength = 1.0f) {
    var newScale = obj.transform.localScale.z * baseLength * pipeTextureSamplesPerMeter;
    var mr = renderer ?? obj.GetComponent<MeshRenderer>();
    mr.material.mainTextureScale = new Vector2(mr.material.mainTextureScale.x, newScale);
  }

  protected Material CreateMaterial() {
    var material = new Material(Shader.Find(shaderNameOverride ?? shaderName));
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
