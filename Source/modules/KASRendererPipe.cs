// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;
using KASAPIv2;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.Types;
using UnityEngine;

namespace KAS {

/// <summary>Module that draws a pipe between two nodes.</summary>
/// <remarks>
/// Usually, the renderer is started or stopped by a link source. However, it can be any module.  
/// <para>
/// Pipe ends can be constructed differently:
/// <list type="table">
/// <item>
/// <term><see cref="PipeEndType.Simple"/></term>
/// <description>
/// A simple cylinder is drawn between the nodes. The ends of the pipe may look ugly at the large
/// angles if they are not "sunken" into another mesh.
/// </description>
/// </item>
/// <item>
/// <term><see cref="PipeEndType.ProceduralModel"/></term>
/// <description>
/// A sphere is drawn at the end of the pipe. If sphere diameter matches the pipe's diameter then
/// the pipe gets a capsule form. However, the sphere is not required to have the same diameter.
/// This mode is good for the cases when the attach node is located on the surface of the part.
/// <para>
/// There is an option to raise the connection point over the attach node. In this case a simple
/// cylinder (the "arm") is drawn between the attach node and the connection sphere.
/// </para>
/// </description>
/// </item>
/// <item>
/// <term><see cref="PipeEndType.PrefabModel"/></term>
/// <description>
/// A model form the part's prefab is used to draw the end of the pipe. No extra actions are made at
/// the pipe's connect, so the model must be appropriately setup to make this joint looking cute.
/// </description>
/// </item>
/// </list>
/// </para>
/// <para>
/// The descendants of this module can use the custom persistent fields of groups:
/// </para>
/// <list type="bullet">
/// <item><c>StdPersistentGroups.PartConfigLoadGroup</c></item>
/// <item><c>StdPersistentGroups.PartPersistant</c></item>
/// </list>
/// </remarks>
/// <seealso cref="KASAPIv1.ILinkSource"/>
/// <seealso cref="KASAPIv1.ILinkRenderer"/>
/// <seealso cref="PipeEndType"/>
/// <seealso cref="JointConfig"/>
/// <seealso href="http://ihsoft.github.io/KSPDev/Utils/html/M_KSPDev_ConfigUtils_ConfigAccessor_ReadPartConfig.htm"/>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.PersistentFieldAttribute']/*"/>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.StdPersistentGroups']/*"/>
public class KASRendererPipe : AbstractPipeRenderer,
    // KPSDev sugar interfaces.    
    IsDestroyable {

  #region Public config types
  /// <summary>Type if the end of the pipe.</summary>
  public enum PipeEndType {
    /// <summary>The pipe's mesh just touches the target's attach node.</summary>
    /// <remarks>
    /// It looks ugly on the large pipe diameters but may be fine when the diameter is not
    /// significant. The problem can be mitigated by "sinking" the node into the part's mesh.
    /// </remarks>
    Simple,
    /// <summary>Pipe's end is a model that is dynamically created.</summary>
    /// <remarks>
    /// <para>
    /// A sphere mesh is rendered at the point where the pipe's mesh touches the target part's
    /// attach node. The sphere diameter can be adjusted, and if it's equal or greater than the
    /// diameter of the pipe then the joint looks smoother.
    /// </para>
    /// <para>
    /// The actual connection point for the pipe mesh can be elevated over the target attach node.
    /// In this case a simple cylinder, the "arm", can be rendered between the part and the joint
    /// sphere. The diameter and the height of the arm can be adjusted.
    /// </para>
    /// </remarks>
    /// <seealso cref="JointConfig"/>
    ProceduralModel,
    /// <summary>Pipe's end model is defined in the part's prefab.</summary>
    /// <remarks>
    /// The model must exist in the part's prefab. Also, some extra settings need to be setup to
    /// tell how to align the model against the target part.
    /// </remarks>
    /// <seealso cref="JointConfig"/>
    PrefabModel,
  }

  /// <summary>Helper structure to hold the joint model setup.</summary>
  /// <seealso cref="KASRendererPipe"/>
  public class JointConfig {
    /// <summary>Defines how to obtain the joint model.</summary>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("type")]
    public PipeEndType type = PipeEndType.Simple;
    
    /// <summary>Defines if model's should trigger physical effects on collision.</summary>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/> or
    /// <see cref="PipeEndType.PrefabModel"/>. If the prefab models are used then the colliders must
    /// be existing in the model. If there are none then this settings doesn't have effect.
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("colliderIsPhysical")]
    public bool colliderIsPhysical;

    /// <summary>Height of the joint sphere over the attach node.</summary>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/>.
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("sphereOffset")]
    public float sphereOffset;

