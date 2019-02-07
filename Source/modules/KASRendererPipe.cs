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
/// At each end of the pipe a model can be drawn to make the connection look nicer, it's
/// configured separately for the pipe source and target. If nothing is configured, then the pipe
/// (which is a cylinder mesh) simply toches the attach nodes of the parts. If the pipe diameter
/// is big, then it may look bad since the edges of the cylinder won't mix nicely with the part
/// meshes.
/// </para>
/// <para>
/// One way to improve appearance is adding a sphere mesh of the same or bigger diameter at the
/// location where pipe touches the part. This way the cylinder edges will "sink" in the spheres.
/// The sphere diameter can be set via <c>sphereDiameter</c> setting.
/// </para>
/// <para>
/// By default, the sphere is placed at the part's mesh (depending on how the part's attach node
/// is configured, actually). If it needs to be offset above or below of the default position, the
/// <c>sphereOffset</c> setting can be used.
/// </para>
/// <para>
/// If the sphere is offset above the part's mesh, there may be desirable to simulate a small
/// piece of pipe between the part's mesh and the sphere. This can be done by defining pipe diameter
/// via <c>armDiameter</c>.
/// </para>
/// <para>
/// Finally, a complete prefab model can be inserted! This model will be inserted between the part
/// and the sphere. The model path is defined via <c>model</c> setting. To properly orient the
/// model, two extra parameters are needed: <c>modelPartAttachAt</c>, which defines how the model
/// attches to the part; and <c>modelPipeAttachAt</c>, which defines where the pipe attaches to the
/// model. If sphere or offsets were set, they will be counter relative to <c>modelPipeAttachAt</c>. 
/// </para>
/// <para>
/// Normally, the pipe models are shown and hidden depending on the state the pipe. However, it's
/// possible to define a static position where the model(s) will be placed when the renderer is
/// stopped. This is done via setting <c>parkAttachAt</c>.
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
/// <seealso cref="JointConfig"/>
/// <seealso href="http://ihsoft.github.io/KSPDev/Utils/html/M_KSPDev_ConfigUtils_ConfigAccessor_ReadPartConfig.htm"/>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.PersistentFieldAttribute']/*"/>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.StdPersistentGroups']/*"/>
public class KASRendererPipe : AbstractPipeRenderer {

  #region Public config types
  /// <summary>Helper structure to hold the joint model setup.</summary>
  /// <seealso cref="KASRendererPipe"/>
  public class JointConfig {
    /// <summary>Offset of the pipe joint relative to the attach node.</summary>
    /// <remarks>
    /// It can be negative to shift the "joint" point in the opposite direction. If prefab model is 
    /// defined, then the offset is counted relative to <see cref="modelPipeAttachAt"/>.
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("sphereOffset")]
    [KASDebugAdjustable("Sphere offset")]
    public float sphereOffset;

    /// <summary>Diameter of the sphere to place at the pipe joint.</summary>
    /// <remarks>It must be zero or positive.</remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("sphereDiameter")]
    [KASDebugAdjustable("Sphere diameter")]
    public float sphereDiameter;

    /// <summary>Diameter of the pipe that connects the attach node and the pipe joint.</summary>
    /// <remarks>It must be zero or positive.</remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("armDiameter")]
    [KASDebugAdjustable("Arm diameter")]
    public float armDiameter;

