// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;
using KASAPIv2;
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
public class KASRendererPipe : AbstractPipeRenderer {

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
    [KASDebugAdjustable("Pipe type")]
    public PipeEndType type = PipeEndType.Simple;
    
    /// <summary>Height of the joint sphere over the attach node.</summary>
    /// <remarks>
    /// It can be negative to shift the "joint" point in the opposite direction.
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/>.
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("sphereOffset")]
    [KASDebugAdjustable("Sphere offset")]
    public float sphereOffset;

    /// <summary>Diameter of the joint sphere. It must be zero or positive.</summary>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/>.
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("sphereDiameter")]
    [KASDebugAdjustable("Sphere diameter")]
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
    [KASDebugAdjustable("Arm diameter")]
    public float armDiameter;

    /// <summary>Defines how the texture is tiled on the sphere and arm primitives.</summary>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/>.
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("textureSamplesPerMeter")]
    [KASDebugAdjustable("Texture samples per meter")]
    public float textureSamplesPerMeter = 1.0f;

    /// <summary>Texture to use to cover the arm and sphere primitives.</summary>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/>.
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("texture")]
    [KASDebugAdjustable("Main texture")]
    public string texture = "";

    /// <summary>Normals texture for the primitives. Can be omitted.</summary>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/>.
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("textureNrm")]
    [KASDebugAdjustable("Main texture normals")]
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
    [KASDebugAdjustable("Prefab model")]
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
    [KASDebugAdjustable("Prefab part attach pos&rot")]
    public PosAndRot partAttachAt = new PosAndRot();

    /// <summary>Setup of the node at which the node's model will attach to the pipe.</summary>
    /// <remarks>
    /// <para>Only used if <see cref="type"/> is <see cref="PipeEndType.PrefabModel"/>.</para>
    /// <para><i>IMPORTANT!</i> The position is affected by the prefab's scale.</para>
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.Types.PosAndRot']/*"/>
    [PersistentField("pipeAttachAt")]
    [KASDebugAdjustable("Prefab pipe attach pos&rot")]
    public PosAndRot pipeAttachAt = new PosAndRot();
  }
  #endregion

  #region Object names for the procedural model construction
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
    public readonly Transform rootModel;

    /// <summary>Transform at which the node attaches to the target part.</summary>
    /// <remarks>It's always a child of the main node's model.</remarks>
    public readonly Transform partAttach;

    /// <summary>Transform at which the node attaches to the pipe mesh.</summary>
    /// <remarks>It's always a child of the main node's model.</remarks>
    public readonly Transform pipeAttach;

    /// <summary>Creates a new attach node.</summary>
    /// <param name="rootModel">Model to use. It cannot be <c>null</c>.</param>
    public ModelPipeEndNode(Transform rootModel) {
      this.rootModel = rootModel;
      partAttach = GetTransformByName(PartJointTransformName);
      partAttach.parent = rootModel;  // Prefab can have different hierarchy.
      pipeAttach = GetTransformByName(PipeJointTransformName);
      pipeAttach.parent = rootModel;  // Prefab can have different hierarchy.
    }

    /// <summary>Aligns node against the target.</summary>
    /// <param name="target">
    /// The target object. Can be <c>null</c> in which case the node model will be hidden.
    /// </param>
    /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='M:KSPDev.AlignTransforms.SnapAlign']/*"/>
    public virtual void AlignTo(Transform target) {
      if (target != null) {
        AlignTransforms.SnapAlign(rootModel, partAttach, target);
        rootModel.gameObject.SetActive(true);
      } else {
        rootModel.gameObject.SetActive(false);
      }
    }

    /// <summary>
    /// Finds and returns the requested child model, or the main model if the child is not found.  
    /// </summary>
    /// <param name="name">Name of the child object to find.</param>
    /// <returns>Object or node's model itself if the child is not found.</returns>
    protected Transform GetTransformByName(string name) {
      var res = rootModel.Find(name);
      if (res == null) {
        HostedDebugLog.Error(rootModel, "Cannot find transform: {0}", name);
        res = rootModel;  // Fallback.
      }
      return res;
    }
  }
  #endregion

  #region Part's config settings loaded via ConfigAccessor
  /// <summary>Configuration of the source joint model.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
  [PersistentField("sourceJoint", group = StdPersistentGroups.PartConfigLoadGroup)]
  [KASDebugAdjustable("Source joint config")]
  public JointConfig sourceJointConfig = new JointConfig();

  /// <summary>Configuration of the target joint model.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
  [PersistentField("targetJoint", group = StdPersistentGroups.PartConfigLoadGroup)]
  [KASDebugAdjustable("Target joint config")]
  public JointConfig targetJointConfig = new JointConfig();
  #endregion

  #region Inheritable properties
  /// <summary>Pipe's mesh.</summary>
  /// <value>The root object the link mesh. <c>null</c> if the renderer is not started.</value>
  /// <seealso cref="CreateLinkPipe"/>
  protected Transform pipeTransform { get; private set; }

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
  protected Renderer pipeMeshRenderer { get; private set; }

  /// <summary>Pipe ending node at the source.</summary>
  /// <value>The source node container.</value>
  /// <seealso cref="LoadJointNode"/>
  protected ModelPipeEndNode sourceJointNode { get; private set; }

  /// <summary>Pipe ending node at the target.</summary>
  /// <value>The target node container.</value>
  /// <seealso cref="LoadJointNode"/>
  protected ModelPipeEndNode targetJointNode { get; private set; }
  #endregion

  #region AbstractPipeRenderer abstract members
  /// <inheritdoc/>
  protected override void CreatePartModel() {
  }

  /// <inheritdoc/>
  protected override void LoadPartModel() {
  }

  /// <inheritdoc/>
  protected override void CreatePipeMesh() {
    var sourceNodeModel = CreateJointEndModels(ModelBasename + "-sourceNode", sourceJointConfig);
    sourceNodeModel.parent = sourceTransform;
    sourceJointNode = new ModelPipeEndNode(sourceNodeModel);
    sourceJointNode.AlignTo(sourceTransform);
    var targetNodeModel = CreateJointEndModels(ModelBasename + "-targetNode", targetJointConfig);
    targetNodeModel.parent = sourceTransform;
    targetJointNode = new ModelPipeEndNode(targetNodeModel);
    targetJointNode.AlignTo(targetTransform);
    CreateLinkPipe();
    pipeTransform.parent = sourceTransform;
    // Have the overrides applied.
    colorOverride = colorOverride;
    shaderNameOverride = shaderNameOverride;
    isPhysicalCollider = isPhysicalCollider;
  }

  /// <inheritdoc/>
  protected override void DestroyPipeMesh() {
    if (isStarted) {
      Object.Destroy(sourceJointNode.rootModel.gameObject);
      sourceJointNode = null;
      Object.Destroy(targetJointNode.rootModel.gameObject);
      targetJointNode = null;
      DestroyLinkPipe();
    }
  }

  /// <inheritdoc/>
  public override void UpdateLink() {
    if (isStarted) {
      targetJointNode.AlignTo(targetTransform);
      SetupPipe(
          pipeTransform, sourceJointNode.pipeAttach.position, targetJointNode.pipeAttach.position);
      if (pipeTextureRescaleMode != PipeTextureRescaleMode.Stretch) {
        RescaleTextureToLength(pipeTransform, pipeTextureSamplesPerMeter, renderer: pipeMeshRenderer);
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
  /// <remarks>
  /// The procedural models are created as children to the part's model. The prefab models will have
  /// their parent re-defined to the part's model regardless to what was set in prefab.
  /// </remarks>
  /// <param name="modelName">
  /// The game object name. If this name is used downstream to retrieve the object, then it must be
  /// unique in scope of the part. Use <c>ModelBasename</c> to achieve it.
  /// </param>
  /// <param name="config">The joint configuration from the part's config.</param>
  /// <returns>The created object.</returns>
  /// <seealso cref="PipeEndType"/>
  /// <seealso cref="AbstractPipeRenderer.ModelBasename"/>
  protected virtual Transform CreateJointEndModels(string modelName, JointConfig config) {
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
      root = new GameObject(ModelBasename + "-pipe").transform;
      Hierarchy.MoveToParent(root, partModelTransform);
      var partJoint = new GameObject(PartJointTransformName).transform;
      Hierarchy.MoveToParent(partJoint, root,
                             newRotation: Quaternion.LookRotation(Vector3.back));
      if (config.type == PipeEndType.ProceduralModel) {
        // Create procedural models at the point where the pipe connects to the part's node.
        var material = CreateMaterial(
            GetTexture(config.texture), mainTexNrm: GetNormalMap(config.textureNrm));
        var offset = Mathf.Abs(config.sphereOffset);
        if (Mathf.Abs(config.sphereDiameter) > float.Epsilon) {
          var sphere = Meshes.CreateSphere(config.sphereDiameter, material, root,
                                           colliderType: Colliders.PrimitiveCollider.Shape);
          sphere.name = PipeJointTransformName;
          if (offset > float.Epsilon) {
            sphere.transform.localPosition = new Vector3(0, 0, config.sphereOffset);
          }
          sphere.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
          RescaleTextureToLength(sphere.transform,
                                 samplesPerMeter: config.textureSamplesPerMeter,
                                 extraScale: config.sphereDiameter);
        }
        if (offset > float.Epsilon) {
          if (config.armDiameter > float.Epsilon) {
            var arm = Meshes.CreateCylinder(
                config.armDiameter, offset, material, root,
                colliderType: Colliders.PrimitiveCollider.Shape);
            arm.transform.localPosition = new Vector3(0, 0, config.sphereOffset / 2);
            arm.transform.localRotation = Quaternion.LookRotation(Vector3.forward);
            RescaleTextureToLength(
                arm.transform, samplesPerMeter: config.textureSamplesPerMeter, extraScale: offset);
          }
        }
      } else {
        // No extra models are displayed at the joint, just attach the pipe to the part's node.
        if (config.type != PipeEndType.Simple) {
          // Normally, this error should never pop up.
          HostedDebugLog.Error(this, "Unknown joint type: {0}", config.type);
        }
        var pipeJoint = new GameObject(PipeJointTransformName);
        Hierarchy.MoveToParent(pipeJoint.transform, root,
                               newRotation: Quaternion.LookRotation(Vector3.forward));
      }
    }
    root.gameObject.SetActive(false);
    root.name = modelName;
    return root;
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
  /// Creates a mesh that represents the connecting pipe between the source and the target.
  /// </summary>
  /// <remarks>The models are created as children to the part's model.</remarks>
  protected virtual void CreateLinkPipe() {
    var material = CreateMaterial(
        GetTexture(pipeTexturePath), mainTexNrm: GetNormalMap(pipeNormalsTexturePath));
    pipeTransform = Meshes.CreateCylinder(
        pipeDiameter, 1.0f, material, partModelTransform,
        colliderType: Colliders.PrimitiveCollider.Shape).transform;
    CollisionManager.IgnoreCollidersOnVessel(
        vessel, pipeTransform.GetComponentsInChildren<Collider>());
    // TODO(ihsoft): Ignore the parts when migrated to the interfaces.
    Colliders.SetCollisionIgnores(sourceTransform.root, targetTransform.root, true);
    pipeMeshRenderer = pipeTransform.GetComponent<Renderer>();  // To speedup OnUpdate() handling.
    SetupPipe(pipeTransform.transform,
              sourceJointNode.pipeAttach.position, targetJointNode.pipeAttach.position);
    RescaleTextureToLength(pipeTransform, pipeTextureSamplesPerMeter, renderer: pipeMeshRenderer);
    // Let the part know about the new mesh so that it could be properly highlighted.
    part.RefreshHighlighter();
  }

  /// <summary>Destroys any meshes that represent a connection pipe.</summary>
  protected virtual void DestroyLinkPipe() {
    Object.Destroy(pipeTransform.gameObject);
    pipeTransform = null;
    pipeMeshRenderer = null;
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
  /// <param name="extraScale">
  /// The multiplier to add to the base scale. Normally, the method uses a scale from the object and
  /// the part, but if the mesh itself was scaled, then an extra scale may be needed to properly
  /// adjust the texture.
  /// </param>
  protected void RescaleTextureToLength(
      Transform obj, float samplesPerMeter, Renderer renderer = null, float extraScale = 1.0f) {
    var newScale = obj.localScale.z * samplesPerMeter / baseScale * extraScale;
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
