// Kerbal Attachment System
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.DebugUtils;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.PartUtils;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KAS {

/// <summary>Base class for the renderers that represent the links as a "pipe".</summary>
// Next localization ID: #kasLOC_07002.
public abstract class AbstractPipeRenderer : AbstractProceduralModel,
    // KAS interfaces.
    ILinkRenderer {

  #region Localizable GUI strings
  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<PartType> LinkCollidesWithObjectMsg = new Message<PartType>(
      "#kasLOC_07000",
      defaultTemplate: "Link collides with: <<1>>",
      description: "Message to display when the link cannot be created due to an obstacle."
      + "\nArgument <<1>> is the part that would collide with the proposed link.",
      example: "Link collides with: Mk2 Cockpit");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message LinkCollidesWithSurfaceMsg = new Message(
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
    /// length is changing as the link's length is updating.
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
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string rendererName = "";

  /// <summary>Diameter of the pipe in meters.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Pipe diameter")]
  public float pipeDiameter = 0.7f;

  /// <summary>Main texture to use for the pipe.</summary>
  /// <seealso cref="pipeTextureRescaleMode"/>
  /// <seealso cref="pipeTextureSamplesPerMeter"/>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Pipe texture")]
  public string pipeTexturePath = "";

  /// <summary>Normals for the main texture. If empty string, then no normals used.</summary>
  /// <seealso cref="pipeTexturePath"/>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Pipe texture NRM")]
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
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Texture samples per meter")]
  public float pipeTextureSamplesPerMeter = 1.0f;

  /// <summary>Defines how the texture should cover the pipe.</summary>
  /// <seealso cref="pipeTexturePath"/>
  /// <seealso cref="pipeTextureSamplesPerMeter"/>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Texture rescale mode")]
  public PipeTextureRescaleMode pipeTextureRescaleMode = PipeTextureRescaleMode.Stretch;

  /// <summary>Defines if pipe's collider should interact with the physics objects.</summary>
  /// <remarks>
  /// If this setting is <c>false</c> the link mesh won't have colliders. It affects how player can
  /// select the part in the scene.
  /// </remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Physical collider")]
  public bool pipeColliderIsPhysical;
  #endregion

  #region ILinkRenderer properties
  /// <inheritdoc/>
  public string cfgRendererName => rendererName;

  /// <inheritdoc/>
  public virtual Color? colorOverride {
    get => _colorOverride;
    set {
      _colorOverride = value;
      UpdateMaterialOverrides();
    }
  }
  Color? _colorOverride;

  /// <inheritdoc/>
  public virtual string shaderNameOverride {
    get => _shaderNameOverride;
    set {
      _shaderNameOverride = value;
      UpdateMaterialOverrides();
    }
  }
  string _shaderNameOverride;

  /// <inheritdoc/>
  public virtual bool isPhysicalCollider {
    get => _isPhysicalCollider;
    set {
      _isPhysicalCollider = value;
      UpdateColliderOverrides();
    }
  }
  bool _isPhysicalCollider = true;  // It's a "forced OFF" setting.

  /// <inheritdoc/>
  public bool isStarted => sourceTransform != null && targetTransform != null;

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
  protected string modelBasename => "$rendererRoot-" + rendererName;

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

  /// <summary>Part that owns the target transform.</summary>
  /// <remarks>
  /// It can be <c>null</c> if the target is not a part or the renderer is not started.
  /// </remarks>
  /// <seealso cref="StartRenderer"/>
  // ReSharper disable once MemberCanBePrivate.Global
  protected Part targetPart { get; private set; }
  #endregion

  #region Local fields and properties
  /// <summary>Coroutine that updates the pipe meshes.</summary>
  /// <remarks>
  /// The only thing it does is calling <see cref="UpdateLink"/> on every frame update.
  /// </remarks>
  Coroutine _linkUpdateCoroutine;
  #endregion

  #region IHasDebugAdjustables overrides
  Transform _dbgOldSource;
  Transform _dbgOldTarget;

  /// <summary>Logs all the part's model objects.</summary>
  [Debug.KASDebugAdjustable("Dump part's model hierarchy")]
  public void ShowHierarchyDbgAction() {
    HostedDebugLog.Warning(this, "Part's model hierarchy:");
    DebugGui.DumpHierarchy(partModelTransform, partModelTransform);
    if (targetTransform != null) {
      HostedDebugLog.Warning(this, "Model hierarchy at target:");
      DebugGui.DumpHierarchy(targetTransform.root, targetTransform);
    }
  }

  /// <inheritdoc/>
  public override void OnBeforeDebugAdjustablesUpdate() {
    base.OnBeforeDebugAdjustablesUpdate();
    _dbgOldSource = sourceTransform;
    _dbgOldTarget = targetTransform;
    StopRenderer();
  }

  /// <inheritdoc/>
  public override void OnDebugAdjustablesUpdated() {
    _pipeMaterial = null;
    base.OnDebugAdjustablesUpdated();
    if (_dbgOldSource != null && _dbgOldTarget != null) {
      HostedDebugLog.Warning(
          this, "Restart renderer: src={0}, tgt={1}", _dbgOldSource, _dbgOldTarget);
      StartRenderer(_dbgOldSource, _dbgOldTarget);
    }
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
    targetPart = targetTransform.GetComponentInParent<Part>();
    CreatePipeMesh();

    // Update the meshes on the source vessel. 
    PartModel.UpdateHighlighters(part);
    sourceTransform.GetComponentsInChildren<Renderer>().ToList()
        .ForEach(r => r.SetPropertyBlock(part.mpb));
    vessel.parts.ForEach(p => SetCollisionIgnores(p, true));

    // Update the target vessel relations (if any).
    if (targetPart != null) {
      PartModel.UpdateHighlighters(targetPart);
      targetTransform.GetComponentsInChildren<Renderer>().ToList()
          .ForEach(r => r.SetPropertyBlock(targetPart.mpb));
      if (targetPart.vessel != vessel) {
        targetPart.vessel.parts.ForEach(p => SetCollisionIgnores(p, true));
      }
    }

    RegisterGameEventListener(GameEvents.onPartCoupleComplete, OnPartCoupleCompleteEvent);
    RegisterGameEventListener(GameEvents.onPartDeCouple, OnPartDeCoupleEvent);
    RegisterGameEventListener(GameEvents.onPartDeCoupleComplete, OnPartDeCoupleCompleteEvent);

    _linkUpdateCoroutine = StartCoroutine(UpdateLinkCoroutine());
  }

  /// <inheritdoc/>
  public virtual void StopRenderer() {
    // Stop meshes updates.
    if (_linkUpdateCoroutine != null) {
      HostedDebugLog.Fine(this, "Stopping renderer updates...");
      StopCoroutine(_linkUpdateCoroutine);
      _linkUpdateCoroutine = null;
    }

    // Sync the renderers settings to the source part to handle the highlights.
    if (isStarted) {
      sourceTransform.GetComponentsInChildren<Renderer>().ToList()
          .ForEach(r => r.SetPropertyBlock(part.mpb));
      targetTransform.GetComponentsInChildren<Renderer>().ToList()
          .ForEach(r => r.SetPropertyBlock(part.mpb));
    }

    DestroyPipeMesh();
    PartModel.UpdateHighlighters(part);

    // Update the target vessel relations (if any).
    if (targetPart != null) {
      PartModel.UpdateHighlighters(targetPart);
      if (targetPart.vessel != vessel) {
        targetPart.vessel.parts
            .Where(p => p != null)  // It's a cleanup method.
            .ToList()
            .ForEach(p => SetCollisionIgnores(p, false));
      }
    }

    targetPart = null;
    sourceTransform = null;
    targetTransform = null;

    GameEvents.onPartCoupleComplete.Remove(OnPartCoupleCompleteEvent);
    GameEvents.onPartDeCouple.Remove(OnPartDeCoupleEvent);
    GameEvents.onPartDeCoupleComplete.Remove(OnPartDeCoupleCompleteEvent);
  }

  /// <inheritdoc/>
  public string[] CheckColliderHits(Transform source, Transform target) {
    if (!pipeColliderIsPhysical) {
      return new string[0];  // No need to check, the meshes will never collide.
    }
    // HACK: Start the renderer before getting the pipes. 
    var oldStartState = isStarted;
    var oldPhysicalState = isPhysicalCollider;
    if (!isStarted) {
      isPhysicalCollider = false;
      StartRenderer(source, target);
    } else if (sourceTransform != source || targetTransform != target) {
      HostedDebugLog.Error(this, "Cannot verify hits on a started renderer");
    }
    var points = GetPipePath(source, target);
    if (!oldStartState) {
      StopRenderer();
      isPhysicalCollider = oldPhysicalState;
    }

    var hitParts = new HashSet<Part>();
    for (var i = 0; i < points.Length - 1; i++) {
      CheckHitsForCapsule(points[i + 1], points[i], pipeDiameter, target, hitParts);
    }
    var hitMessages = new List<string>();
    foreach (var hitPart in hitParts) {
      hitMessages.Add(hitPart != null
          ? LinkCollidesWithObjectMsg.Format(hitPart)
          : LinkCollidesWithSurfaceMsg.Format());
    }
    return hitMessages.ToArray();
  }

  /// <inheritdoc/>
  public abstract Transform GetMeshByName(string meshName);
  #endregion

  #region Inheritable methods
  /// <inheritdoc/>
  public abstract void UpdateLink();

  /// <summary>Creates the dynamic pipe mesh(-es).</summary>
  /// <remarks>
  /// The source and target must be already set at the moment of this method called. However,
  /// it may be called without the prior call to <see cref="DestroyPipeMesh"/>. So any existing mesh
  /// should be handled accordingly (e.g. destroyed and re-created).
  /// <para>
  /// All the meshes are expected to belong to either <see cref="sourceTransform"/> or
  /// <see cref="targetTransform"/>. If it's not the case, then the creator must handle collision
  /// ignores and highlighter modules. Same applies to the meshes created outside of this method,
  /// even when they belong to the proper source or target transform. 
  /// </para>
  /// </remarks>
  /// <seealso cref="StartRenderer"/>
  /// <seealso cref="SetCollisionIgnores"/>
  protected abstract void CreatePipeMesh();

  /// <summary>Destroys the dynamic pipe mesh(-es).</summary>
  /// <remarks>
  /// This is a cleanup method. It may be called at any time and must always succeed. Note, that
  /// when this method is called the part's config may not be available. 
  /// </remarks>
  /// <seealso cref="StopRenderer"/>
  protected abstract void DestroyPipeMesh();

  /// <summary>Gives an approximate path to verify pipe collisions.</summary>
  /// <remarks>
  /// This method is only called on the started renderer, and only if the pipe mode is physical.
  /// In non-physical mode any pipe is safe, so no check is done.
  /// <para>
  /// The path is used to move a sphere collider to determine if the pipe mesh collides with
  /// anything. So pay attention to the endpoints, since the sphere will go beyond the end points by
  /// the pipe's radius distance. In most cases it's not an issue since the renderer meshes are not
  /// supposed to collide with both the source and the target vessels. However, surface can be hit
  /// if the target part is too close to it and doesn't give enough offset.
  /// </para>
  /// </remarks>
  /// <returns>
  /// An empty array if no checks should be done, or a list of control points (minimum length is 2).
  /// </returns>
  protected abstract Vector3[] GetPipePath(Transform start, Transform end);

  /// <summary>Updates the pipe material(s) to the current module's state.</summary>
  /// <remarks>It is called when the material mutable settings are changed.</remarks>
  /// <seealso cref="colorOverride"/>
  /// <seealso cref="shaderNameOverride"/>
  protected abstract void UpdateMaterialOverrides();

  /// <summary>Updates the pipe collider(s) to the current module's state.</summary>
  /// <remarks>It is called when the collider mutable settings are changed.</remarks>
  /// <seealso cref="isPhysicalCollider"/>
  /// <seealso cref="pipeColliderIsPhysical"/>
  protected abstract void UpdateColliderOverrides();
  #endregion

  #region Utility methods
  /// <summary>
  /// Enables or disables the collisions between the pipe meshes and the provided part.
  /// </summary>
  /// <remarks>
  /// There are multiple cases when this method can be called with different arguments. Some, but
  /// not all are: make a link, couple a part, de-couple a part, create a new mesh, etc.
  /// </remarks>
  /// <param name="otherPart">The part to update the colliders for.</param>
  /// <param name="ignore">Tells if the collision ignores must be set or reset.</param>
  /// <seealso cref="targetPart"/>
  protected abstract void SetCollisionIgnores(Part otherPart, bool ignore);
  #endregion

  #region Local utility methods
  /// <summary>Checks if a capsule collider between the points hits anything.</summary>
  /// <remarks>Hits with own vessel models or models of the other vessel are ignored.</remarks>
  /// <param name="startPos">The starting point of the link.</param>
  /// <param name="endPos">The ending point of the link.</param>
  /// <param name="diameter">The diameter of the capsule.</param>
  /// <param name="target">The target object. It will be used to obtain the target vessel.</param>
  /// <param name="hits">The hash to store the hit parts.</param>
  void CheckHitsForCapsule(Vector3 startPos, Vector3 endPos, float diameter,
                           Transform target, HashSet<Part> hits) {
    var tgtPart = target.root.GetComponent<Part>();
    var otherVessel = tgtPart != null ? tgtPart.vessel : null;
    var colliders = Physics.OverlapCapsule(
        startPos, endPos, diameter / 2.0f,
        (int)(KspLayerMask.Part | KspLayerMask.SurfaceCollider | KspLayerMask.Kerbal),
        QueryTriggerInteraction.Ignore);
    foreach (var collider in colliders) {
      var hitPart = collider.transform.root.GetComponent<Part>();
      if (hitPart != null) {
        if (hitPart.vessel != vessel && hitPart.vessel != otherVessel) {
          hits.Add(hitPart);
        }
      } else {
        hits.Add(null);  // Surface hit.
      }
    }
  }
  #endregion

  #region Collision ignores tracking code (tricky!)
  /// <summary>
  /// Intermediate field to save the vessel between starting and ending of the part decoupling
  /// event.
  /// </summary>
  Vessel _formerTargetVessel;

  /// <summary>Reacts on a part coupling and adjusts its colliders as needed.</summary>
  /// <remarks>
  /// The pipe meshes should not collide with the target vessel. So track the part changes on the
  /// target vessel and disable collisions on the newly appeared parts.
  /// </remarks>
  /// <param name="action">The callback action.</param>
  void OnPartCoupleCompleteEvent(GameEvents.FromToAction<Part, Part> action) {
    if (targetPart != null && targetPart.vessel != vessel
        && (action.from.vessel == targetPart.vessel || action.to.vessel == targetPart.vessel)) {
      if (action.from == targetPart) {
        // The target part has couple to a new vessel.
        HostedDebugLog.Fine(this, "Set collision ignores on: {0}", action.to.vessel);
        action.to.vessel.parts
            .ForEach(p => SetCollisionIgnores(p, true));
      } else {
        // A part has joined the target vessel.
        HostedDebugLog.Fine(this, "Set collision ignores on: {0}", action.from);
        SetCollisionIgnores(action.from, true);
      }
    }
  }

  /// <summary>Records the owner vessel of the part being de-coupled.</summary>
  /// <remarks>
  /// This information will be used down the stream to detect if the collisions should be adjusted.
  /// </remarks>
  /// <param name="originator">The part that has decoupled.</param>
  void OnPartDeCoupleEvent(Part originator) {
    if (targetPart != null && targetPart.vessel != vessel
        && originator.vessel == targetPart.vessel) {
      _formerTargetVessel = originator.vessel;
    }
  }

  /// <summary>Reacts on a part de-coupling and adjusts its colliders as needed.</summary>
  /// <remarks>
  /// When a part is leaving the target vessel, the collisions between this part and the pipe meshes
  /// must be restored.
  /// </remarks>
  /// <param name="originator">The part that has decoupled.</param>
  void OnPartDeCoupleCompleteEvent(Part originator) {
    if (_formerTargetVessel != null && originator.vessel != _formerTargetVessel) {
      // It's either the target part has decoupled from its vessel, or the owner vessel has
      // abandoned the target part.
      var leavingVessel = originator == targetPart ? _formerTargetVessel : originator.vessel;
      HostedDebugLog.Fine(this, "Restore collision ignores on: {0}", leavingVessel);
      leavingVessel.parts
          .ForEach(p => SetCollisionIgnores(p, false));
    }
    _formerTargetVessel = null;
  }

  /// <summary>Calls renderer updates as long as the renderer is started.</summary>
  /// <seealso cref="_linkUpdateCoroutine"/>
  /// <seealso cref="StartRenderer"/>
  IEnumerator UpdateLinkCoroutine() {
    HostedDebugLog.Fine(this, "Staring renderer updates...");
    while (isStarted) {
      UpdateLink();
      yield return null;
    }
    // The coroutine is expected to be terminated explicitly!
    HostedDebugLog.Warning(this, "Terminate coroutine on renderer stop!");
  }
  #endregion
}

}  // namespace
