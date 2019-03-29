// Kerbal Attachment System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;
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
    [Debug.KASDebugAdjustable("Sphere offset")]
    public float sphereOffset;

    /// <summary>Diameter of the sphere to place at the pipe joint.</summary>
    /// <remarks>It must be zero or positive.</remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("sphereDiameter")]
    [Debug.KASDebugAdjustable("Sphere diameter")]
    public float sphereDiameter;

    /// <summary>Diameter of the pipe that connects the attach node and the pipe joint.</summary>
    /// <remarks>It must be zero or positive.</remarks>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    [PersistentField("armDiameter")]
    [Debug.KASDebugAdjustable("Arm diameter")]
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
    [Debug.KASDebugAdjustable("Prefab PART attach pos&rot")]
    public PosAndRot modelPartAttachAt = new PosAndRot();

    /// <summary>Position and rotation at which the node's model will attach to the pipe.</summary>
    /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
    /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.Types.PosAndRot']/*"/>
    [PersistentField("modelPipeAttachAt")]
    [Debug.KASDebugAdjustable("Prefab PIPE attach pos&rot")]
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
    [Debug.KASDebugAdjustable("Park location pos&rot")]
    public PosAndRot parkAttachAt;

    /// <summary>
    /// Tells if the only prefab model, but not the procedural models must be parked on renderer
    /// stop.
    /// </summary>
    /// <remarks>
    /// If this setting is <c>true</c> and there is a parking position defined, then on renderer
    /// stop all the procedural models (sphere and arm) will be removed. Changing this behavior
    /// makes sense when the procedural models are parts of the connector rather than simple helpers
    /// for the pipe mesh.
    /// </remarks>
    [PersistentField("parkAllModels")]
    [Debug.KASDebugAdjustable("Park prefab model only")]
    public bool parkPrefabOnly = true;
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
  public const string PartJointTransformName = "partAttach";

  /// <summary>
  /// Name of the object in the node's model that defines where it attaches to the pipe mesh.
  /// </summary>
  /// <remarks>This object looks in the direction of the pipe, towards the other end.</remarks>
  public const string PipeJointTransformName = "pipeAttach";

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
    /// <summary>Node name, used to create the object name.</summary>
    public readonly string name;

    /// <summary>Config settings for this node.</summary>
    public readonly JointConfig config;

    /// <summary>The main node's model. All anchors are children to it.</summary>
    public Transform rootModel;

    /// <summary>Transform at which the node attaches to the target part.</summary>
    /// <remarks>It's always a child of <see cref="rootModel"/>.</remarks>
    public Transform partAttach;

    /// <summary>Transform at which the node attaches to the pipe mesh.</summary>
    /// <remarks>It's always a child of <see cref="rootModel"/>.</remarks>
    public Transform pipeAttach;

    /// <summary>Transform to attach the root model when the renderer stops.</summary>
    /// <remarks>
    /// Can be <c>null</c>, in which case the root model will be deactivated instead of being
    /// aligned to the park location. The alignment is done as "snap align" at
    /// <see cref="pipeAttach"/>. Note, that this object only used for the <i>alignment</i>, the
    /// parent of the parked/hidden model will be <see cref="parkRootObject"/>.
    /// </remarks>
    public Transform parkAttach;

    /// <summary>Object that becomes parent when the model is parked.</summary>
    /// <remarks>
    /// This obejct must never be <c>null</c>. Set it to the part's model when unsure what to
    /// provide.
    /// </remarks>
    public Transform parkRootObject;

    /// <summary>Creates a node.</summary>
    /// <param name="name">The string to use when making model object name.</param>
    /// <param name="config">The settings of the node.</param>
    public ModelPipeEndNode(string name, JointConfig config) {
      this.name = name;
      this.config = config;
    }

    /// <summary>Updates the node's state to the target transform.</summary>
    /// <param name="target">
    /// The transfrom to align the node to. If <c>null</c>, then the model will be "parked".
    /// </param>
    /// <seealso cref="parkAttach"/>
    public void AlignToTransform(Transform target) {
      if (target != null) {
        rootModel.gameObject.SetActive(true);
        AlignTransforms.SnapAlign(rootModel, partAttach, target);
        rootModel.parent = target;
      } else {
        rootModel.parent = parkRootObject;
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
  [Debug.KASDebugAdjustable("Source joint config")]
  public JointConfig sourceJointConfig = new JointConfig();

  /// <summary>Configuration of the target joint model.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
  [PersistentField("targetJoint", group = StdPersistentGroups.PartConfigLoadGroup)]
  [Debug.KASDebugAdjustable("Target joint config")]
  public JointConfig targetJointConfig = new JointConfig();
  #endregion

  #region Inheritable fields & properties
  /// <summary>Pipe's mesh.</summary>
  /// <value><c>null</c> if the renderer is not started.</value>
  /// <seealso cref="CreateLinkPipe"/>
  protected Transform pipeTransform;

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
  protected Renderer pipeMeshRenderer;

  /// <summary>Pipe ending node at the source.</summary>
  /// <value>The source node container.</value>
  /// <seealso cref="UpdateJointNode"/>
  protected ModelPipeEndNode sourceJointNode { get; private set; }

  /// <summary>Pipe ending node at the target.</summary>
  /// <value>The target node container.</value>
  /// <seealso cref="UpdateJointNode"/>
  protected ModelPipeEndNode targetJointNode { get; private set; }
  #endregion

  #region AbstractPipeRenderer abstract methods
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    sourceJointNode = new ModelPipeEndNode(SourceNodeName, sourceJointConfig);
    targetJointNode = new ModelPipeEndNode(TargetNodeName, targetJointConfig);
  }

  /// <inheritdoc/>
  protected override void LoadPartModel() {
    UpdateJointNode(sourceJointNode, sourceTransform);
    UpdateJointNode(targetJointNode, targetTransform);
  }

  /// <inheritdoc/>
  protected override void CreatePipeMesh() {
    CreateLinkPipe();
    UpdateJointNode(sourceJointNode, sourceTransform);
    UpdateJointNode(targetJointNode, targetTransform);

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
    if (sourceJointNode != null && sourceTransform != null) {
      UpdateJointNode(sourceJointNode, null);
    }
    if (targetJointNode != null && targetTransform != null) {
      UpdateJointNode(targetJointNode, null);
    }
  }

  /// <inheritdoc/>
  public override void UpdateLink() {
    if (isStarted) {//FIXME coroutine
      SetupPipe(sourceJointNode.pipeAttach.position, targetJointNode.pipeAttach.position);
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
    if (pipeColliderIsPhysical && isStarted) {
      return new[] {
          sourceJointNode.partAttach.position, sourceJointNode.pipeAttach.position,
          targetJointNode.pipeAttach.position, targetJointNode.partAttach.position
      };
    }
    return new Vector3[0];
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
      Colliders.UpdateColliders(pipeTransform.gameObject, isEnabled: isPhysicalCollider);
    }
    Colliders.UpdateColliders(
        sourceJointNode.rootModel.gameObject, isEnabled: isPhysicalCollider);
    Colliders.UpdateColliders(
        targetJointNode.rootModel.gameObject, isEnabled: isPhysicalCollider);
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
  /// <param name="node">The node to setup.</param>
  /// <param name="alignTo">
  /// The object to align the conenctor to. If it's <c>null</c>, then the model will be parked.
  /// </param>
  protected virtual void UpdateJointNode(ModelPipeEndNode node, Transform alignTo) {
    var config = node.config;
    var makeProceduralModels = alignTo != null || !config.parkPrefabOnly;

    // Return the models back to the owner part to make the search working properly.
    if (node.rootModel != null) {
      node.AlignToTransform(null);
    }
    
    node.parkRootObject = partModelTransform;
    
    // Create basic setup.
    var nodeName = ModelBasename + "-pipeNode" + node.name;
    node.rootModel = partModelTransform.Find(nodeName)
        ?? new GameObject(nodeName).transform;
    node.rootModel.parent = partModelTransform;
    node.partAttach = node.rootModel.Find(PartJointTransformName)
        ?? new GameObject(PartJointTransformName).transform;
    Hierarchy.MoveToParent(node.partAttach, node.rootModel,
                           newPosition: Vector3.zero,
                           newRotation: Quaternion.LookRotation(Vector3.back));
    node.pipeAttach = node.rootModel.Find(PipeJointTransformName)
        ?? new GameObject(PipeJointTransformName).transform;
    Hierarchy.MoveToParent(node.pipeAttach, node.rootModel,
                           newPosition: Vector3.zero,
                           newRotation: Quaternion.LookRotation(Vector3.forward));

    // Add a pipe attachment sphere if set.
    const string sphereName = "pipeSphere";
    var sphere = node.pipeAttach.Find(sphereName);
    if (config.sphereDiameter > float.Epsilon && makeProceduralModels) {
      if (sphere == null) {
        sphere = Meshes.CreateSphere(config.sphereDiameter, pipeMaterial, node.pipeAttach,
                                     colliderType: Colliders.PrimitiveCollider.Shape).transform;
        sphere.name = sphereName;
      }
      sphere.GetComponent<Renderer>().sharedMaterial = pipeMaterial;  // For performance.
      RescalePipeTexture(sphere, sphere.localScale.z, extraScale: config.sphereDiameter * 2.0f);
    } else if (sphere != null) {
      Hierarchy2.SafeDestory(sphere);
    }

    // Parking position, if defined.
    var parkObjectName = ModelBasename + "-park" + node.name;
    var parkAttach = partModelTransform.Find(parkObjectName);
    if (config.parkAttachAt != null) {
      node.parkAttach = parkAttach ?? new GameObject(parkObjectName).transform;
      Hierarchy.MoveToParent(node.parkAttach, partModelTransform,
                             newPosition: config.parkAttachAt.pos,
                             newRotation: config.parkAttachAt.rot);
    } else if (parkAttach != null) {
      Hierarchy2.SafeDestory(parkAttach);
    }

    // Place prefab between the part and the pipe if specified.
    if (!string.IsNullOrEmpty(config.modelPath)) {
      // The prefab model can move to the part's model, so make a unique name for it.
      const string prefabName = "prefabConnector";
      var prefabModel = node.rootModel.Find(prefabName)
          ?? Hierarchy.FindTransformByPath(partModelTransform, config.modelPath);
      if (prefabModel != null) {
        prefabModel.gameObject.SetActive(true);
        prefabModel.name = prefabName;
        prefabModel.parent = node.rootModel;
        prefabModel.rotation = node.partAttach.rotation * config.modelPartAttachAt.rot.Inverse();
        prefabModel.position = node.partAttach.TransformPoint(config.modelPartAttachAt.pos);
        node.pipeAttach.rotation = prefabModel.rotation * config.modelPipeAttachAt.rot;
        node.pipeAttach.position = prefabModel.TransformPoint(config.modelPipeAttachAt.pos);
      } else {
        HostedDebugLog.Error(this, "Cannot find prefab model: {0}", config.modelPath);
      }
    }

    // The offset is intended for the sphere/arm models only.  
    if (makeProceduralModels) {
      node.pipeAttach.localPosition += new Vector3(0, 0, config.sphereOffset);
    }

    // Add arm pipe.
    const string armName = "sphereArm";
    var arm = node.pipeAttach.Find(armName);
    if (config.armDiameter > float.Epsilon && config.sphereOffset > float.Epsilon
        && makeProceduralModels) {
      if (arm == null) {
        arm = Meshes.CreateCylinder(config.armDiameter, config.sphereOffset, pipeMaterial,
                                    node.pipeAttach,
                                    colliderType: Colliders.PrimitiveCollider.Shape).transform;
        arm.name = armName;
      }
      arm.GetComponent<Renderer>().sharedMaterial = pipeMaterial;  // For performance.
      arm.transform.localPosition = new Vector3(0, 0, -config.sphereOffset / 2);
      arm.transform.localRotation = Quaternion.LookRotation(Vector3.forward);
      RescalePipeTexture(arm.transform, arm.localScale.z, extraScale: config.sphereOffset);
    } else if (arm != null) {
      Hierarchy2.SafeDestory(arm);
    }

    // Adjust to the new target.
    node.AlignToTransform(alignTo);
  }

  /// <summary>
  /// Creates a mesh that represents the connecting pipe between the source and the target.
  /// </summary>
  /// <remarks>It's only called in the started state.</remarks>
  /// <seealso cref="CreatePipeMesh"/>
  /// <seealso cref="pipeTransform"/>
  protected virtual void CreateLinkPipe() {
    var colliderType = pipeColliderIsPhysical
        ? Colliders.PrimitiveCollider.Shape
        : Colliders.PrimitiveCollider.None;
    pipeTransform = Meshes.CreateCylinder(
        pipeDiameter, 1.0f, pipeMaterial, sourceTransform, colliderType: colliderType).transform;
    pipeTransform.GetComponent<Renderer>().sharedMaterial = pipeMaterial;
    pipeMeshRenderer = pipeTransform.GetComponent<Renderer>();  // To speedup OnUpdate() handling.
    SetupPipe(sourceJointNode.pipeAttach.position, targetJointNode.pipeAttach.position);
    var extraScale = 1.0f;
    if (pipeTextureRescaleMode == PipeTextureRescaleMode.Stretch) {
      extraScale /=
          (sourceJointNode.pipeAttach.position - targetJointNode.pipeAttach.position).magnitude;
    }
    RescalePipeTexture(pipeTransform, pipeTransform.localScale.z,
                       renderer: pipeMeshRenderer, extraScale: extraScale);
  }

  /// <summary>Ensures that the pipe's mesh connects the specified positions.</summary>
  /// <param name="fromPos">Position of the link source.</param>
  /// <param name="toPos">Position of the link target.</param>
  /// <seealso cref="pipeTransform"/>
  protected virtual void SetupPipe(Vector3 fromPos, Vector3 toPos) {
    pipeTransform.position = (fromPos + toPos) / 2;
    if (pipeTextureRescaleMode == PipeTextureRescaleMode.TileFromTarget) {
      pipeTransform.LookAt(fromPos);
    } else {
      pipeTransform.LookAt(toPos);
    }
    pipeTransform.localScale = new Vector3(
        pipeTransform.localScale.x,
        pipeTransform.localScale.y,
        Vector3.Distance(fromPos, toPos) / baseScale);
    if (pipeTextureRescaleMode != PipeTextureRescaleMode.Stretch) {
      RescalePipeTexture(pipeTransform, pipeTransform.localScale.z, renderer: pipeMeshRenderer);
    }
  }

  /// <summary>Adjusts texture on the object to fit the part's rescale mode.</summary>
  /// <remarks>
  /// The mesh UV coordinates are expected to be distributed over its full length from <c>0.0</c> to
  /// <c>1.0</c>. Such configuration ensures that texture covers the entire mesh, and
  /// stretches/shrinks as the mesh changes its length. However, in the tiling modes, the texture
  /// must be distributed over the mesh lengh so that it's not changing its ratio. This method
  /// checks the renderer stretching mode and adjusts the texture scale.
  /// <para>
  /// This method is intentionally not virtual, since it's a utility method. Any part specific logic
  /// must be implemented outside of this method.
  /// </para>
  /// <para>
  /// This method ensures that the bump/specular map textures, if any, are properly scaled as well.
  /// </para>
  /// </remarks>
  /// <param name="obj">The mesh object to adjust texture for.</param>
  /// <param name="length">The length to adjust the texture to.</param>
  /// <param name="renderer">
  /// The optional renderer that owns the material. If not provided, then it will be obtained via
  /// a <c>GetComponent()</c> call which is rather expensive.
  /// </param>
  /// <param name="extraScale">The multiplier to add to the base scale. For any reason.</param>
  /// <seealso cref="AbstractPipeRenderer.pipeTextureSamplesPerMeter"/>
  /// <seealso cref="AbstractPipeRenderer.pipeTextureRescaleMode"/>
  protected void RescalePipeTexture(
      Transform obj, float length, Renderer renderer = null, float extraScale = 1.0f) {
    var newScale = length * pipeTextureSamplesPerMeter / baseScale * extraScale;
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