    /// <summary>Diameter of the joint sphere. It must be zero or positive.</summary>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/>.
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("sphereDiameter")]
    public float sphereDiameter;

    /// <summary>
    /// Diameter of the pipe that connects the attach node and the sphere. It must be zero or
    /// positive.
    /// </summary>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/> and
    /// <see cref="sphereOffset"/> is greater than zero.
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("armDiameter")]
    public float armDiameter;

    /// <summary>Defines how the texture is tiled on the sphere and arm primitives.</summary>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/>.
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("textureSamplesPerMeter")]
    public float textureSamplesPerMeter = 1.0f;

    /// <summary>Texture to use to cover the arm and sphere primitives.</summary>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/>.
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("texture")]
    public string texture = "";

    /// <summary>Normals texture for the primitives. Can be omitted.</summary>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/>.
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("textureNrm")]
    public string textureNrm = "";

    /// <summary>Path to the model that represents the joint.</summary>
    /// <remarks>
    /// <para>Only used if <see cref="type"/> is <see cref="PipeEndType.PrefabModel"/>.</para>
    /// <para>
    /// Note, that the model will be "consumed". I.e. the internal logic may change the name of the
    /// prefab within the part's model, extend it with more objects, or destroy it altogether. If
    /// the same model is needed for the other purposes, add a copy via a <c>MODEL</c> tag in the
    /// part's config.
    /// </para>
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='M:KSPDev.Hierarchy.FindTransformByPath']/*"/>
    [PersistentField("model")]
    public string modelPath = "";

    /// <summary>
    /// Setup of the node at which the node's model will attach to the target part.
    /// </summary>
    /// <remarks>
    /// <para>Only used if <see cref="type"/> is <see cref="PipeEndType.PrefabModel"/>.</para>
    /// <para><i>IMPORTANT!</i> The position is affected by the prefab's scale.</para>
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.Types.PosAndRot']/*"/>
    [PersistentField("partAttachAt")]
    public PosAndRot partAttachAt = new PosAndRot();

    /// <summary>Setup of the node at which the node's model will attach to the pipe.</summary>
    /// <remarks>
    /// <para>Only used if <see cref="type"/> is <see cref="PipeEndType.PrefabModel"/>.</para>
    /// <para><i>IMPORTANT!</i> The position is affected by the prefab's scale.</para>
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.Types.PosAndRot']/*"/>
    [PersistentField("pipeAttachAt")]
    public PosAndRot pipeAttachAt = new PosAndRot();
  }
  #endregion

  #region Object names for the procedural model construction
  /// <summary>Name of the node's model for the end that attaches to the source part.</summary>
  protected string ProceduralSourceJointObjectName {
    get {
      return "$sourceJointEnd-" + rendererName;
    }
  }

  /// <summary>Name of the node's model for the end that attaches to the target part.</summary>
  protected string ProceduralTargetJointObjectName {
    get {
      return "$targetJointEnd-" + rendererName;
    }
  }

  /// <summary>
  /// Name of the object in the node's model that defines where it attaches to the part.
  /// </summary>
  /// <remarks>
  /// The source part's attach node attaches to the pipe at the point, defined by this object.
  /// The part's attach node and the are oriented so that their directions look at each other. 
  /// </remarks>
  protected const string PartJointTransformName = "partAttach";

  /// <summary>
  /// Name of the object in the node's model that defines where it attaches to the pipe mesh.
  /// </summary>
  /// <remarks>This object looks in the direction of the pipe, towards the other end.</remarks>
  protected const string PipeJointTransformName = "pipeAttach";
  #endregion

  #region Helper class for drawing a pipe's end
  /// <summary>Helper class for drawing a pipe's end.</summary>
  /// <seealso cref="KASRendererPipe"/>
  protected class ModelPipeEndNode {
    /// <summary>The main node's model.</summary>
    /// <remarks>All other objects of the node <i>must</i> be children to this model.</remarks>
    public readonly Transform model;

    /// <summary>Transform at which the node attaches to the target part.</summary>
    /// <remarks>It's always a child of the main node's model.</remarks>
    public readonly Transform partAttach;

    /// <summary>Transform at which the node attaches to the pipe mesh.</summary>
    /// <remarks>It's always a child of the main node's model.</remarks>
    public readonly Transform pipeAttach;

    /// <summary>Creates a new attach node.</summary>
    /// <param name="model">Model to use. It cannot be <c>null</c>.</param>
    public ModelPipeEndNode(Transform model) {
      this.model = model;
      partAttach = GetTransformByName(PartJointTransformName);
      partAttach.parent = model;  // Prefab can have different hierarchy.
      pipeAttach = GetTransformByName(PipeJointTransformName);
      pipeAttach.parent = model;  // Prefab can have different hierarchy.
    }

    /// <summary>Aligns node against the target.</summary>
    /// <param name="target">
    /// The target object. Can be <c>null</c> in which case the node model will be hidden.
    /// </param>
    /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='M:KSPDev.AlignTransforms.SnapAlign']/*"/>
    public virtual void AlignTo(Transform target) {
      if (target != null) {
        AlignTransforms.SnapAlign(model, partAttach, target);
        model.gameObject.SetActive(true);
      } else {
        model.gameObject.SetActive(false);
      }
    }

    /// <summary>Updates the material settings on the model meshes.</summary>
    /// <param name="newColor">New color.</param>
    /// <param name="newShaderName">New shader name.</param>
    public virtual void UpdateMaterial(Color? newColor = null, string newShaderName = null) {
      Meshes.UpdateMaterials(model.gameObject, newShaderName: newShaderName, newColor: newColor);
    }

    /// <summary>Turns model's collider(s) on/off.</summary>
    /// <param name="isEnabled">The new state.</param>
    public virtual void SetColliderEnabled(bool isEnabled) {
      Colliders.UpdateColliders(model.gameObject, isEnabled: isEnabled);
    }

    /// <summary>
    /// Finds and returns the requested child model, or the main model if the child is not found.  
    /// </summary>
    /// <param name="name">Name of the child object to find.</param>
    /// <returns>Object or node's model itself if the child is not found.</returns>
    protected Transform GetTransformByName(string name) {
      var res = model.Find(name);
      if (res == null) {
        HostedDebugLog.Error(model, "Cannot find transform: {0}", name);
        res = model;  // Fallback.
      }
      return res;
    }
  }
  #endregion

  #region Part's config settings loaded via ConfigAccessor
  /// <summary>Configuration of the source joint model.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
  [PersistentField("sourceJoint", group = StdPersistentGroups.PartConfigLoadGroup)]
  public JointConfig sourceJointConfig = new JointConfig();

  /// <summary>Configuration of the target joint model.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
  [PersistentField("targetJoint", group = StdPersistentGroups.PartConfigLoadGroup)]
  public JointConfig targetJointConfig = new JointConfig();
  #endregion

  #region Inheritable properties
  /// <summary>Pipe's mesh.</summary>
  /// <value>The root object the link mesh. <c>null</c> if the renderer is not started.</value>
  /// <seealso cref="CreateLinkPipe"/>
  protected GameObject linkPipe { get; private set; }

  /// <summary>Pipe's mesh renderer. Used to speedup the updates.</summary>
  /// <value>
  /// The mesh renderer object the link mesh. <c>null</c> if the renderer is not started.
  /// </value>
  /// <remarks>
  /// The pipe's mesh is updated in every farme. So, saving some performance by caching the 
  /// components is in general a good thing to do.
  /// </remarks>
  /// <seealso cref="CreateLinkPipe"/>
  /// <seealso cref="UpdateLink"/>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Renderer']/*"/>
  protected Renderer linkPipeMR { get; private set; }

  /// <summary>Pipe ending node at the source.</summary>
  /// <value>The source node container.</value>
  /// <seealso cref="LoadJointNode"/>
  protected ModelPipeEndNode sourceJointNode { get; private set; }

  /// <summary>Pipe ending node at the target.</summary>
  /// <value>The target node container.</value>
  /// <seealso cref="LoadJointNode"/>
  protected ModelPipeEndNode targetJointNode { get; private set; }

  /// <summary>The scale of the part models.</summary>
  /// <remarks>
  /// The scale of the part must be "even", i.e. all the components in the scale vector must be
  /// equal. If they are not, then the renderer's behavior may be inconsistent.
  /// </remarks>
  /// <value>The scale to be applied to all the components.</value>
  protected float baseScale {
    get {
      if (_baseScale < 0) {
        var scale = partModelTransform.lossyScale;
        if (Mathf.Abs(scale.x - scale.y) > 1e-05 || Mathf.Abs(scale.x - scale.z) > 1e-05) {
          HostedDebugLog.Error(this, "Uneven part scale is not supported: {0}",
                               DbgFormatter.Vector(scale));
        }
        _baseScale = scale.x;
      }
      return _baseScale;
    }
  }
  float _baseScale = -1;
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    ConfigAccessor.ReadPartConfig(this, node);
    // For the procedural and simple modes use the hardcoded model names.
    if (sourceJointConfig.type != PipeEndType.PrefabModel) {
      sourceJointConfig.modelPath = ProceduralSourceJointObjectName;
    }
    if (targetJointConfig.type != PipeEndType.PrefabModel) {
      targetJointConfig.modelPath = ProceduralTargetJointObjectName;
    }
    base.OnLoad(node);
  }
  #endregion

  #region IsDestroyable implementation
  /// <inheritdoc/>
  public virtual void OnDestroy() {
    StopRenderer();
  }
  #endregion

  #region AbstractProceduralModel abstract members
  /// <inheritdoc/>
  protected override void CreatePartModel() {
    CreateJointEndModels(ProceduralSourceJointObjectName, sourceJointConfig);
    CreateJointEndModels(ProceduralTargetJointObjectName, targetJointConfig);
    sourceJointNode = LoadJointNode(ProceduralSourceJointObjectName);
    targetJointNode = LoadJointNode(ProceduralTargetJointObjectName);
  }

  /// <inheritdoc/>
  protected override void LoadPartModel() {
    sourceJointNode = LoadJointNode(ProceduralSourceJointObjectName);
    targetJointNode = LoadJointNode(ProceduralTargetJointObjectName);
  }
  #endregion

  #region AbstractPipeRenderer abstract members
  /// <inheritdoc/>
  protected override void CreatePipeMesh() {
    sourceJointNode.AlignTo(sourceTransform);
    sourceJointNode.UpdateMaterial(newShaderName: shaderNameOverride, newColor: colorOverride);
    targetJointNode.AlignTo(targetTransform);
    targetJointNode.UpdateMaterial(newShaderName: shaderNameOverride, newColor: colorOverride);
    CreateLinkPipe();
    isPhysicalCollider = isPhysicalCollider;  // Update the status.
  }

  /// <inheritdoc/>
  protected override void DestroyPipeMesh() {
    if (isStarted) {
      sourceJointNode.AlignTo(null);
      targetJointNode.AlignTo(null);
      DestroyLinkPipe();
    }
  }

  /// <inheritdoc/>
  public override void UpdateLink() {
    if (isStarted) {
      targetJointNode.AlignTo(targetTransform);
      SetupPipe(linkPipe.transform,
                sourceJointNode.pipeAttach.position, targetJointNode.pipeAttach.position);
      if (pipeTextureRescaleMode != PipeTextureRescaleMode.Stretch) {
        RescaleTextureToLength(linkPipe, pipeTextureSamplesPerMeter, renderer: linkPipeMR);
      }
    }
  }

  /// <inheritdoc/>
  protected override Vector3[] GetPipePath(Transform start, Transform end) {
    // TODO(ihsoft): Implement a full check that includes the pipe ends as well.
    return isPhysicalCollider
        ? new[] { start.position, end.position }
        : new Vector3[0];
  }
  #endregion

  #region Inheritable utility methods
  /// <summary>Builds a model for the joint end basing on the configuration.</summary>
  /// <param name="modelName">The joint transform name.</param>
  /// <param name="config">The joint configuration from the part's config.</param>
  protected virtual void CreateJointEndModels(string modelName, JointConfig config) {
    // FIXME: Prefix the model name with the renderer name.
    // Make or get the root.
    Transform root = null;
    if (config.type == PipeEndType.PrefabModel) {
      root = Hierarchy.FindTransformByPath(partModelTransform, config.modelPath);
      if (root != null) {
        root.parent = partModelTransform;  // We need the part's model to be the root.
        var partAttach = new GameObject(PartJointTransformName).transform;
        Hierarchy.MoveToParent(partAttach, root,
                               newPosition: config.partAttachAt.pos,
                               newRotation: config.partAttachAt.rot);
        var pipeAttach = new GameObject(PipeJointTransformName);
        Hierarchy.MoveToParent(pipeAttach.transform, root,
                               newPosition: config.pipeAttachAt.pos,
                               newRotation: config.pipeAttachAt.rot);
      } else {
        HostedDebugLog.Error(this, "Cannot find model: {0}", config.modelPath);
        config.type = PipeEndType.Simple;  // Fallback.
      }
    }
    if (root == null) {
      root = new GameObject().transform;
      Hierarchy.MoveToParent(root, partModelTransform);
      var partJoint = new GameObject(PartJointTransformName).transform;
      Hierarchy.MoveToParent(partJoint, root);
      partJoint.rotation = Quaternion.LookRotation(Vector3.forward);
      if (config.type == PipeEndType.ProceduralModel) {
        // Create procedural models at the point where the pipe connects to the part's node.
        var material = CreateMaterial(
            GetTexture(config.texture), mainTexNrm: GetNormalMap(config.textureNrm));
        var sphere = Meshes.CreateSphere(config.sphereDiameter, material, root,
                                         colliderType: Colliders.PrimitiveCollider.Shape);
        sphere.name = PipeJointTransformName;
        sphere.transform.rotation = Quaternion.LookRotation(Vector3.back);
        RescaleTextureToLength(sphere, samplesPerMeter: config.textureSamplesPerMeter);
        if (Mathf.Abs(config.sphereOffset) > float.Epsilon) {
          sphere.transform.localPosition += new Vector3(0, 0, config.sphereOffset);
          if (config.armDiameter > float.Epsilon) {
            var arm = Meshes.CreateCylinder(
                config.armDiameter, config.sphereOffset, material, root,
                colliderType: Colliders.PrimitiveCollider.Shape);
            arm.transform.localPosition += new Vector3(0, 0, config.sphereOffset / 2);
            arm.transform.LookAt(sphere.transform.position);
            RescaleTextureToLength(arm, samplesPerMeter: config.textureSamplesPerMeter);
          }
        }
      } else {
        // No extra models are displayed at the joint, just attach the pipe to the part's node.
        if (config.type != PipeEndType.Simple) {
          // Normally, this error should never pop up.
          HostedDebugLog.Error(this, "Unknown joint type: {0}", config.type);
        }
        var pipeJoint = new GameObject(PipeJointTransformName);
        Hierarchy.MoveToParent(pipeJoint.transform, root);
        pipeJoint.transform.rotation = Quaternion.LookRotation(Vector3.back);
      }
    }
    Colliders.UpdateColliders(root.gameObject, isPhysical: config.colliderIsPhysical);
    root.gameObject.SetActive(false);
    root.name = modelName;
  }

  /// <summary>Constructs a joint node for the requested config.</summary>
  /// <param name="modelName">Name of the model in the hierarchy.</param>
  /// <returns>Pipe's end node.</returns>
  protected virtual ModelPipeEndNode LoadJointNode(string modelName) {
    var node = new ModelPipeEndNode(partModelTransform.Find(modelName));
    node.AlignTo(null);  // Init mode objects state.
    return node;
  }
  
  /// <summary>
  /// Creates and displays a mesh that represents a connecting pipe between the source and the
  /// target parts.
  /// </summary>
  protected virtual void CreateLinkPipe() {
    var material = CreateMaterial(GetTexture(pipeTexturePath),
                                  mainTexNrm: GetNormalMap(pipeNormalsTexturePath),
                                  overrideShaderName: shaderNameOverride,
                                  overrideColor: colorOverride);
    linkPipe = Meshes.CreateCylinder(pipeDiameter, 1f, material, partModelTransform,
                                     colliderType: Colliders.PrimitiveCollider.Shape);
    Colliders.UpdateColliders(linkPipe, isPhysical: pipeColliderIsPhysical);
    if (pipeColliderIsPhysical) {
      CollisionManager.IgnoreCollidersOnVessel(
          vessel, linkPipe.GetComponentsInChildren<Collider>());
      // TODO(ihsoft): Ignore the parts when migrated to the interfaces.
      Colliders.SetCollisionIgnores(sourceTransform.root, targetTransform.root, true);
    }
    linkPipeMR = linkPipe.GetComponent<Renderer>();  // To speedup OnUpdate() handling.
    SetupPipe(linkPipe.transform,
              sourceJointNode.pipeAttach.position, targetJointNode.pipeAttach.position);
    RescaleTextureToLength(linkPipe, pipeTextureSamplesPerMeter, renderer: linkPipeMR);
    // Let the part know about the new mesh so that it could be properly highlighted.
    part.RefreshHighlighter();
  }

  /// <summary>Destroys any meshes that represent a connection pipe.</summary>
  protected virtual void DestroyLinkPipe() {
    Destroy(linkPipe);
    linkPipe = null;
    linkPipeMR = null;
  }

  /// <summary>Ensures that the pipe's mesh connects the specified positions.</summary>
  /// <param name="obj">Pipe's object.</param>
  /// <param name="fromPos">Position of the link source.</param>
  /// <param name="toPos">Position of the link target.</param>
  protected virtual void SetupPipe(Transform obj, Vector3 fromPos, Vector3 toPos) {
    obj.position = (fromPos + toPos) / 2;
    if (pipeTextureRescaleMode == PipeTextureRescaleMode.TileFromTarget) {
      obj.LookAt(fromPos);
    } else {
      obj.LookAt(toPos);
    }
    obj.localScale = new Vector3(
        obj.localScale.x, obj.localScale.y, Vector3.Distance(fromPos, toPos) / baseScale);
  }
  #endregion

  #region Utility methods
  /// <summary>Adjusts texture on the object to fit rescale mode.</summary>
  /// <remarks>Primitive mesh is expected to be of base size 1m.</remarks>
  /// <param name="obj">Object to adjust texture for.</param>
  /// <param name="samplesPerMeter">
  /// Number fo texture samples per a meter of the linear size.
  /// </param>
  /// <param name="renderer">
  /// Optional renderer that owns the material. If not provided then renderer will be obtained via
  /// a <c>GetComponent()</c> call which is rather expensive.
  /// </param>
  protected void RescaleTextureToLength(
      GameObject obj, float samplesPerMeter, Renderer renderer = null) {
    var newScale = obj.transform.localScale.z * samplesPerMeter / baseScale;
    var mr = renderer ?? obj.GetComponent<Renderer>();
    mr.material.mainTextureScale = new Vector2(mr.material.mainTextureScale.x, newScale);
    if (mr.material.HasProperty(BumpMapProp)) {
      var nrmScale = mr.material.GetTextureScale(BumpMapProp);
      mr.material.SetTextureScale(BumpMapProp, new Vector2(nrmScale.x, newScale));
    }
  }
  #endregion
}

}  // namespace
