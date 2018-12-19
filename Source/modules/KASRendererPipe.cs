// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;
using KASAPIv2;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.Types;
using System;
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

    /// <summary>
    /// Defines the location where to attach the prefab model when the renderer is stopped. It's the
    /// location at the <i>source</i> part.
    /// </summary>
    /// <remarks>
    /// <para>Only used if <see cref="type"/> is <see cref="PipeEndType.PrefabModel"/>.</para>
    /// <para><i>IMPORTANT!</i> The position is affected by the prefab's scale.</para>
    /// <para>If it's <c>null</c>, then the model will be simply hidden on the renderer stop.</para>
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.Types.PosAndRot']/*"/>
    [PersistentField("parkAttachAt")]
    [KASDebugAdjustable("Park location pos&rot")]
    public PosAndRot parkAttachAt;
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

  /// <summary>Base name of the source node models</summary>
  /// <seealso cref="CreatePipeMesh"/>
  protected const string SourceNodeName = "Source";

  /// <summary>Base name of the target node models</summary>
  /// <seealso cref="CreatePipeMesh"/>
  protected const string TargetNodeName = "Target";
  #endregion

  #region Helper class for drawing a pipe's end
  /// <summary>Helper class for drawing a pipe's end.</summary>
  protected class ModelPipeEndNode {
    /// <summary>The main node's model.</summary>
    /// <remarks>All other objects of the node <i>must</i> be children to this model.</remarks>
    public Transform rootModel;

    /// <summary>Transform at which the node attaches to the target part.</summary>
    /// <remarks>It's always a child of the main node's model.</remarks>
    public Transform partAttach;

    /// <summary>Transform at which the node attaches to the pipe mesh.</summary>
    /// <remarks>It's always a child of the main node's model.</remarks>
    public Transform pipeAttach;

    /// <summary>Transform to attach the root model when the renderer stops.</summary>
    /// <remarks>
    /// It's always a child of the main node's model.
    /// <para>
    /// Can be <c>null</c>, in which case the root model will be deactivated instead of aligned to
    /// park location. Normally the alignment is done as "snap align" at the
    /// <see cref="pipeAttach"/>.
    /// </para>    
    /// </remarks>
    public Transform parkAttach;

    /// <summary>
    /// Tells if the root model is dynamic and needs to be cleaned up on renderer stop.
    /// </summary>
    public bool cleanupRoot;
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

  #region Inheritable fields & properties
  /// <summary>Pipe's mesh.</summary>
  /// <value><c>null</c> if the renderer is not started.</value>
  /// <seealso cref="CreateLinkPipe"/>
  protected Transform pipeTransform { get; private set; }

  /// <summary>Pipe's mesh renderer. Used to speedup the updates.</summary>
  /// <value>
  /// The mesh renderer object the link mesh. <c>null</c> if the renderer is not started.
  /// </value>
  /// <remarks>
  /// The pipe's mesh is updated in every frame. So, saving some performance by caching the
  /// components is in general a good thing to do.
  /// </remarks>
  /// <seealso cref="CreateLinkPipe"/>
  /// <seealso cref="UpdateLink"/>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Renderer']/*"/>
  protected Renderer pipeMeshRenderer { get; private set; }

  /// <summary>Pipe ending node at the source.</summary>
  /// <value>The source node container.</value>
  /// <seealso cref="CreateJointEndModels"/>
  protected ModelPipeEndNode sourceJointNode { get; private set; }

  /// <summary>Pipe ending node at the target.</summary>
  /// <value>The target node container.</value>
  /// <seealso cref="CreateJointEndModels"/>
  protected ModelPipeEndNode targetJointNode { get; private set; }
  #endregion

  #region AbstractPipeRenderer abstract members
  /// <inheritdoc/>
  protected override void CreatePartModel() {
  }

  /// <inheritdoc/>
  protected override void LoadPartModel() {
    if (sourceJointConfig.type == PipeEndType.PrefabModel) {
      var node = MakePrefabNode(SourceNodeName, sourceJointConfig);
      if (node != null) {
        if (node.parkAttach == null) {
          node.rootModel.gameObject.SetActive(false);
        } else  {
          node.rootModel.gameObject.SetActive(true);
          AlignTransforms.SnapAlign(node.rootModel, node.pipeAttach, node.parkAttach);
        }
      }
    }
    if (targetJointConfig.type == PipeEndType.PrefabModel) {
      var node = MakePrefabNode(TargetNodeName, targetJointConfig);
      if (node != null) {
        if (node.parkAttach == null) {
          node.rootModel.gameObject.SetActive(false);
        } else  {
          node.rootModel.gameObject.SetActive(true);
          AlignTransforms.SnapAlign(node.rootModel, node.pipeAttach, node.parkAttach);
        }
      }
    }
  }

  /// <inheritdoc/>
  protected override void CreatePipeMesh() {
    DestroyPipeMesh();
    sourceJointNode = CreateJointEndModels(SourceNodeName, sourceJointConfig);
    AlignTransforms.SnapAlign(
        sourceJointNode.rootModel, sourceJointNode.partAttach, sourceTransform);
    targetJointNode = CreateJointEndModels(TargetNodeName, targetJointConfig);
    AlignTransforms.SnapAlign(
        targetJointNode.rootModel, targetJointNode.partAttach, targetTransform);
    CreateLinkPipe();

    // Have the overrides applied if any.
    UpdateMaterialOverrides();
    UpdateColliderOverrides();
  }

  /// <inheritdoc/>
  protected override void DestroyPipeMesh() {
    if (sourceJointNode != null) {
      if (sourceJointNode.cleanupRoot) {
        UnityEngine.Object.Destroy(sourceJointNode.rootModel.gameObject);
      } else {
        sourceJointNode.rootModel.parent = partModelTransform;
        if (sourceJointNode.parkAttach != null) {
          AlignTransforms.SnapAlign(
              sourceJointNode.rootModel, sourceJointNode.pipeAttach, sourceJointNode.parkAttach);
          sourceJointNode.rootModel.gameObject.SetActive(true);
        } else {
          sourceJointNode.rootModel.gameObject.SetActive(false);
        }
      }
      sourceJointNode = null;
    }
    if (targetJointNode != null) {
      if (targetJointNode.cleanupRoot) {
        UnityEngine.Object.Destroy(targetJointNode.rootModel.gameObject);
      } else {
        targetJointNode.rootModel.parent = partModelTransform;
        if (targetJointNode.parkAttach != null) {
          AlignTransforms.SnapAlign(
              targetJointNode.rootModel, targetJointNode.pipeAttach, targetJointNode.parkAttach);
          targetJointNode.rootModel.gameObject.SetActive(true);
        } else {
          targetJointNode.rootModel.gameObject.SetActive(false);
        }
      }
      targetJointNode = null;
    }
    if (pipeTransform != null) {
      UnityEngine.Object.Destroy(pipeTransform.gameObject);
      pipeTransform = null;
    }
    pipeMeshRenderer = null;
  }

  /// <inheritdoc/>
  public override void UpdateLink() {
    if (isStarted) {
      AlignTransforms.SnapAlign(
          targetJointNode.rootModel, targetJointNode.partAttach, targetTransform);
      SetupPipe(
          pipeTransform, sourceJointNode.pipeAttach.position, targetJointNode.pipeAttach.position);
      if (pipeTextureRescaleMode != PipeTextureRescaleMode.Stretch) {
        RescaleTextureToLength(
            pipeTransform, pipeTextureSamplesPerMeter, renderer: pipeMeshRenderer);
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

  /// <inheritdoc/>
  protected override void UpdateMaterialOverrides() {
    var color = colorOverride ?? materialColor;
    var shader = shaderNameOverride ?? shaderName;
    if (isStarted) {
      Meshes.UpdateMaterials(pipeTransform.gameObject, newColor: color, newShaderName: shader);
      Meshes.UpdateMaterials(
          sourceJointNode.rootModel.gameObject, newColor: color, newShaderName: shader);
      Meshes.UpdateMaterials(
          targetJointNode.rootModel.gameObject, newColor: color, newShaderName: shader);
    } else {
      // Prefab nodes has models that always exist in the scene. 
      UpdatePrefabNode(
          SourceNodeName, sourceJointConfig,
          node => Meshes.UpdateMaterials(
              node.rootModel.gameObject, newColor: color, newShaderName: shader));
      UpdatePrefabNode(
          TargetNodeName, targetJointConfig,
          node => Meshes.UpdateMaterials(
              node.rootModel.gameObject, newColor: color, newShaderName: shader));
    }
  }

  /// <inheritdoc/>
  protected override void UpdateColliderOverrides() {
    if (isStarted) {
      Colliders.UpdateColliders(pipeTransform.gameObject, isEnabled: pipeColliderIsPhysical);
      Colliders.UpdateColliders(
          sourceJointNode.rootModel.gameObject, isEnabled: pipeColliderIsPhysical);
      Colliders.UpdateColliders(
          targetJointNode.rootModel.gameObject, isEnabled: pipeColliderIsPhysical);
    } else {
      // Prefab nodes has models that always exist in the scene. 
      UpdatePrefabNode(
          SourceNodeName, sourceJointConfig,
          node => Colliders.UpdateColliders(
              node.rootModel.gameObject, isEnabled: pipeColliderIsPhysical));
      UpdatePrefabNode(
          TargetNodeName, targetJointConfig,
          node => Colliders.UpdateColliders(
              node.rootModel.gameObject, isEnabled: pipeColliderIsPhysical));
    }
  }

  /// <inheritdoc/>
  protected override void SetCollisionIgnores(Part otherPart, bool ignore) {
    if (isStarted) {
      Colliders.SetCollisionIgnores(pipeTransform, otherPart.transform, ignore);
      Colliders.SetCollisionIgnores(sourceJointNode.rootModel, otherPart.transform, ignore);
      Colliders.SetCollisionIgnores(targetJointNode.rootModel, otherPart.transform, ignore);
    } else {
      // Prefab nodes has models that always exist in the scene.
      UpdatePrefabNode(
          SourceNodeName, sourceJointConfig,
          node => Colliders.SetCollisionIgnores(node.rootModel, otherPart.transform, ignore));
      UpdatePrefabNode(
          TargetNodeName, targetJointConfig,
          node => Colliders.SetCollisionIgnores(node.rootModel, otherPart.transform, ignore));
    }
  }
  #endregion

  #region Inheritable methods
  /// <summary>Builds a model for the joint end basing on the configuration.</summary>
  /// <remarks>
  /// The models are created as the children of <see cref="AbstractPipeRenderer.sourceTransform"/>.
  /// It applies to the prefab models as well, even though they are not dynamically created. So
  /// calling this method may change the models state.
  /// </remarks>
  /// <param name="name">
  /// The name of the node's root model to disambiguate the module's objects hierarchy. This name
  /// may not be used as is, the actual object can have a different full name.
  /// </param>
  /// <param name="config">The joint configuration from the part's config.</param>
  /// <returns>The pipe end node.</returns>
  /// <seealso cref="PipeEndType"/>
  protected virtual ModelPipeEndNode CreateJointEndModels(string name, JointConfig config) {
    ModelPipeEndNode res;
    switch (config.type) {
      case PipeEndType.PrefabModel:
        res = MakePrefabNode(name, config);
        if (res == null) {
          goto case PipeEndType.Simple;  // Fallback if no prefab found.
        }
        break;
      case PipeEndType.Simple:
        res = MakeSimpleNode(name);
        break;
      case PipeEndType.ProceduralModel:
        res = MakeProceduralNode(name, config);
        break;
      default:
        HostedDebugLog.Error(this, "Cannot create node of type: {0}", config.type);
        res = MakeSimpleNode(name);
        break;
    }
    res.rootModel.parent = sourceTransform;
    return res;
  }

  /// <summary>
  /// Creates a mesh that represents the connecting pipe between the source and the target.
  /// </summary>
  /// <remarks>It's only called in the started state.</remarks>
  /// <seealso cref="CreatePipeMesh"/>
  /// <seealso cref="pipeTransform"/>
  protected virtual void CreateLinkPipe() {
    pipeTransform = Meshes.CreateCylinder(
        pipeDiameter, 1.0f, pipeMaterial, sourceTransform,
        colliderType: Colliders.PrimitiveCollider.Shape).transform;
    pipeTransform.GetComponent<Renderer>().sharedMaterial = pipeMaterial;
    pipeMeshRenderer = pipeTransform.GetComponent<Renderer>();  // To speedup OnUpdate() handling.
    SetupPipe(pipeTransform.transform,
              sourceJointNode.pipeAttach.position, targetJointNode.pipeAttach.position);
    RescaleTextureToLength(pipeTransform, pipeTextureSamplesPerMeter, renderer: pipeMeshRenderer);
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

  /// <summary>Makes a node that doesn't have any meshes.</summary>
  /// <param name="name">
  /// The name of the node's root model to disambiguate the module's objects hierarchy. This name
  /// may not be used as is, the actual object can have a different full name.
  /// </param>
  /// <returns>The node.</returns>
  protected ModelPipeEndNode MakeSimpleNode(string name) {
    var res = new ModelPipeEndNode();
    res.rootModel = new GameObject(ModelBasename + "-pipeNode" + name).transform;
    res.partAttach = new GameObject(PartJointTransformName).transform;
    Hierarchy.MoveToParent(res.partAttach, res.rootModel,
                           newRotation: Quaternion.LookRotation(Vector3.back));
    res.pipeAttach = new GameObject(PipeJointTransformName).transform;
    Hierarchy.MoveToParent(res.pipeAttach, res.rootModel,
                           newRotation: Quaternion.LookRotation(Vector3.forward));
    res.cleanupRoot = true;
    return res;
  }

  /// <summary>Makes a node with the dynamically generated meshes.</summary>
  /// <param name="name">
  /// The name of the node's root model to disambiguate the module's objects hierarchy. This name
  /// may not be used as is, the actual object can have a different full name.
  /// </param>
  /// <param name="config">The configuration object.</param>
  /// <returns>The node or <c>null</c> if the prefab model cannot be found.</returns>
  protected ModelPipeEndNode MakeProceduralNode(string name, JointConfig config) {
    var res = new ModelPipeEndNode();
    res.rootModel = new GameObject(ModelBasename + "-pipeNode" + name).transform;
    res.partAttach = new GameObject(PartJointTransformName).transform;
    Hierarchy.MoveToParent(res.partAttach, res.rootModel,
                           newRotation: Quaternion.LookRotation(Vector3.back));
    var offset = Mathf.Abs(config.sphereOffset);
    if (Mathf.Abs(config.sphereDiameter) > float.Epsilon) {
      res.pipeAttach = Meshes.CreateSphere(
          config.sphereDiameter, pipeMaterial, res.rootModel,
          colliderType: Colliders.PrimitiveCollider.Shape).transform;
      res.pipeAttach.name = PipeJointTransformName;
      res.pipeAttach.GetComponent<Renderer>().sharedMaterial = pipeMaterial;  // For performance.
      RescaleTextureToLength(res.pipeAttach,
                             samplesPerMeter: pipeTextureSamplesPerMeter,
                             extraScale: config.sphereDiameter * 2.0f);
    } else {
      res.pipeAttach = new GameObject(PipeJointTransformName).transform;
      Hierarchy.MoveToParent(res.pipeAttach , res.rootModel);
    }
    res.pipeAttach.localPosition = new Vector3(0, 0, config.sphereOffset);
    res.pipeAttach.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
    if (offset > float.Epsilon) {
      if (Mathf.Abs(config.armDiameter) > float.Epsilon) {
        var arm = Meshes.CreateCylinder(
            config.armDiameter, offset, pipeMaterial, res.rootModel,
            colliderType: Colliders.PrimitiveCollider.Shape);
        arm.GetComponent<Renderer>().sharedMaterial = pipeMaterial;  // For performance.
        arm.transform.localPosition = new Vector3(0, 0, config.sphereOffset / 2);
        arm.transform.localRotation = Quaternion.LookRotation(Vector3.forward);
        RescaleTextureToLength(
            arm.transform, samplesPerMeter: pipeTextureSamplesPerMeter, extraScale: offset);
      }
    }
    res.cleanupRoot = true;
    return res;
  }

  /// <summary>Makes a node with the meshes from prefab.</summary>
  /// <remarks>The prefab model must be a child of the part's model.</remarks>
  /// <param name="name">
  /// The name of the node's root model to disambiguate the module's objects hierarchy. This name
  /// may not be used as is, the actual object can have a different full name.
  /// </param>
  /// <param name="config">The configuration object.</param>
  /// <returns>The node or <c>null</c> if the prefab model cannot be found.</returns>
  protected ModelPipeEndNode MakePrefabNode(string name, JointConfig config) {
    var res = new ModelPipeEndNode();
    res.rootModel = Hierarchy.FindTransformByPath(partModelTransform, config.modelPath);
    if (res.rootModel != null) {
      res.rootModel.gameObject.SetActive(true);
      res.partAttach = res.rootModel.Find(PartJointTransformName)
          ?? new GameObject(PartJointTransformName).transform;
      Hierarchy.MoveToParent(res.partAttach, res.rootModel,
                             newPosition: config.partAttachAt.pos,
                             newRotation: config.partAttachAt.rot);
      res.pipeAttach = res.rootModel.Find(PipeJointTransformName) 
          ?? new GameObject(PipeJointTransformName).transform;
      Hierarchy.MoveToParent(res.pipeAttach, res.rootModel,
                             newPosition: config.pipeAttachAt.pos,
                             newRotation: config.pipeAttachAt.rot);
      if (config.parkAttachAt != null) {
        var parkObjectName = ModelBasename + "-park" + name;
        res.parkAttach = partModelTransform.Find(parkObjectName)
            ?? new GameObject(parkObjectName).transform;
        Hierarchy.MoveToParent(res.parkAttach, partModelTransform,
                               newPosition: config.parkAttachAt.pos,
                               newRotation: config.parkAttachAt.rot);
      }
    } else {
      HostedDebugLog.Error(this, "Cannot find model: {0}", config.modelPath);
      return null;
    }
    return res;
  }
  #endregion

  #region Local utility methods
  /// <summary>Fires a callabck of the node config if it refers prefab model.</summary>
  /// <param name="nodeName">The hierarchy obejct name.</param>
  /// <param name="config">The node config.</param>
  /// <param name="actionFn">The callback to call if the ndoe model is prefab.</param>
  void UpdatePrefabNode(string nodeName, JointConfig config, Action<ModelPipeEndNode> actionFn) {
    if (config.type == PipeEndType.PrefabModel) {
      var node = MakePrefabNode(nodeName, config);
      if (node != null) {
        actionFn(node);
      }
    }
  }
  #endregion
}

}  // namespace
