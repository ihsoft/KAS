// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.KSPInterfaces;
using KSPDev.ConfigUtils;
using KSPDev.ModelUtils;
using KSPDev.LogUtils;
using KSPDev.Types;
using KSPDev.GUIUtils;
using System;
using System.Linq;
using UnityEngine;

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
/// </remarks>
/// <seealso cref="ILinkSource"/>
/// <seealso cref="ILinkRenderer"/>
/// <seealso cref="PipeEndType"/>
/// <seealso cref="JointConfig"/>
public class KASModulePipeRenderer : AbstractProceduralModel,
    // KAS interfaces.
    ILinkRenderer,
    // KPSDev sugar interfaces.    
    IPartModule, IsDestroyable {

  #region Localizable GUI strings
  /// <summary>
  /// Message to display when link cannot be created due to an obstacle in the way. 
  /// </summary>
  protected static Message<string> LinkCollidesWithObjectMsg = "Link collides with {0}";

  /// <summary>
  /// Message to display when link strut orientation cannot be changed due to it would hit the
  /// surface.
  /// </summary>
  protected static Message LinkCollidesWithSurfaceMsg = "Link collides with the surface";
  #endregion

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
    /// A sphere mesh is rendered at the point where the pipe's mesh touches the target part's
    /// attach node. The sphere diameter can be adjusted, and if it's equal or greater than the
    /// diameter of the pipe then the joint looks smoother.
    /// <para>
    /// The actual connection point for the pipe mesh can be elevated over the target attach node.
    /// In this case a simple cylinder, the "arm", will be rendered between the part and the joint
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

  /// <summary>Helper structure to hold the joint model setup.</summary>
  public class JointConfig {
    /// <value>Defines how to obtain the joint model.</value>
    [PersistentField("type")]
    public PipeEndType type = PipeEndType.Simple;
    
    /// <value>
    /// Defines if model's should trigger physical effects on collision.
    /// </value>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/> or
    /// <see cref="PipeEndType.PrefabModel"/>. If the prefab models are used then the colliders must
    /// be existing in the model. If there are none then this settings doesn't have effect.
    /// </remarks>
    [PersistentField("colliderIsPhysical")]
    public bool colliderIsPhysical;

    /// <value>
    /// Height of the joint sphere over the attach node. It's either zero or a positive number.
    /// </value>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/>.
    /// </remarks>
    [PersistentField("sphereOffset")]
    public float sphereOffset;

    /// <summary>Diameter of the joint sphere.</summary>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/>.
    /// </remarks>
    [PersistentField("sphereDiameter")]
    public float sphereDiameter;

    /// <value>Diameter of the pipe that connects the attach node and the sphere.</value>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/> and
    /// <see cref="sphereOffset"/> is greater than zero.
    /// </remarks>
    [PersistentField("armDiameter")]
    public float armDiameter;

    /// <value>Defines how the texture is tiled on the sphere and arm primitives.</value>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/>.
    /// </remarks>
    [PersistentField("textureSamplesPerMeter")]
    public float textureSamplesPerMeter;

    /// <value>Texture to use to cover the arm and sphere primitives.</value>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/>.
    /// </remarks>
    [PersistentField("texture")]
    public string texture = "";

    /// <value>Normals texture for the primitives. Can be omitted.</value>
    /// <remarks>
    /// Only used if <see cref="type"/> is <see cref="PipeEndType.ProceduralModel"/>.
    /// </remarks>
    [PersistentField("textureNrm")]
    public string textureNrm = "";

    /// <value>Path to the model that represents the joint.</value>
    /// <remarks>Only used if <see cref="type"/> is <see cref="PipeEndType.PrefabModel"/>.</remarks>
    /// <seealso href="http://ihsoft.github.io/KSPDev/Utils/html/M_KSPDev_ModelUtils_Hierarchy_FindTransformByPath.htm">
    /// KSPDev: Hierarchy.FindTransformByPath</seealso>
    [PersistentField("model")]
    public string modelPath = "";

    /// <value>Setup of the node at which the node's model will attach to the target part.</value>
    /// <remarks>Only used if <see cref="type"/> is <see cref="PipeEndType.PrefabModel"/>.</remarks>
    [PersistentField("partAttachAt")]
    public PosAndRot partAttachAt = new PosAndRot();

    /// <value>Setup of the node at which the node's model will attach to the pipe.</value>
    /// <remarks>Only used if <see cref="type"/> is <see cref="PipeEndType.PrefabModel"/>.</remarks>
    [PersistentField("pipeAttachAt")]
    public PosAndRot pipeAttachAt = new PosAndRot();
  }
  #endregion

  #region Object names for the procedural model construction
  const string PartJointTransformName = "$partAttach";
  const string PipeJointTransformName = "$pipeAttach";
  const string ProceduralSourceJointObjectName = "$sourceJointEnd";
  const string ProceduralTargetJointObjectName = "$targetJointEnd";
  #endregion

  /// <summary>
  /// Name of the group for the extra settings from the part's config. The values will be loaded via
  /// <c>ConfigAccessor</c>.
  /// </summary>
  /// <remarks>
  /// Decendants may declare own persistent fields in this group, and they will be automatically
  /// loaded. The only requirement is that these fields must be declared public.
  /// </remarks>
  /// <seealso href="http://ihsoft.github.io/KSPDev/Utils/html/T_KSPDev_ConfigUtils_ConfigAccessor.htm">
  /// KSPDev Utils: ConfigUtils.ConfigAccessor</seealso>
  protected const string PartConfigGroup = "partConfig";

  #region Helper class for drawing a pipe's end
  /// <summary>Helper class for drawing a pipe's end.</summary>
  protected class ModelPipeEndNode {
    /// <value>The main node's model.</value>
    public readonly Transform model;

    /// <value>Transform at which the node attaches to the target part.</value>
    /// <remarks>It's always a child of the main node's model.</remarks>
    public readonly Transform partAttach;

    /// <value>Transform at which the node attaches to the pipe mesh.</value>
    /// <remarks>It's always a child of the main node's model.</remarks>
    public readonly Transform pipeAttach;

    /// <summary>Creates a new attach node.</summary>
    /// <param name="model">Model to use. It cannot be <c>null</c>.</param>
    public ModelPipeEndNode(Transform model) {
      this.model = model;
      partAttach = GetTransformByName(PartJointTransformName);
      partAttach.parent = model;
      pipeAttach = GetTransformByName(PipeJointTransformName);
      pipeAttach.parent = model;
    }

    /// <summary>Aligns node against the target.</summary>
    /// <param name="target">
    /// The target object. Can be <c>null</c> in which case the node model will be hidden.
    /// </param>
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
    /// Finds and returns the requested child model, or mthe main model if the child is not found.  
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    protected Transform GetTransformByName(string name) {
      var res = model.FindChild(name);
      if (res == null) {
        Debug.LogErrorFormat(
            "Cannot find transform '{0}' in '{1}'", name, DbgFormatter.TranformPath(model));
        res = model;  // Fallback.
      }
      return res;
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
      var newColor = _colorOverride ?? materialColor;
      sourceJointNode.UpdateMaterial(newColor: newColor);
      targetJointNode.UpdateMaterial(newColor: newColor);
      if (linkPipe != null) {
        Meshes.UpdateMaterials(linkPipe, newColor: newColor);
      }
    }
  }
  Color? _colorOverride;

  /// <inheritdoc/>
  public virtual string shaderNameOverride {
    get { return _shaderNameOverride; }
    set {
      _shaderNameOverride = value;
      var newShader = _shaderNameOverride ?? shaderName;
      sourceJointNode.UpdateMaterial(newShaderName: newShader);
      targetJointNode.UpdateMaterial(newShaderName: newShader);
      if (linkPipe) {
        Meshes.UpdateMaterials(linkPipe, newShaderName: newShader);
      }
    }
  }
  string _shaderNameOverride;

  /// <inheritdoc/>
  public virtual bool isPhysicalCollider {
    get { return _collidersEnabled; }
    set {
      _collidersEnabled = value;
      sourceJointNode.SetColliderEnabled(_collidersEnabled);
      targetJointNode.SetColliderEnabled(_collidersEnabled);
      if (linkPipe != null) {
        Colliders.UpdateColliders(linkPipe.gameObject, isEnabled: _collidersEnabled);
      }
    }
  }
  bool _collidersEnabled = true;

  /// <inheritdoc/>
  public bool isStarted { get { return linkPipe != null; } }

  /// <inheritdoc/>
  public Transform sourceTransform { get; private set; }

  /// <inheritdoc/>
  public Transform targetTransform { get; private set; }

  /// <inheritdoc/>
  public virtual float stretchRatio { get; set; }
  #endregion

  #region Part's config fields
  /// <value>Config setting. See <see cref="cfgRendererName"/>.</value>
  [KSPField]
  public string rendererName = string.Empty;

  /// <value>Config setting. Diameter of the pipe.</value>
  [KSPField]
  public float pipeDiameter = 0.15f;

  /// <value>Config setting. Texture to use for the pipe.</value>
  /// <seealso cref="pipeTextureRescaleMode"/>
  /// <seealso cref="pipeTextureSamplesPerMeter"/>
  [KSPField]
  public string pipeTexturePath = "KAS-1.0/Textures/pipe";

  /// <value>
  /// Config setting. Normals texture to use for the pipe. If empty string then no normals.
  /// </value>
  /// <seealso cref="pipeTexturePath"/>
  [KSPField]
  public string pipeNormalsTexturePath = "";

  /// <value>Config setting. Defines how the texture should cover the pipe.</value>
  /// <seealso cref="pipeTexturePath"/>
  /// <seealso cref="pipeTextureSamplesPerMeter"/>
  [KSPField]
  public PipeTextureRescaleMode pipeTextureRescaleMode = PipeTextureRescaleMode.Stretch;

  /// <value>
  /// Config setting. Defines how many texture samples to apply per one meter of the pipe's length.
  /// </value>
  /// <remarks>
  /// This setting is ignored if texture rescale mode is
  /// <see cref="PipeTextureRescaleMode.Stretch"/>.
  /// </remarks>
  /// <seealso cref="pipeTexturePath"/>
  /// <seealso cref="pipeTextureRescaleMode"/>
  [KSPField]
  public float pipeTextureSamplesPerMeter = 1f;

  /// <value>
  /// Config setting. Defines if pipe's collider should interact with the physics objects.
  /// </value>
  /// <remarks>
  /// If this setting is <c>false</c> the link mesh will still have a collider, but it will not
  /// trigger physical effects.
  /// </remarks>
  [KSPField]
  public bool pipeColliderIsPhysical;
  #endregion

  #region Part's config settings loaded via ConfigAccessor
  /// <summary>Configuration of the source joint model.</summary>
  [PersistentField("sourceJoint", group = "partConfig")]
  public JointConfig sourceJointConfig = new JointConfig();

  /// <summary>Configuration of the target joint model.</summary>
  [PersistentField("targetJoint", group = "partConfig")]
  public JointConfig targetJointConfig = new JointConfig();
  #endregion

  #region Local properties
  /// <value>The pipe mesh.</value>
  protected GameObject linkPipe { get; private set; }

  /// <value>Pipe's mesh renderer. Used to speedup updates that are done in every frame.</value>
  protected Renderer linkPipeMR { get; private set; }

  /// <value>Pipe ending node at the source.</value>
  protected ModelPipeEndNode sourceJointNode { get; private set; }

  /// <value>Pipe ending node at the target.</value>
  protected ModelPipeEndNode targetJointNode { get; private set; }
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    if (HighLogic.LoadedScene == GameScenes.LOADING) {
      LoadPartConfig(node);
    }
    base.OnLoad(node);  // Must be the last in the call sequence.
  }

  /// <inheritdoc/>
  public override void OnAwake() {
    stretchRatio = 1.0f;  // A property default.
    base.OnAwake();
    if (HighLogic.LoadedScene != GameScenes.LOADING) {
      LoadPartConfig(PartConfig.GetModuleConfig(this));
    }
  }

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
  public virtual void OnDestroy() {
    StopRenderer();
  }
  #endregion

  #region AbstractProceduralModel abstract members
  /// <inheritdoc/>
  protected override void CreatePartModel() {
    CreateJointEndModelsIfNeeded(ProceduralSourceJointObjectName, sourceJointConfig);
    CreateJointEndModelsIfNeeded(ProceduralTargetJointObjectName, targetJointConfig);
    sourceJointNode = LoadJointNode(ProceduralSourceJointObjectName, sourceJointConfig);
    targetJointNode = LoadJointNode(ProceduralTargetJointObjectName, targetJointConfig);
  }

  /// <inheritdoc/>
  protected override void LoadPartModel() {
    sourceJointNode = LoadJointNode(ProceduralSourceJointObjectName, sourceJointConfig);
    targetJointNode = LoadJointNode(ProceduralTargetJointObjectName, targetJointConfig);
  }
  #endregion

  #region ILinkRenderer implementation
  /// <inheritdoc/>
  public virtual void StartRenderer(Transform source, Transform target) {
    if (isStarted) {
      Debug.LogWarning("Renderer already started. Stopping...");
      StopRenderer();
    }
    sourceTransform = source;
    sourceJointNode.AlignTo(source);
    sourceJointNode.UpdateMaterial(newShaderName: shaderNameOverride, newColor: colorOverride);
    targetTransform = target;
    targetJointNode.AlignTo(target);
    targetJointNode.UpdateMaterial(newShaderName: shaderNameOverride, newColor: colorOverride);
    CreateLinkPipe();
    isPhysicalCollider = isPhysicalCollider;  // Update the status.
  }
  
  /// <inheritdoc/>
  public virtual void StopRenderer() {
    if (isStarted) {
      sourceJointNode.AlignTo(null);
      targetJointNode.AlignTo(null);
      DestroyLinkPipe();
    }
  }

  /// <inheritdoc/>
  public virtual void UpdateLink() {
    if (isStarted) {
      SetupPipe(linkPipe.transform,
                sourceJointNode.pipeAttach.position, targetJointNode.pipeAttach.position);
      targetJointNode.AlignTo(targetTransform);
      if (pipeTextureRescaleMode != PipeTextureRescaleMode.Stretch) {
        RescaleTextureToLength(linkPipe, pipeTextureSamplesPerMeter,
                               renderer: linkPipeMR, scaleRatio: 1 / stretchRatio);
      }
    }
  }

  /// <inheritdoc/>
  public virtual string CheckColliderHits(Transform source, Transform target) {
    // TODO(ihsoft): Implement a full check that includes the pipe ends as well.
    string result = null;
    if (pipeColliderIsPhysical) {
      result = DoSimpleSphereCheck(target, source, pipeDiameter);
    }
    return result;
  }
  #endregion

  #region Inheritable utility methods
  /// <summary>Constructs a joint node for the requested config.</summary>
  /// <param name="modelName"></param>
  /// <param name="config"></param>
  /// <returns></returns>
  protected virtual ModelPipeEndNode LoadJointNode(string modelName, JointConfig config) {
    var node = new ModelPipeEndNode(partModelTransform.FindChild(modelName));
    node.AlignTo(null);  // Init mode objects state.
    return node;
  }
  
  /// <summary>Loads the dynamic properties from the part's config.</summary>
  /// <remarks>
  /// It triggers every time when a new instance of the part instantiates. Use it to update/load
  /// the settings that cannot be loaded via normal KSP means, like the custom types for the
  /// <c>PersistentField</c> attributed fields.
  /// </remarks>
  /// <pre>
  /// When a decendant class needs the custom persisten fields loaded, there is no need to override
  /// this method. It's enough to declare the fields as public and assign them to the persistent
  /// group <see cref="PartConfigGroup"/>. The base implementation will load all the fields in this
  /// group.
  /// </pre>
  /// <param name="moduleNode">Config node to get the values from.</param>
  /// <seealso href="http://ihsoft.github.io/KSPDev/Utils/html/T_KSPDev_ConfigUtils_PersistentFieldAttribute.htm">
  /// KSPDev Utils: ConfigUtils.PersistentFieldAttribute</seealso>
  protected virtual void LoadPartConfig(ConfigNode moduleNode) {
    // This will load all the public fields of the descendant types as well.
    ConfigAccessor.ReadFieldsFromNode(moduleNode, GetType(), this, group: PartConfigGroup);
    // For the procedural and simple modes use the hardcoded model names.
    if (sourceJointConfig.type != PipeEndType.PrefabModel) {
      sourceJointConfig.modelPath = ProceduralSourceJointObjectName;
    }
    if (targetJointConfig.type != PipeEndType.PrefabModel) {
      targetJointConfig.modelPath = ProceduralTargetJointObjectName;
    }
  }

  /// <summary>
  /// Creates and displays a mesh that represents a connecting pipe between the source and the
  /// target parts.
  /// </summary>
  protected virtual void CreateLinkPipe() {
    var nrmTexture = pipeNormalsTexturePath != ""
        ? GetTexture(pipeNormalsTexturePath, asNormalMap: true)
        : null;
    var material = CreateMaterial(GetTexture(pipeTexturePath),
                                  mainTexNrm: nrmTexture,
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
    RescaleTextureToLength(linkPipe, pipeTextureSamplesPerMeter,
                           renderer: linkPipeMR, scaleRatio: 1 / stretchRatio);
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
    obj.localScale =
        new Vector3(obj.localScale.x, obj.localScale.y, Vector3.Distance(fromPos, toPos));
  }

  /// <summary>Builds a model for the joint end basing on the procedural configuration.</summary>
  /// <param name="nodeName">Joint transform name.</param>
  /// <param name="config">Joint configuration from the part's config.</param>
  protected virtual void CreateJointEndModelsIfNeeded(string nodeName, JointConfig config) {
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
        Debug.LogErrorFormat("Cannot find model '{0}' in part '{1}'.",config.modelPath, part.name);
        config.type = PipeEndType.Simple;  // Fallback.
      }
    }
    if (root == null) {
      root = new GameObject().transform;
      Hierarchy.MoveToParent(root, partModelTransform);
      var partJoint = new GameObject(PartJointTransformName).transform;
      Hierarchy.MoveToParent(partJoint, root);
      partJoint.rotation = Quaternion.LookRotation(Vector3.forward);
      if (config.type == PipeEndType.Simple || config.sphereDiameter < float.Epsilon) {
        // No extra models are displayed at the joint, just attach the pipe to the part's node.
        var pipeJoint = new GameObject(PipeJointTransformName);
        Hierarchy.MoveToParent(pipeJoint.transform, root);
        pipeJoint.transform.rotation = Quaternion.LookRotation(Vector3.back);
      } else {
        // Create procedural models at the point where the pipe connects to the part's node.
        var material = CreateMaterial(
            GetTexture(config.texture),
            mainTexNrm: config.textureNrm != "" ? GetTexture(config.textureNrm) : null);
        var sphere = Meshes.CreateSphere(config.sphereDiameter, material, root,
                                         colliderType: Colliders.PrimitiveCollider.Shape);
        sphere.name = PipeJointTransformName;
        sphere.transform.rotation = Quaternion.LookRotation(Vector3.back);
        RescaleTextureToLength(sphere, samplesPerMeter: config.textureSamplesPerMeter);
        if (config.sphereOffset > float.Epsilon) {
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
      }
    }
    Colliders.UpdateColliders(root.gameObject, isPhysical: config.colliderIsPhysical);
    root.gameObject.SetActive(false);
    root.name = nodeName;
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
  /// <param name="scaleRatio">Additional scale to apply to the pipe texture.</param>
  protected static void RescaleTextureToLength(
      GameObject obj, float samplesPerMeter, Renderer renderer = null, float scaleRatio = 1.0f) {
    var newScale = obj.transform.localScale.z * samplesPerMeter * scaleRatio;
    var mr = renderer ?? obj.GetComponent<Renderer>();
    mr.material.mainTextureScale = new Vector2(mr.material.mainTextureScale.x, newScale);
    if (mr.material.GetTexture(BumpMapProp) != null) {
      var nrmScale = mr.material.GetTextureScale(BumpMapProp);
      mr.material.SetTextureScale(BumpMapProp, new Vector2(nrmScale.x, newScale));
    }
  }

  /// <summary>
  /// Performs a simple collision check for a vector that connects two transforms.
  /// </summary>
  /// <remarks>
  /// The method ignores the colliders that belong to the source or target object hierarchies.
  /// </remarks>
  /// <param name="source">Transform to start from.</param>
  /// <param name="target">Transform to end at.</param>
  /// <param name="radius">Radius of the spehere to use for the check.</param>
  /// <returns>
  /// <c>null</c> if nothing has been hit or a message for the first hit detected.
  /// </returns>
  protected static string DoSimpleSphereCheck(Transform source, Transform target, float radius) {
    var linkVector = target.position - source.position;
    var hits = Physics.SphereCastAll(
        source.position, radius, linkVector, linkVector.magnitude,
        (int)(KspLayerMask.PARTS | KspLayerMask.SURFACE | KspLayerMask.KERBALS),
        QueryTriggerInteraction.Ignore);
    foreach (var hit in hits) {
      var hitPart = hit.transform.root.GetComponent<Part>();
      if (hit.transform.root != source.root && hit.transform.root != target.root) {
        return hitPart != null
            ? LinkCollidesWithObjectMsg.Format(hitPart.partInfo.title)
            : LinkCollidesWithSurfaceMsg.ToString();
      }
    }
    return null;
  }
  #endregion
}

}  // namespace