    /// <summary>Path to the prefab model that represents the joint.</summary>
    /// <remarks>
    /// Note, that the model will be "consumed". I.e. the internal logic may change the name of the
    /// object within the part's model, extend it with more objects, or destroy it altogether. If
    /// the same model is needed for the other purposes, add a copy via a <c>MODEL</c> tag in the
    /// part's config.
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='M:KSPDev.Hierarchy.FindTransformByPath']/*"/>
    [PersistentField("model")]
    public string modelPath = "";

    /// <summary>Position and rotation at which the model will attach to the target part.</summary>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.Types.PosAndRot']/*"/>
    [PersistentField("modelPartAttachAt")]
    [KASDebugAdjustable("Prefab PART attach pos&rot")]
    public PosAndRot modelPartAttachAt = new PosAndRot();

    /// <summary>Position and rotation at which the node's model will attach to the pipe.</summary>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.Types.PosAndRot']/*"/>
    [PersistentField("modelPipeAttachAt")]
    [KASDebugAdjustable("Prefab PIPE attach pos&rot")]
    public PosAndRot modelPipeAttachAt = new PosAndRot();

    /// <summary>
    /// Position and rotation at which the joint head attaches when the renderer is stopped.
    /// </summary>
    /// <remarks>
    /// <para>It's the location at the <i>source</i> part.</para>
    /// <para>If it's <c>null</c>, then the model will be simply hidden on the renderer stop.</para>
    /// </remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.Types.PosAndRot']/*"/>
    [PersistentField("parkAttachAt")]
    [KASDebugAdjustable("Park location pos&rot")]
    public PosAndRot parkAttachAt;
  }
  #endregion

  #region Public mesh names
  /// <summary>Mesh name for the node that attaches to the source part.</summary>
  public const string SourceNodeMesh = "sourceNodeModel";

  /// <summary>Mesh name for the node that attaches to the target part.</summary>
  public const string TargetNodeMesh = "targetNodeModel";

  /// <summary>Mesh name for the pipe between teh nodes.</summary>
  public const string PipeMesh = "pipeModel";
  #endregion

  #region Object names for the procedural model construction
  /// <summary>
  /// Name of the object in the node's model that defines where it attaches to the part.
  /// </summary>
  /// <remarks>
  /// The source part's attach node attaches to the pipe at the point, defined by this object.
  /// The part's attach node and the are oriented so that their directions look at each other. 
  /// </remarks>
  /// FIXME: make public for winch
  protected const string PartJointTransformName = "partAttach";

  /// <summary>
  /// Name of the object in the node's model that defines where it attaches to the pipe mesh.
  /// </summary>
  /// <remarks>This object looks in the direction of the pipe, towards the other end.</remarks>
  /// FIXME: make public for winch
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

    /// <summary>Prefab model, used as a part of the attachement end node.</summary>
    /// <remarks>
    /// This object is never disposed. On dispose, it's returned to the part's model in the inactive
    /// state.
    /// </remarks>
    public Transform prefabModel;

    /// <summary>Part's model.</summary>
    /// <remarks>Used to return the persistent objects on dispose.</remarks>
    public Transform partModel;

    /// <summary>Destroys the dynamically created objects and disables the prefab.</summary>
    public void Dispose() {
      // Do NOT destroy the prefab model. Only disable it.
      if (prefabModel != null) {
        prefabModel.parent = partModel;
        prefabModel.gameObject.SetActive(false);
      }

      // Destroy the dynamically created objects. 
      rootModel.parent = null;  // Remove from hierarchy immediately.
      UnityEngine.Object.Destroy(rootModel.gameObject);
      if (parkAttach != null) {
        parkAttach.parent = null;  // Remove from hierarchy immediately.
        UnityEngine.Object.Destroy(parkAttach.gameObject);
      }
    }

    /// <summary>Updates the node's state to the target transform.</summary>
    /// <param name="target">
    /// The transfrom to align the node to. It can be <c>null</c> if there is no transform.
    /// </param>
    public void AlignToTransform(Transform target) {
      if (target != null) {
        rootModel.gameObject.SetActive(true);
        AlignTransforms.SnapAlign(rootModel, partAttach, target);
        rootModel.parent = target;
      } else {
        rootModel.parent = partModel;
        if (parkAttach == null) {
          rootModel.gameObject.SetActive(false);
        } else {
          rootModel.gameObject.SetActive(true);
          AlignTransforms.SnapAlign(rootModel, pipeAttach, parkAttach);
        }
      }
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
    LoadPartModel();
  }

  /// <inheritdoc/>
  protected override void LoadPartModel() {
    if (sourceJointNode != null) {
      sourceJointNode.Dispose();
      sourceJointNode = null;
    }
    if (targetJointNode != null) {
      targetJointNode.Dispose();
      targetJointNode = null;
    }
    sourceJointNode = CreateJointEndModels(SourceNodeName, sourceJointConfig);
    targetJointNode = CreateJointEndModels(TargetNodeName, targetJointConfig);
    sourceJointNode.AlignToTransform(sourceTransform);
    targetJointNode.AlignToTransform(targetTransform);
  }

  /// <inheritdoc/>
  protected override void CreatePipeMesh() {
    DestroyPipeMesh();
    CreateLinkPipe();
    sourceJointNode.AlignToTransform(sourceTransform);
    targetJointNode.AlignToTransform(targetTransform);

    // Have the overrides applied if any.
    UpdateMaterialOverrides();
    UpdateColliderOverrides();
  }

  /// <inheritdoc/>
  protected override void DestroyPipeMesh() {
    if (pipeTransform != null) {
      UnityEngine.Object.Destroy(pipeTransform.gameObject);
    }
    pipeTransform = null;
    pipeMeshRenderer = null;
    if (sourceJointNode != null) {
      sourceJointNode.AlignToTransform(null);
    }
    if (targetJointNode != null) {
      targetJointNode.AlignToTransform(null);
    }
  }

  /// <inheritdoc/>
  public override void UpdateLink() {
    if (isStarted) {
      SetupPipe(
          pipeTransform, sourceJointNode.pipeAttach.position, targetJointNode.pipeAttach.position);
      if (pipeTextureRescaleMode != PipeTextureRescaleMode.Stretch) {
        RescaleTextureToLength(
            pipeTransform, pipeTextureSamplesPerMeter, renderer: pipeMeshRenderer);
      }
    }
  }

  /// <inheritdoc/>
  public override Transform GetMeshByName(string meshName) {
    Transform res = null;
    switch (meshName) {
      case SourceNodeMesh:
        res = sourceJointNode.rootModel;
        break;
      case TargetNodeMesh:
        res = targetJointNode.rootModel;
        break;
      case PipeMesh:
        res = pipeTransform;
        break;
    }
    if (res == null) {
      throw new ArgumentException("Unknown mesh name: " + meshName);
    }
    return res;
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
    if (pipeTransform != null) {
      Meshes.UpdateMaterials(pipeTransform.gameObject, newColor: color, newShaderName: shader);
    }
    Meshes.UpdateMaterials(
        sourceJointNode.rootModel.gameObject, newColor: color, newShaderName: shader);
    Meshes.UpdateMaterials(
        targetJointNode.rootModel.gameObject, newColor: color, newShaderName: shader);
  }

  /// <inheritdoc/>
  protected override void UpdateColliderOverrides() {
    if (pipeTransform != null) {
      Colliders.UpdateColliders(pipeTransform.gameObject, isEnabled: pipeColliderIsPhysical);
    }
    Colliders.UpdateColliders(
        sourceJointNode.rootModel.gameObject, isEnabled: pipeColliderIsPhysical);
    Colliders.UpdateColliders(
        targetJointNode.rootModel.gameObject, isEnabled: pipeColliderIsPhysical);
  }

  /// <inheritdoc/>
  protected override void SetCollisionIgnores(Part otherPart, bool ignore) {
    if (pipeTransform != null) {
      Colliders.SetCollisionIgnores(pipeTransform, otherPart.transform, ignore);
    }
    Colliders.SetCollisionIgnores(sourceJointNode.rootModel, otherPart.transform, ignore);
    Colliders.SetCollisionIgnores(targetJointNode.rootModel, otherPart.transform, ignore);
  }
  #endregion

  #region Inheritable methods
  /// <summary>Builds a model for the joint end basing on the configuration.</summary>
  /// <param name="name">
  /// The name of the node's root model to disambiguate the module's objects hierarchy. This name
  /// may not be used as is, the actual object can have a different full name.
  /// </param>
  /// <param name="config">The joint configuration from the part's config.</param>
  /// <returns>The pipe end node. The root model will eb a child of the part's model.</returns>
  protected virtual ModelPipeEndNode CreateJointEndModels(string name, JointConfig config) {
    var res = new ModelPipeEndNode();
    res.partModel = partModelTransform;
    
    // Create basic setup.
    var nodeName = ModelBasename + "-pipeNode" + name;
    res.rootModel = partModelTransform.Find(nodeName)
        ?? new GameObject(nodeName).transform;
    res.rootModel.parent = partModelTransform;
    res.partAttach = res.rootModel.Find(PartJointTransformName)
        ?? new GameObject(PartJointTransformName).transform;
    Hierarchy.MoveToParent(res.partAttach, res.rootModel,
                           newPosition: Vector3.zero,
                           newRotation: Quaternion.LookRotation(Vector3.back));
    res.pipeAttach = res.rootModel.Find(PipeJointTransformName)
        ?? new GameObject(PipeJointTransformName).transform;
    Hierarchy.MoveToParent(res.pipeAttach, res.rootModel,
                           newPosition: Vector3.zero,
                           newRotation: Quaternion.LookRotation(Vector3.forward));

    // Add a pipe attachment sphere if set.
    if (config.sphereDiameter > float.Epsilon) {
      const string sphereName = "pipeSphere";
      var sphere = res.pipeAttach.Find(sphereName)
          ?? Meshes.CreateSphere(config.sphereDiameter, pipeMaterial, res.pipeAttach,
                                 colliderType: Colliders.PrimitiveCollider.Shape).transform;
      sphere.name = sphereName;
      sphere.GetComponent<Renderer>().sharedMaterial = pipeMaterial;  // For performance.
      RescaleTextureToLength(sphere,
                             samplesPerMeter: pipeTextureSamplesPerMeter,
                             extraScale: config.sphereDiameter * 2.0f);
      Hierarchy.MoveToParent(sphere, res.pipeAttach,
                             newPosition: Vector3.zero,
                             newRotation: Quaternion.identity);
    }

    // Parking position, if defined.
    if (config.parkAttachAt != null) {
      var parkObjectName = ModelBasename + "-park" + name;
      res.parkAttach = partModelTransform.Find(parkObjectName)
          ?? new GameObject(parkObjectName).transform;
      Hierarchy.MoveToParent(res.parkAttach, partModelTransform,
                             newPosition: config.parkAttachAt.pos,
                             newRotation: config.parkAttachAt.rot);
    }

    // Place prefab between the part and the pipe if specified.
    if (!string.IsNullOrEmpty(config.modelPath)) {
      // The prefab model can move to the part's model, so make a unique name for it. 
      var prefabName = ModelBasename + "-connector" + name;
      var prefabModel = res.rootModel.Find(prefabName)
          ?? partModelTransform.Find(prefabName)  // Models re-create case.
          ?? Hierarchy.FindTransformByPath(partModelTransform, config.modelPath);
      if (prefabModel != null) {
        prefabModel.gameObject.SetActive(true);
        prefabModel.name = prefabName;
        prefabModel.parent = res.rootModel;
        prefabModel.rotation = res.partAttach.rotation * config.modelPartAttachAt.rot.Inverse();
        prefabModel.position = res.partAttach.TransformPoint(config.modelPartAttachAt.pos);
        res.pipeAttach.rotation = prefabModel.rotation * config.modelPipeAttachAt.rot;
        res.pipeAttach.position = prefabModel.TransformPoint(config.modelPipeAttachAt.pos);
        res.prefabModel = prefabModel;
      } else {
        HostedDebugLog.Error(this, "Cannot find model: {0}", prefabName);
      }
    }

    // Add arm pipe.
    res.pipeAttach.localPosition += new Vector3(0, 0, config.sphereOffset);
    if (config.armDiameter > float.Epsilon && config.sphereOffset > float.Epsilon) {
      const string armName = "sphereArm";
      var arm = res.pipeAttach.Find(armName)
          ?? Meshes.CreateCylinder(config.armDiameter, config.sphereOffset, pipeMaterial,
                                   res.pipeAttach,
                                   colliderType: Colliders.PrimitiveCollider.Shape).transform;
      arm.GetComponent<Renderer>().sharedMaterial = pipeMaterial;  // For performance.
      arm.transform.localPosition = new Vector3(0, 0, -config.sphereOffset / 2);
      arm.transform.localRotation = Quaternion.LookRotation(Vector3.forward);
      RescaleTextureToLength(
          arm.transform, samplesPerMeter: pipeTextureSamplesPerMeter,
          extraScale: config.sphereOffset);
    }

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
  #endregion
}

}  // namespace
