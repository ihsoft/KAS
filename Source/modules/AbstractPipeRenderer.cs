// Kerbal Attachment System
// https://forum.kerbalspaceprogram.com/index.php?/topic/142594-15-kerbal-attachment-system-kas-v11
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KASAPIv2;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.DebugUtils;
using KSPDev.KSPInterfaces;
using KSPDev.ModelUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>Base class for the renderers that represent the links as a "pipe".</summary>
/// <remarks>
/// <para>
/// The descendants of this module can use the custom persistent fields of groups:
/// </para>
/// <list type="bullet">
/// <item><c>StdPersistentGroups.PartConfigLoadGroup</c></item>
/// <item><c>StdPersistentGroups.PartPersistant</c></item>
/// </list>
/// </remarks>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.PersistentFieldAttribute']/*"/>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.StdPersistentGroups']/*"/>
// Next localization ID: #kasLOC_07002.
public abstract class AbstractPipeRenderer : AbstractProceduralModel,
    // KAS interfaces.
    ILinkRenderer,
    // KPSDev sugar interfaces.    
    IsDestroyable,
    // KSPDev interfaces
    IHasDebugAdjustables {

  #region Localizable GUI strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  public static readonly Message<PartType> LinkCollidesWithObjectMsg = new Message<PartType>(
      "#kasLOC_07000",
      defaultTemplate: "Link collides with: <<1>>",
      description: "Message to display when the link cannot be created due to an obstacle."
      + "\nArgument <<1>> is the part that would collide with the proposed link.",
      example: "Link collides with: Mk2 Cockpit");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  public static readonly Message LinkCollidesWithSurfaceMsg = new Message(
      "#kasLOC_07001",
      defaultTemplate: "Link collides with the surface",
      description: "Message to display when the link strut orientation cannot be changed due to it"
      + " would hit the surface.");
  #endregion

  #region Internal types
  /// <summary>
  /// Mode of adjusting the main texture (and its normals map) when the pipe length is changed.
  /// </summary>
  public enum PipeTextureRescaleMode {
    /// <summary>
    /// Texture stretches to the pipe's size. The resolution of the texture per meter of the link's
    /// length is chnaging as the link's length is updating.
    /// </summary>
    /// <seealso cref="pipeTextureSamplesPerMeter"/>
    Stretch,
  
    /// <summary>
    /// Texture is tiled starting from the source to the target. The resolution of the texture per
    /// meter of the link's length is kept constant and depends on the part's settings.
    /// </summary>
    /// <seealso cref="pipeTextureSamplesPerMeter"/>
    TileFromSource,
  
    /// <summary>
    /// Texture is tiled starting from the target to the source. The resolution of the texture per
    /// meter of the link's length is kept constant and depends on the part's settings.
    /// </summary>
    /// <seealso cref="pipeTextureSamplesPerMeter"/>
    TileFromTarget,
  }
  #endregion

  #region Part's config fields
  /// <summary>Name of the renderer for this procedural part.</summary>
  /// <remarks>
  /// This setting is used to let link source know the primary renderer for the linked state.
  /// </remarks>
  /// <seealso cref="ILinkSource"/>
  /// <seealso cref="ILinkRenderer.cfgRendererName"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string rendererName = "";

  /// <summary>Diameter of the pipe in meters.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [KASDebugAdjustable("Pipe diameter")]
  public float pipeDiameter = 0.7f;

  /// <summary>Main texture to use for the pipe.</summary>
  /// <seealso cref="pipeTextureRescaleMode"/>
  /// <seealso cref="pipeTextureSamplesPerMeter"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [KASDebugAdjustable("Pipe texture")]
  public string pipeTexturePath = "KAS/TExtures/hose-d70-1kn";

  /// <summary>Normals for the main texture. If empty string, then no normals used.</summary>
  /// <seealso cref="pipeTexturePath"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [KASDebugAdjustable("Pipe texture NRM")]
  public string pipeNormalsTexturePath = "";

  /// <summary>
  /// Defines how many texture samples to apply per one meter of the pipe's length.
  /// </summary>
  /// <remarks>
  /// This setting is ignored if the texture rescale mode is
  /// <see cref="PipeTextureRescaleMode.Stretch"/>.
  /// </remarks>
  /// <seealso cref="pipeTexturePath"/>
  /// <seealso cref="pipeTextureRescaleMode"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [KASDebugAdjustable("Texture samples per meter")]
  public float pipeTextureSamplesPerMeter = 1.0f;

  /// <summary>Defines how the texture should cover the pipe.</summary>
  /// <seealso cref="pipeTexturePath"/>
  /// <seealso cref="pipeTextureSamplesPerMeter"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [KASDebugAdjustable("Texture rescale mode")]
  public PipeTextureRescaleMode pipeTextureRescaleMode = PipeTextureRescaleMode.Stretch;

  /// <summary>Defines if pipe's collider should interact with the physics objects.</summary>
  /// <remarks>
  /// If this setting is <c>false</c> the link mesh still may have a collider, but it will not
  /// trigger physical effects.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [KASDebugAdjustable("Physical collider")]
  public bool pipeColliderIsPhysical;
  #endregion

  #region ILinkRenderer properties
  /// <inheritdoc/>
  public string cfgRendererName { get { return rendererName; } }

  /// <inheritdoc/>
  public virtual Color? colorOverride {
    get { return _colorOverride; }
    set {
      _colorOverride = value;
      if (sourceTransform != null) {
        Meshes.UpdateMaterials(
            sourceTransform.gameObject, newColor: _colorOverride ?? materialColor);
      }
    }
  }
  Color? _colorOverride;

  /// <inheritdoc/>
  public virtual string shaderNameOverride {
    get { return _shaderNameOverride; }
    set {
      _shaderNameOverride = value;
      if (sourceTransform != null) {
        Meshes.UpdateMaterials(
            sourceTransform.gameObject, newShaderName: _shaderNameOverride ?? shaderName);
      }
    }
  }
  string _shaderNameOverride;

  /// <inheritdoc/>
  public virtual bool isPhysicalCollider {
    get { return pipeColliderIsPhysical; }
    set {
      pipeColliderIsPhysical = value;
      if (sourceTransform != null) {
        Colliders.UpdateColliders(sourceTransform.gameObject, isEnabled: value);
      }
    }
  }

  /// <inheritdoc/>
  public bool isStarted {
    get { return sourceTransform != null && targetTransform != null; }
  }

  /// <inheritdoc/>
  public Transform sourceTransform { get; private set; }

  /// <inheritdoc/>
  public Transform targetTransform { get; private set; }
  #endregion

  #region Inheritable fields and properties
  /// <summary>Basename of the module's model objects.</summary>
  /// <remarks>
  /// Use this name as a part of the full name to any dynamically created object within the module.
  /// The name is guaranteed to be unique in the part's hierarchy, so it can always be looked up
  /// once the object is created.
  /// </remarks>
  protected string ModelBasename {
    get { return "$rendererRoot-" + rendererName; }
  }

  /// <summary>Material to use for the pipe elements.</summary>
  /// <remarks>It doesn't consider shader or color overrides.</remarks>
  protected Material pipeMaterial {
    get {
      if (_pipeMaterial == null) {
        _pipeMaterial = CreateMaterial(
            GetTexture(pipeTexturePath), mainTexNrm: GetNormalMap(pipeNormalsTexturePath));
      }
      return _pipeMaterial;
    }
  }
  Material _pipeMaterial;
  #endregion

  #region IHasDebugAdjustables implementation
  Transform dbgOldSource;
  Transform dbgOldTarget;

  /// <inheritdoc/>
  public virtual void OnBeforeDebugAdjustablesUpdate() {
    dbgOldSource = sourceTransform;
    dbgOldTarget = targetTransform;
    StopRenderer();
  }

  /// <inheritdoc/>
  public virtual void OnDebugAdjustablesUpdated() {
    _pipeMaterial = null;
    LoadPartModel();
    if (dbgOldSource != null && dbgOldTarget != null) {
      StartRenderer(dbgOldSource, dbgOldTarget);
    }
  }
  #endregion

  #region IsDestroyable implementation
  /// <inheritdoc/>
  public virtual void OnDestroy() {
    StopRenderer();
  }
  #endregion

  #region ILinkRenderer implemetation
  /// <inheritdoc/>
  public virtual void StartRenderer(Transform source, Transform target) {
    if (isStarted) {
      if (sourceTransform == source && targetTransform == target) {
        return;  // NO-OP
      }
      StopRenderer();
    }
    sourceTransform = source;
    targetTransform = target;
    CreatePipeMesh();
    UpdateMeshes();
  }

  /// <inheritdoc/>
  public virtual void StopRenderer() {
    DestroyPipeMesh();
    sourceTransform = null;
    targetTransform = null;
  }

  /// <inheritdoc/>
  public virtual string[] CheckColliderHits(Transform source, Transform target) {
    var hitParts = new HashSet<Part>();
    var ignoreRoots = new HashSet<Transform>() {source.root, target.root};
    var points = GetPipePath(source, target);
    for (var i = 0; i < points.Length - 1; i++) {
      CheckHitsForCapsule(points[i + 1], points[i], pipeDiameter, hitParts, ignoreRoots);
    }
    var hitMessages = new List<string>();
    foreach (var hitPart in hitParts) {
      hitMessages.Add(hitPart != null
          ? LinkCollidesWithObjectMsg.Format(hitPart)
          : LinkCollidesWithSurfaceMsg.Format());
    }
    return hitMessages.ToArray();
  }
  #endregion

  #region AbstractProceduralModel overrides
  /// <inheritdoc/>
  public override void OnUpdate() {
    base.OnUpdate();
    if (isStarted) {
      UpdateLink();
    }
  }
  #endregion

  #region Inheritable methods
  /// <inheritdoc/>
  public abstract void UpdateLink();

  /// <summary>Creates the dynamic pipe mesh(-es).</summary>
  /// <remarks>
  /// The source and target must be already set at the moment of this method called. However,
  /// it may be called without the prior call to <see cref="DestroyPipeMesh"/>. So any existing mesh
  /// should be handled accordingly (e.g. destroyed and re-created).
  /// </remarks>
  /// <seealso cref="StartRenderer"/>
  protected abstract void CreatePipeMesh();

  /// <summary>Destroys the dynamic pipe mesh(-es).</summary>
  /// <remarks>This is a cleanup method. It must always succeed.</remarks>
  /// <seealso cref="StopRenderer"/>
  protected abstract void DestroyPipeMesh();

  /// <summary>Gives an approximate path for the collision check.</summary>
  /// <remarks>
  /// <para>
  /// If there is a real path, then there can be any number of points, but not less than two. If no
  /// check is needed, then return an empty array.
  /// </para>
  /// <para>If there is a part returned, then it's used to move a spehere collider along it to
  /// determine if the pipe mesh collides to anything.
  /// </para>
  /// </remarks>
  /// <returns>The control points or empty array.</returns>
  protected abstract Vector3[] GetPipePath(Transform start, Transform end);
  #endregion

  #region Utility methods
  /// <summary>Updates the part to catch up with the dynamic meshes updates.</summary>
  /// <remarks>
  /// Call it if new dynamic meshes are created outside of the <see cref="CreatePipeMesh"/> method.
  /// </remarks>
  protected void UpdateMeshes() {
    CollisionManager.IgnoreCollidersOnVessel(
        vessel, sourceTransform.root.GetComponentsInChildren<Collider>());
    Colliders.SetCollisionIgnores(sourceTransform.root, targetTransform.root, true);
    part.RefreshHighlighter();
  }
  #endregion

  #region Local utility methods
  /// <summary>Checks if a capsule collider between the points hit's parts.</summary>
  /// <param name="startPos">The starting point of the link.</param>
  /// <param name="endPos">The ending point of the link.</param>
  /// <param name="diameter">The diameter of the capsule.</param>
  /// <param name="hits">The hash to store the hit parts.</param>
  /// <param name="ignoreRoots">
  /// The list of the root transforms for which the collisions should be ignored. To ignore a hit
  /// with a particular part, simple provide it's transform root here.  
  /// </param>
  void CheckHitsForCapsule(Vector3 startPos, Vector3 endPos, float diameter,
                           HashSet<Part> hits, HashSet<Transform> ignoreRoots) {
    var linkVector = endPos - startPos;
    var linkLength = linkVector.magnitude;
    Collider[] colliders;
    if (linkLength >= diameter) {
      // The spheres at the ends of the capsule can hit undesired parts, so reduce the capsule size
      // so that the sphere edges are located at the start/end positions. This way some useful hits
      // may get missed, but it's the price.
      var linkDirection = linkVector.normalized;
      colliders = Physics.OverlapCapsule(
          startPos + linkDirection * diameter / 2.0f,
          endPos - linkDirection * diameter / 2.0f,
          diameter / 2.0f,
          (int)(KspLayerMask.Part | KspLayerMask.SurfaceCollider | KspLayerMask.Kerbal),
          QueryTriggerInteraction.Ignore);
    } else {
      // EDGE CASE. There is no reliable way to check the hits when the distance is less than the
      // pipe's diameter due to the minimum possible capsule shape is a sphere. As a fallback, check
      // a sphere, placed between the points.
      colliders = Physics.OverlapSphere(
          startPos + linkVector / 2.0f, linkLength / 2.0f,
          (int)(KspLayerMask.Part | KspLayerMask.SurfaceCollider | KspLayerMask.Kerbal),
          QueryTriggerInteraction.Ignore);
    }
    foreach (var collider in colliders) {
      if (!ignoreRoots.Contains(collider.transform.root)) {
        var hitPart = collider.transform.root.GetComponent<Part>();
        if (hitPart != null) {
          hits.Add(hitPart);
        } else {
          hits.Add(null);
        }
      }
    }
  }
  #endregion
}

}  // namespace
