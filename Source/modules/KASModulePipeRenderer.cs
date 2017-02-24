// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine;
using KASAPIv1;
using KSPDev.KSPInterfaces;
using KSPDev.ModelUtils;

namespace KAS {

/// <summary>Module that draws a pipe between two nodes.</summary>
/// <remarks>
/// If it's assigned to link source then it will draw a pipe between source and target atatch nodes.
/// <para>
/// Pipe ends can be constructed differently:
/// <list type="table">
/// <item>
/// <term><see cref="PipeEndType.Simple"/></term>
/// <description>
/// Simple cylinder is drawn between the nodes. The ends of the pipe may look ugly at large angles
/// if they are not "sunken" into another mesh.
/// </description>
/// </item>
/// <item>
/// <term><see cref="PipeEndType.Rounded"/></term>
/// <description>
/// A sphere is draw at the end of the pipe. If sphere diameter matches pipe's diameter then pipe
/// gets capsule form. Though, sphere is not required to have the same diameter. This mode is good
/// for the cases when attach node is located on a surface of the part.
/// </description>
/// </item>
/// <item>
/// <term><see cref="PipeEndType.RoundedWithOffset"/></term>
/// <description>
/// Basically, it's the same as <see cref="PipeEndType.Rounded"/> but the node is located above the
/// part's surface. Between the part and the node a static cylinder is drawn which forms an "arm".
/// The length of the arm can be configured independently for source and target.
/// </description>
/// </item>
/// </list>
/// </para>
/// <para>See config settings for defining capsule sphere and arm</para>
/// </remarks>
/// <seealso cref="ILinkSource"/>
/// <seealso cref="ILinkRenderer"/>
public sealed class KASModulePipeRenderer : AbstractProceduralModel,
    // KAS interfaces.
    ILinkRenderer,
    // KPSDev sugar interfaces.    
    IPartModule, IsDestroyable{

  #region Internal config types
  /// <summary>Type if the end of the pipe.</summary>
  public enum PipeEndType {
    /// <summary>Pipe's end is just a section of the cylinder.</summary>
    Simple,
    /// <summary>Pipe ends with a half of a sphere. Kind of "capsule" design.</summary>
    Rounded,
    /// <summary>
    /// Link attach point is raised above the attach node point, and joint between the "arm" and
    /// the pipe is rounded using a sphere.
    /// </summary>
    RoundedWithOffset,
  }

  /// <summary>Mode of adjusting texture when pipe length is changed.</summary>
  public enum PipeTextureRescaleMode {
    /// <summary>Texture simply stretches to the pipe's size.</summary>
    Stretch,
    /// <summary>
    /// Texture is tiled starting from the source. The size of single texture sample depends
    /// on the settings.
    /// </summary>
    /// <seealso cref="pipeTextureSamplesPerMeter"/>
    TileFromSource,
    /// <summary>
    /// Texture is tiled starting from the target. The size of single texture sample depends
    /// on the settings.
    /// </summary>
    /// <seealso cref="pipeTextureSamplesPerMeter"/>
    TileFromTarget,
  }
  #endregion

  #region Helper class for drawing a pipe's end.
  struct PipeEndNode {
    /// <summary>Sphere primitive for rounding the end.</summary>
    public GameObject sphere;

    /// <summary>Cylinder primitive to connect attach node and the rounding sphere.</summary>
    public GameObject arm;

    readonly Transform node;

    /// <summary>
    /// Actual position to start pipe from. It gets affected by the <see cref="PipeEndType"/>.
    /// </summary>
    public Vector3 position {
      get {
        return sphere != null ? sphere.transform.position : node.position;
      }
    }

    /// <summary>Constructs end node at the specified tranform.</summary>
    /// <param name="node">Actual attach node transform in the part.</param>
    public PipeEndNode(Transform node) {
      this.node = node;
      sphere = null;
      arm = null;
    }

    /// <summary>Cleans up all the primitives.</summary>
    public void DestroyPrimitives() {
      if (sphere != null) {
        Destroy(sphere);
        sphere = null;
      }
      if (arm != null) {
        Destroy(arm);
        arm = null;
      }
    }

    /// <summary>Updates color and shader on the primitives.</summary>
    /// <remarks>Set parameters to <c>null</c> to have them <i>not</i> affected.</remarks>
    public void UpdateMaterial(Color? newColor = null, string newShaderName = null) {
      if (sphere != null) {
        Meshes.UpdateMaterials(sphere, newShaderName: newShaderName, newColor: newColor);
      }
      if (arm != null) {
        Meshes.UpdateMaterials(arm, newShaderName: newShaderName, newColor: newColor);
      }
    }
  }
  #endregion

  #region ILinkRenderer properties
  /// <inheritdoc/>
  public string cfgRendererName { get { return rendererName; } }

  /// <inheritdoc/>
  public Color? colorOverride {
    get { return _colorOverride; }
    set {
      _colorOverride = value;
      if (isStarted) {
        var newColor = _colorOverride ?? materialColor;
        sourceJointNode.UpdateMaterial(newColor: newColor);
        targetJointNode.UpdateMaterial(newColor: newColor);
        Meshes.UpdateMaterials(linkPipe, newColor: newColor);
      }
    }
  }
  Color? _colorOverride;

  /// <inheritdoc/>
  public string shaderNameOverride {
    get { return _shaderNameOverride; }
    set {
      _shaderNameOverride = value;
      if (isStarted) {
        var newShader = _shaderNameOverride ?? shaderName;
        sourceJointNode.UpdateMaterial(newShaderName: newShader);
        targetJointNode.UpdateMaterial(newShaderName: newShader);
        Meshes.UpdateMaterials(linkPipe, newShaderName: newShader);
      }
    }
  }
  string _shaderNameOverride;

  /// <inheritdoc/>
  public bool isPhysicalCollider {
    get { return false; }
    // disable once ValueParameterNotUsed
    set {
      // TODO(ihsoft): Support colliders if mode is enabled.
    }
  }

  /// <inheritdoc/>
  public bool isStarted { get { return linkPipe != null; } }

  /// <inheritdoc/>
  public Transform sourceTransform { get; private set; }

  /// <inheritdoc/>
  public Transform targetTransform { get; private set; }

  /// <inheritdoc/>
  //public float stretchRatio { get; set; }
  public float stretchRatio {
    get { return _stretchRatio; }
    set {
      _stretchRatio = value;
      //pipeTextureScaleRatio = 1 / _stretchRatio;
    }
  }
  float _stretchRatio = 1.0f;
  #endregion

  #region Part's config fields
  /// <summary>Config setting. See <see cref="cfgRendererName"/>.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public string rendererName = string.Empty;

  /// <summary>Config setting. Diameter of the pipe.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public float pipeDiameter = 0.15f;

  /// <summary>Config setting. Specifies how the source end of the pipe should be rounded.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso cref="sourceJointOffset"/>
  /// <seealso cref="sphereDiameter"/>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public PipeEndType sourceJointType = PipeEndType.Rounded;

  /// <summary>
  /// Config setting. Specifies how far above the attach node the source attach point is located.
  /// </summary>
  /// <remarks>
  /// Only makes sense when source end's type is <see cref="PipeEndType.RoundedWithOffset"/>.
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso cref="sourceTransform"/>
  /// <seealso cref="PipeEndType.RoundedWithOffset"/>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public float sourceJointOffset = 0f;

  /// <summary>Config setting. Specifies how the target end of the pipe should be rounded.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso cref="targetJointOffset"/>
  /// <seealso cref="sphereDiameter"/>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public PipeEndType targetJointType = PipeEndType.Rounded;

  /// <summary>
  /// Config setting. Specifies how far above the attach node the target attach point is located.
  /// </summary>
  /// <remarks>
  /// Only makes sense when target end's type is <see cref="PipeEndType.RoundedWithOffset"/>.
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso cref="targetTransform"/>
  /// <seealso cref="PipeEndType.RoundedWithOffset"/>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public float targetJointOffset = 0f;

  /// <summary>
  /// Config setting. Diameter of the rounding sphere for <see cref="PipeEndType.Rounded"/> mode.
  /// </summary>
  /// <remarks>
  /// Only makes sense when either source or target end type is configured for
  /// <see cref="PipeEndType.Rounded"/> or <see cref="PipeEndType.RoundedWithOffset"/>.
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso cref="targetTransform"/>
  /// <seealso cref="PipeEndType.Rounded"/>
  /// <seealso cref="PipeEndType.RoundedWithOffset"/>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public float sphereDiameter = 0.15f;

  /// <summary>
  /// Config setting. Texture to use for the pipe.
  /// </summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso cref="targetTransform"/>
  /// <seealso cref="PipeEndType.Rounded"/>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public string pipeTexturePath = "KAS-1.0/Textures/pipe";

  /// <summary>
  /// Config setting. Normals texture to use for the pipe. If empty string then no normals.
  /// </summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso cref="targetTransform"/>
  /// <seealso cref="PipeEndType.Rounded"/>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public string pipeNormalsTexturePath = "";

  /// <summary>
  /// Config setting. Defines how texture should cover the pipe.  
  /// </summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso cref="pipeTextureSamplesPerMeter"/>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public PipeTextureRescaleMode pipeTextureRescaleMode = PipeTextureRescaleMode.Stretch;

  /// <summary>
  /// Config setting. Defines how many texture samples to apply per one meter of pipe's length.
  /// </summary>
  /// This setting is ignored if texture rescale mode is
  /// <see cref="PipeTextureRescaleMode.Stretch"/>.
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso cref="pipeTextureRescaleMode"/>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public float pipeTextureSamplesPerMeter = 1f;
  #endregion

  #region Local properties
  /// <summary>The pipe mesh.</summary>
  GameObject linkPipe;

  /// <summary>Pipe's mesh renderer. Used to speedup updates that are done in every frame.</summary>
  Renderer linkPipeMR;

  /// <summary>Pipe ending at the source.</summary>
  PipeEndNode sourceJointNode;

  /// <summary>Pipe ending at the target.</summary>
  PipeEndNode targetJointNode;
  #endregion
  
  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnUpdate() {
    base.OnUpdate();
    if (isStarted) {
      UpdateLink();
    }
  }
  #endregion

  #region IsDestroyable implementation
  /// <inheritdoc/>
  public void OnDestroy() {
    StopRenderer();
  }
  #endregion

  #region AbstractProceduralModel abstract members
  /// <inheritdoc/>
  protected override void CreatePartModel() {
    // Nothing to do.
  }

  /// <inheritdoc/>
  protected override void LoadPartModel() {
    // Nothing to do.
  }
  #endregion

  #region ILinkRenderer implementation
  /// <inheritdoc/>
  public void StartRenderer(Transform source, Transform target) {
    if (isStarted) {
      Debug.LogWarning("Renderer already started. Stopping...");
      StopRenderer();
    }
    sourceTransform = source;
    targetTransform = target;
    var nrmTexture = pipeNormalsTexturePath != "" ? GetTexture(pipeNormalsTexturePath, asNormalMap: true) : null;
    var material = CreateMaterial(GetTexture(pipeTexturePath),
                                  normals: nrmTexture,
                                  overrideShaderName: shaderNameOverride,
                                  overrideColor: colorOverride);
    sourceJointNode = CreateJointNode(sourceJointType, material, source, sourceJointOffset);
    targetJointNode = CreateJointNode(targetJointType, material, target, targetJointOffset);
    linkPipe = Meshes.CreateCylinder(pipeDiameter, 1f, material, partModelTransform);
    linkPipeMR = linkPipe.GetComponent<Renderer>();  // To speedup OnUpdate() handling.
    SetupPipe(linkPipe.transform, sourceJointNode.position, targetJointNode.position);
    RescaleTextureToLength(linkPipe, renderer: linkPipeMR, scaleRatio: 1 / stretchRatio);
  }
  
  /// <inheritdoc/>
  public void StopRenderer() {
    if (isStarted) {
      sourceJointNode.DestroyPrimitives();
      targetJointNode.DestroyPrimitives();
      if (linkPipe != null) {
        Destroy(linkPipe);
        linkPipe = null;
        linkPipeMR = null;
      }
    }
  }

  /// <inheritdoc/>
  public void UpdateLink() {
    if (isStarted) {
      SetupPipe(linkPipe.transform, sourceJointNode.position, targetJointNode.position);
      if (pipeTextureRescaleMode != PipeTextureRescaleMode.Stretch) {
        RescaleTextureToLength(linkPipe, renderer: linkPipeMR, scaleRatio: 1 / stretchRatio);
      }
    }
  }

  /// <inheritdoc/>
  public string CheckColliderHits(Transform source, Transform target) {
    return null;  // There are no collider.
  }
  #endregion

  #region Local utility methods
  /// <summary>Creates internal joint node.</summary>
  /// <param name="type">Type of the pipe's ending.</param>
  /// <param name="material">Material for the new primitives.</param>
  /// <param name="node">Actual attach node transform in the part.</param>
  /// <param name="jointOffset">
  /// Offset for <see cref="PipeEndType.RoundedWithOffset"/> endings.
  /// </param>
  /// <returns>New ending structure.</returns>
  PipeEndNode CreateJointNode(PipeEndType type, Material material, Transform node,
                              float jointOffset = 0) {
    var res = new PipeEndNode(node);
    if (type != PipeEndType.Simple && jointOffset > Mathf.Epsilon) {
      res.sphere = Meshes.CreateSphere(sphereDiameter, material, node);
      RescaleTextureToLength(res.sphere);
      if (type == PipeEndType.RoundedWithOffset) {
        // Raise connection sphere over the node.
        res.sphere.transform.localPosition += new Vector3(0, 0, jointOffset);
        // Connect sphere and the node with a pipe.
        res.arm = Meshes.CreateCylinder(sphereDiameter, jointOffset, material, node);
        res.arm.transform.localPosition += new Vector3(0, 0, jointOffset / 2);
        SetupPipe(res.arm.transform, node.transform.position, res.sphere.transform.position);
        RescaleTextureToLength(res.arm);
      }
    }
    return res;
  }

  /// <summary>Adjusts texture on the object to fit rescale mode.</summary>
  /// <remarks>Primitive mesh is expected to be of base size 1m.</remarks>
  /// <param name="obj">Object to adjust texture for.</param>
  /// <param name="renderer">
  /// Optional renderer that owns the material. If not provided then renderer will be obtained via
  /// <c>GetComponent()</c> call which is rather expensive.
  /// </param>
  /// <param name="scaleRatio">Additional scale to apply to the pipe texture.</param>
  void RescaleTextureToLength(
      GameObject obj, Renderer renderer = null, float scaleRatio = 1.0f) {
    var newScale = obj.transform.localScale.z * pipeTextureSamplesPerMeter * scaleRatio;
    var mr = renderer ?? obj.GetComponent<Renderer>();
    mr.material.mainTextureScale = new Vector2(mr.material.mainTextureScale.x, newScale);
    if (pipeNormalsTexturePath != null) {
      var nrmScale = mr.material.GetTextureScale(BumpMapProp);
      mr.material.SetTextureScale(BumpMapProp, new Vector2(nrmScale.x, newScale));
    }
  }

  /// <summary>Ensures pipe mesh connect the specified positions.</summary>
  /// <param name="obj">Pipe's object.</param>
  /// <param name="fromPos">Position of the link source.</param>
  /// <param name="toPos">Position of the link target.</param>
  void SetupPipe(Transform obj, Vector3 fromPos, Vector3 toPos) {
    obj.position = (fromPos + toPos) / 2;
    if (pipeTextureRescaleMode == PipeTextureRescaleMode.TileFromTarget) {
      obj.LookAt(fromPos);
    } else {
      obj.LookAt(toPos);
    }
    obj.localScale =
        new Vector3(obj.localScale.x, obj.localScale.y, Vector3.Distance(fromPos, toPos));
  }
  #endregion
}

}  // namespace
