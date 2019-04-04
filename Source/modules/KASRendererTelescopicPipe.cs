// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
using KSPDev.DebugUtils;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.PartUtils;
using KSPDev.ConfigUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>
/// Module that keeps all pieces of the link in the model. I.e. it's a material representation of
/// the part that can link to another part.
/// </summary>
// Next localization ID: #kasLOC_04004.
public sealed class KASRendererTelescopicPipe : AbstractProceduralModel,
    // KAS interfaces.
    ILinkRenderer,
    // KSPDev interfaces.
    IHasContextMenu, IHasDebugAdjustables {

  #region Localizable GUI strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<PartType> LinkCollidesWithObjectMsg = new Message<PartType>(
      "#kasLOC_04000",
      defaultTemplate: "Link collides with: <<1>>",
      description: "Message to display when the link cannot be created due to an obstacle."
      + "\nArgument <<1>> is the part that would collide with the proposed link.",
      example: "Link collides with: Mk2 Cockpit");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message LinkCollidesWithSurfaceMsg = new Message(
      "#kasLOC_04001",
      defaultTemplate: "Link collides with the surface",
      description: "Message to display when the link strut orientation cannot be changed due to it"
      + " would hit the surface.");
  #endregion

  #region Persistent fields
  /// <summary>Orientation of the unlinked strut.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public Vector3 persistedParkedOrientation = Vector3.forward;

  /// <summary>Extended length of the unlinked strut.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public float persistedParkedLength = 0;  // If 0 then minimum link length will be used.
  #endregion

  #region Part's config fields
  /// <summary>Name of the renderer for this procedural part.</summary>
  /// <remarks>
  /// This setting is used to let link source know primary renderer for the linked state.
  /// </remarks>
  /// <seealso cref="ILinkSource"/>
  /// <seealso cref="ILinkRenderer.cfgRendererName"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string rendererName = "";

  /// <summary>
  /// Model for a joint lever at the source part. Two such models are used to form a complete joint.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("SOURCE lever model")]
  public string sourceJointModel = "KAS/Models/Joint/model";

  /// <summary>
  /// Model for a joint lever at the target part. Two such models are used to form a complete joint.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("TARGET lever model")]
  public string targetJointModel = "KAS/Models/Joint/model";

  /// <summary>Model for the pistons.</summary>
  /// <remarks>
  /// The piston model will be scaled to the part's model scale. When it's not desirable, use
  /// <see cref="pistonModelScale"/> to compensate the scale change.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("PISTON model")]
  public string pistonModel = "KAS/Models/Piston/model";

  /// <summary>Number of pistons in the link.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Number of pistons")]
  public int pistonsCount = 3;

  /// <summary>Scale of the piston comparing to the prefab.</summary>
  /// <remarks>
  /// Piston's model from prefab will be scaled by this value. X&amp;Y axes affect diameter, Z
  /// affects the length.
  /// <para>
  /// <i>NOTE:</i> as of now X and Y scales must be equal. Otherwise pipe model will get broken.
  /// </para>
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Piston model scale")]
  public Vector3 pistonModelScale = Vector3.one;

  /// <summary>
  /// Allows random rotation of pistons relative to each other around Z (length) axis. If piston's
  /// model has a complex texture this setting may be used to make telescopic pipe less repeatative.
  /// </summary>
  /// <remarks>
  /// Piston's model from prefab will be scaled by this value. X&amp;Y axes affect diameter, Z
  /// affects the length. Note that if X and Y are not equal you may want to disable
  /// <see cref="pistonModelRandomRotation"/>.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Randomize pistons rotation")]
  public bool pistonModelRandomRotation = true;

  /// <summary>Amount to decrease the scale of an inner pistons diameter.</summary>
  /// <remarks>
  /// To keep models consistent every nested piston must be slightly less in diameter than the
  /// parent. This value is a delta to decrease scale of every nested piston comparing to the prefab
  /// model.
  /// <para>
  /// E.g. given this setting is 0.1f and there are 3 pistons the scales will be like this:
  /// <list type="number">
  /// <item>Top most (outer) piston: <c>1.0f</c>.</item>
  /// <item>Middle piston: <c>0.9f</c>.</item>
  /// <item>Last piston: <c>0.8f</c>.</item>
  /// </list>
  /// </para>  
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Piston diameter scale delta")]
  public float pistonDiameterScaleDelta = 0.1f;

  /// <summary>Minimum allowed overlap of the pistons in the extended state in meters.</summary>
  /// <remarks>
  /// <para>
  /// Used to determine the minimum and maximum lengths of the link. This length limit is only
  /// applied when rendering the link to keep it consistent, it does not affect the link
  /// constraints.
  /// </para>
  /// <para>
  /// This value is affected by the part's scale. I.e. if it's <c>0.02m</c>, and the part's scale is
  /// <c>2.0</c>, then the actual shift will be <c>0.04m</c>.
  /// </para>
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Piston min shift")]
  public float pistonMinShift = 0.02f;

  /// <summary>
  /// Container for the menu item description which tells how to park the unlinked strut.
  /// </summary>
  public class Orientation {
    /// <summary>Direction of the pipe.</summary>
    [PersistentField("direction")]
    public Vector3 direction = Vector3.forward;

    /// <summary>User friendly name for a menu item to adjust unlinked strut orientation.</summary>
    [PersistentField("title")]
    public string title = "";
  }

  /// <summary>List of the available menu items for the unlinked pipe oriaentation.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [PersistentField("parkedOrientation", isCollection = true,
                   group = StdPersistentGroups.PartConfigLoadGroup)]
  public List<Orientation> parkedOrientations = new List<Orientation>();
  #endregion

  // FIXME: check colliders.
  #region Context menu events/actions
  /// <summary>Event handler. Extends unlinked strut at maximum length.</summary>
  /// <seealso cref="maxLinkLength"/>
  [KSPEvent(guiActiveUnfocused = true, guiActiveEditor = true, active = false)]
  [LocalizableItem(
      tag = "#kasLOC_04002",
      defaultTemplate = "Extend to max",
      description = "A context menu item that expands a non-linked telescopic pipe to its maximum"
      + " length.")]
  public void ExtendAtMaxMenuAction() {
    persistedParkedLength = maxLinkLength;
    UpdateLinkLengthAndOrientation();
  }

  /// <summary>Event handler. Retracts unlinked strut to the minimum length.</summary>
  /// <seealso cref="minLinkLength"/>
  [KSPEvent(guiActiveUnfocused = true, guiActiveEditor = true, active = false)]
  [LocalizableItem(
      tag = "#kasLOC_04003",
      defaultTemplate = "Retract to min",
      description = "A context menu item that shrinks a non-linked telescopic pipe to its minimum"
      + " length.")]
  public void RetractToMinMenuAction() {
    persistedParkedLength = minLinkLength;
    UpdateLinkLengthAndOrientation();
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
      Meshes.UpdateMaterials(srcPartJoint.gameObject, newColor: _colorOverride ?? materialColor);
    }
  }
  Color? _colorOverride;

  /// <inheritdoc/>
  public string shaderNameOverride {
    get { return _shaderNameOverride; }
    set {
      _shaderNameOverride = value;
      // FIXME: update material everywhere.
      // Only update shader on the source joint object since all other objects (pistons and target
      // joint) are children and will be updated hierarchically.
      Meshes.UpdateMaterials(
          srcPartJoint.gameObject, newShaderName: _shaderNameOverride ?? shaderName);
    }
  }
  string _shaderNameOverride;

  /// <inheritdoc/>
  public bool isPhysicalCollider {
    get { return _isPhysicalCollider; }
    set {
      _isPhysicalCollider = value;
      Colliders.UpdateColliders(srcPartJoint.gameObject, isEnabled: value);
    }
  }
  bool _isPhysicalCollider;

  /// <inheritdoc/>
  public bool isStarted {
    get { return sourceTransform != null && targetTransform != null; }
  }

  /// <inheritdoc/>
  public Transform sourceTransform {
    get { return _sourceTransform; }
    private set {
      _sourceTransform = value;
      UpdateLinkLengthAndOrientation();
    }
  }
  Transform _sourceTransform;

  /// <inheritdoc/>
  public Transform targetTransform {
    get { return _targetTransform; }
    private set {
      _targetTransform = value;
      UpdateLinkLengthAndOrientation();
    }
  }
  Transform _targetTransform;
  #endregion

  #region Model name constants
  /// <summary>A transform that is a root for the whole pipe modelset.</summary>
  /// <remarks>It doesn't have to match part's attach node transform.</remarks>
  const string AttachNodeObjName = "plugNode";

  /// <summary>Name of model that connects pipe with the source part.</summary>
  const string SrcPartJointObjName = "srcPartJoint";

  /// <summary>Name of model at the pipe start.</summary>
  const string SrcStrutJointObjName = "srcStrutJoint";

  /// <summary>Name of model at the pipe end.</summary>
  const string TgtStrutJointObjName = "tgtStrutJoint";

  /// <summary>Name of model that connects pipe with the target part.</summary>
  const string TgtPartJointObjName = "tgtPartJoint";

  /// <summary>
  /// Name of the joint model in the part's model. It's used as a template to create all the joint
  /// levers.
  /// </summary>
  const string JointModelName = "Joint";

  /// <summary>
  /// Name of the transform that is used to connect two levers to form a complete joint. 
  /// </summary>
  const string PivotAxleTransformName = "PivotAxle";

  /// <summary>Name of the piston object in the piston's model.</summary>
  const string PistonModelName = "Piston";
  #endregion

  #region Model transforms & properties
  /// <summary>Model that represents a joint at the source part.</summary>
  /// <value>An object in the part's model. It's never <c>null</c>.</value>
  Transform srcPartJoint;

  /// <summary>Pivot axis object on the source joint.</summary>
  /// <value>An object in the part's model. It's never <c>null</c>.</value>
  Transform srcPartJointPivot;

  /// <summary>Model at the pipe's start that connects to the source joint pivot.</summary>
  /// <value>An object in the part's model. It's never <c>null</c>.</value>
  /// <remarks>It's orientation is reversed. I.e .it "looks" at the source joint pivot.</remarks>
  Transform srcStrutJoint;

  /// <summary>Model at the pipe's end that connects to the target joint pivot.</summary>
  /// <value>An object in the part's model. It's never <c>null</c>.</value>
  Transform tgtStrutJoint;

  /// <summary>Pivot axis object at the pipe's end.</summary>
  /// <value>An object in the part's model. It's never <c>null</c>.</value>
  Transform tgtStrutJointPivot;

  /// <summary>Pistons that form the strut pipe.</summary>
  /// <value>A list of meshes.</value>
  GameObject[] pistons;

  /// <summary>
  /// Distance of the source part joint pivot from it's base. It's calculated from the model.
  /// </summary>
  /// <value>The distance in meters.</value>
  float srcJointHandleLength;

  /// <summary>
  /// Distance of the target part joint pivot from it's base. It's calculated from the model.
  /// </summary>
  /// <value>The distance in meters.</value>
  float tgtJointHandleLength;

  /// <summary>
  /// The minimum length to which the telescopic pipe can shrink. It's calculated from the model.
  /// </summary>
  /// <value>The distance in meters.</value>
  float minLinkLength;

  /// <summary>
  /// The maximum length to which the telescopic pipe can expand. It's calculated from the model.
  /// </summary>
  /// <value>The distance in meters.</value>
  float maxLinkLength;

  /// <summary>Diameter of the outer piston. It's calculated from the model.</summary>
  /// <value>The diameter in metters.</value>
  /// <remarks>It's primarily used to cast a collider.</remarks>
  /// <seealso cref="CheckColliderHits"/>
  float outerPistonDiameter;

  /// <summary>Length of a single piston. It's calculated from the model.</summary>
  /// <value>The distance in meters.</value>
  float pistonLength;

  /// <summary>Prefab for the piston models.</summary>
  /// <value>A model reference from the part's model. It's not a copy!</value>
  GameObject pistonPrefab {
    get {
      return GameDatabase.Instance.GetModelPrefab(pistonModel).transform
          .Find(PistonModelName).gameObject;
    }
  }

  /// <summary>The scale of the strut models.</summary>
  /// <remarks>
  /// The scale of the part must be "even", i.e. all the components in the scale vector must be
  /// equal. If they are not, then the renderers behavior may be inconsistent.
  /// </remarks>
  /// <value>The scale to be applied to all the components.</value>
  float strutScale {
    get {
      if (_strutScale < 0) {
        var scale = plugNodeTransform.lossyScale;
        if (Mathf.Abs(scale.x - scale.y) > 1e-05 || Mathf.Abs(scale.x - scale.z) > 1e-05) {
          HostedDebugLog.Error(this, "Uneven part scale is not supported: {0}",
                               DbgFormatter.Vector(scale));
        }
        _strutScale = scale.x;
      }
      return _strutScale;
    }
  }
  float _strutScale = -1;

  /// <summary>The root node for the telescopic strut.</summary>
  /// <remarks>
  /// All the components are built relative to this node. It's also used to determine the part's
  /// model scale, whic is important for rednering the proper meshes.
  /// </remarks>
  Transform plugNodeTransform {
    get {
      if (_plugNodeTransform == null) {
        _plugNodeTransform = part.FindModelTransform("plugNode");
      }
      return _plugNodeTransform;
    }
  }
  Transform _plugNodeTransform;

  /// <summary>Tells if the source on the part is linked.</summary>
  /// <value>The current state of the link.</value>
  bool isLinked {
    get { return sourceTransform != null && targetTransform != null; }
  }
  #endregion

  #region Local fields & properties
  /// <summary>Instances of the events, that were created for orientation menu items.</summary>
  readonly List<BaseEvent> injectedOrientationEvents = new List<BaseEvent>();
  #endregion

  #region IHasDebugAdjustables implementation
  /// <summary>Dumps basic constraints of the renderer.</summary>
  [Debug.KASDebugAdjustable("Dump render link contstrains")]
  public void DbgEventDumpLinkSettings() {
    HostedDebugLog.Warning(this,
        "Procedural model: minLinkLength={0}, maxLinkLength={1}, attachNodePosition.Y={2},"
        + " pistonLength={3}, outerPistonDiameter={4}",
        minLinkLength, maxLinkLength,
        Hierarchy.FindTransformInChildren(srcStrutJoint, PivotAxleTransformName).position.y,
        pistonLength, outerPistonDiameter);
  }

  /// <inheritdoc/>
  public void OnBeforeDebugAdjustablesUpdate() {
  }

  /// <inheritdoc/>
  public void OnDebugAdjustablesUpdated() {
    CreatePipeMeshes(recreate: true);
  }
  #endregion

  #region AbstractProceduralModel overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    UpdateContextMenu();  // For the editor mode.
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    UpdateContextMenu();  // For the flight mode.
  }

  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);

    InjectOrientationMenuItems();
    UpdateContextMenu();
    UpdateLinkLengthAndOrientation();
  }

  /// <inheritdoc/>
  public override void OnUpdate() {
    base.OnUpdate();
    if (isStarted) {
      UpdateLink();
    }
  }

  /// <inheritdoc/>
  protected override void LoadPartModel() {
    CreatePipeMeshes(recreate: false);

    // Source pivot.
    srcPartJoint = Hierarchy.FindTransformByPath(
        partModelTransform, AttachNodeObjName + "/" + SrcPartJointObjName);
    srcPartJointPivot = Hierarchy.FindTransformInChildren(srcPartJoint, PivotAxleTransformName);

    // Source strut joint.
    srcStrutJoint = Hierarchy.FindTransformInChildren(srcPartJointPivot, SrcStrutJointObjName);
    var srcStrutPivot = Hierarchy.FindTransformInChildren(srcStrutJoint, PivotAxleTransformName);
    srcJointHandleLength = Vector3.Distance(srcStrutJoint.position, srcStrutPivot.position);

    // Target strut joint.
    tgtStrutJoint = Hierarchy.FindTransformInChildren(srcPartJointPivot, TgtStrutJointObjName);
    tgtStrutJointPivot = Hierarchy.FindTransformInChildren(tgtStrutJoint, PivotAxleTransformName);
    tgtJointHandleLength = Vector3.Distance(tgtStrutJoint.position, tgtStrutJointPivot.position);

    // Pistons.
    pistons = new GameObject[pistonsCount];
    for (var i = 0; i < pistonsCount; ++i) {
      pistons[i] = Hierarchy.FindTransformInChildren(partModelTransform, "piston" + i).gameObject;
    }

    UpdateValuesFromModel();
    UpdateLinkLengthAndOrientation();
  }

  /// <inheritdoc/>
  public override void LocalizeModule() {
    base.LocalizeModule();
    for (var i = 0; i < parkedOrientations.Count && i < injectedOrientationEvents.Count; i++) {
      injectedOrientationEvents[i].guiName = parkedOrientations[i].title;
    }
  }
  #endregion

  #region ILinkRenderer implemetation
  /// <inheritdoc/>
  public void StartRenderer(Transform source, Transform target) {
    sourceTransform = source;
    targetTransform = target;
    UpdateContextMenu();
  }

  /// <inheritdoc/>
  public void StopRenderer() {
    sourceTransform = null;
    targetTransform = null;
    UpdateContextMenu();
  }

  /// <inheritdoc/>
  public void UpdateLink() {
    UpdateLinkLengthAndOrientation();
  }

  /// <inheritdoc/>
  public string[] CheckColliderHits(Transform source, Transform target) {
    var hitMessages = new HashSet<string>();  // Same object can be hit multiple times.
    var sourcePos = GetLinkVectorSourcePos(source);
    var linkVector = GetLinkVectorTargetPos(target) - sourcePos;
    var hits = Physics.SphereCastAll(
        sourcePos, outerPistonDiameter / 2, linkVector, GetClampedLinkLength(linkVector),
        (int)(KspLayerMask.Part | KspLayerMask.SurfaceCollider | KspLayerMask.Kerbal),
        QueryTriggerInteraction.Ignore);
    foreach (var hit in hits) {
      if (hit.transform.root != source.root && hit.transform.root != target.root) {
        var hitPart = hit.transform.root.GetComponent<Part>();
        hitMessages.Add(hitPart != null
            ? LinkCollidesWithObjectMsg.Format(hitPart)
            : LinkCollidesWithSurfaceMsg.Format());
      }
    }
    return hitMessages.ToArray();
  }

  /// <inheritdoc/>
  public Transform GetMeshByName(string meshName) {
    // TODO(ihsoft): Return latches and pipes, and attach rigid bodies to them.
    throw new NotImplementedException();
  }
  #endregion

  #region IHasContextMenu implemenation
  /// <inheritdoc/>
  public void UpdateContextMenu() {
    injectedOrientationEvents.ForEach(e => e.active = !isLinked);
    PartModuleUtils.SetupEvent(this, ExtendAtMaxMenuAction, x => x.active = !isLinked);
    PartModuleUtils.SetupEvent(this, RetractToMinMenuAction, x => x.active = !isLinked);
  }
  #endregion

  #region Local utility methods
  /// <summary>Adjusts link models to the changed target position.</summary>
  void UpdateLinkLengthAndOrientation() {
    if (!isStarted) {
      // Simply align everyting along Z axis, and rotate source pivot according to the settings.
      srcPartJoint.localRotation = Quaternion.identity;
      srcPartJointPivot.localRotation = Quaternion.LookRotation(persistedParkedOrientation);
      tgtStrutJoint.localPosition =
          GetUnscaledStrutVector(new Vector3(0, 0, persistedParkedLength - tgtJointHandleLength));
      tgtStrutJoint.localRotation = Quaternion.identity;
      tgtStrutJointPivot.localRotation = Quaternion.identity;
    } else {
      var linkVector =
          GetLinkVectorTargetPos(targetTransform) - GetLinkVectorSourcePos(sourceTransform);
      // Here is the link model hierarchy:
      // srcPartJoint => srcPivot => srcStrutJoint => tgtStrutJoint => tgtPivot => tgtPartJoint.
      // Joints attached via a pivot should be properly aligned against each other since they are
      // connected with a common pivot axle which is parallel to their X axis.
      // 1. Rotate srcPartJoint around Z axis so what its pivot axle (X) is perpendicular to
      //    the link vector.
      srcPartJoint.rotation = Quaternion.LookRotation(srcPartJoint.forward, -linkVector);
      // 2. Rotate srcPivot around X axis (pivot axle) so what its forward vector points to the
      //    target part attach node.
      srcPartJointPivot.localRotation =
          Quaternion.Euler(Vector3.Angle(linkVector, srcPartJoint.forward), 0, 0);
      // 3. Shift tgtStrutJoint along Z axis so what it touches the vector end position with the
      //    tgtPivot pivot axle.
      tgtStrutJoint.localPosition = GetUnscaledStrutVector(
          new Vector3(0, 0, GetClampedLinkLength(linkVector) - tgtJointHandleLength));
      // 4. Rotate tgtStrutJoint around Z axis so what its pivot axle (X) is perpendicular to
      //    the target part attach node.
      tgtStrutJoint.rotation =
          Quaternion.LookRotation(tgtStrutJoint.forward, targetTransform.forward);
      // 5. Rotate tgtPivot around X axis (pivot axle) so that its forward vector points along
      //    target attach node direction.
      tgtStrutJointPivot.localRotation =
          Quaternion.Euler(Vector3.Angle(tgtStrutJoint.forward, -targetTransform.forward), 0, 0);
    }

    // Distribute pistons between the first and the last while keepin the direction.
    if (pistons.Length > 2) {
      var offset = pistons[0].transform.localPosition.z;
      var scalablePistons = pistons.Length - 1;
      var step = GetUnscaledStrutVector(
          pistons.Last().transform.position - pistons[0].transform.position).magnitude
          / scalablePistons;
      for (var i = 1; i < scalablePistons; ++i) {
        offset -= step;  // Pistons are distributed to -Z direction of the pviot.
        pistons[i].transform.localPosition = new Vector3(0, 0, offset);
      }
    }
  }

  /// <summary>Creates the context menu events from the orientation descriptions.</summary>
  /// <seealso cref="parkedOrientations"/>
  void InjectOrientationMenuItems() {
    foreach (var orientation in parkedOrientations) {
      var eventInject = new BaseEvent(
          Events,
          "autoEventOrientation" + part.Modules.IndexOf(this),
          () => {
            persistedParkedOrientation = orientation.direction;
            UpdateLinkLengthAndOrientation();
          },
          new KSPEvent());
      eventInject.guiName = orientation.title;
      eventInject.guiActive = false;
      eventInject.guiActiveEditor = true;
      eventInject.guiActiveUnfocused = true;
      PartModuleUtils.AddEvent(this, eventInject);
      injectedOrientationEvents.Add(eventInject);
    }
  }

  /// <summary>Returns link length. Adjusts it to min/max length.</summary>
  /// <param name="linkVector">Link vector.</param>
  /// <returns>Clamped link length</returns>
  /// <seealso cref="minLinkLength"/>
  /// <seealso cref="maxLinkLength"/>
  float GetClampedLinkLength(Vector3 linkVector) {
    var linkLength = linkVector.magnitude;
    if (linkLength < minLinkLength) {
      return minLinkLength;
    }
    if (linkLength > maxLinkLength) {
      return maxLinkLength;
    }
    return linkLength;
  }

  /// <summary>
  /// Transforms a vector, given in the local coordinates, in the scale of the part' model. 
  /// </summary>
  /// <param name="unscaledLength">The vector in the local coordinates.</param>
  /// <returns>The vector in the scale of the part's model.</returns>
  Vector3 GetScaledStrutVector(Vector3 unscaledLength) {
    return unscaledLength * strutScale;
  }

  /// <summary>
  /// Transforms a vector, given in the world coordinates, in the scale of the part's model. 
  /// </summary>
  /// <param name="scaledLength">The vector in the world coordinates.</param>
  /// <returns>The vector in the local part's model coordinates.</returns>
  Vector3 GetUnscaledStrutVector(Vector3 scaledLength) {
    return scaledLength / strutScale;
  }

  /// <summary>Calculates and populates min/max link lengths from the model.</summary>
  /// <remarks>
  /// The length limits must be calculated between the actual points of the joint connection.
  /// </remarks>
  void UpdateValuesFromModel() {
    var pistonSize = GetScaledStrutVector(
        Vector3.Scale(pistonPrefab.GetComponent<Renderer>().bounds.size, pistonModelScale));
    pistonLength = pistonSize.y;
    outerPistonDiameter = Mathf.Max(pistonSize.x, pistonSize.z);
    minLinkLength =
        srcJointHandleLength
        + pistonLength + (pistonsCount - 1) * pistonMinShift * strutScale
        + tgtJointHandleLength;
    maxLinkLength =
        srcJointHandleLength      
        + pistonsCount * (pistonLength - pistonMinShift * strutScale)
        + tgtJointHandleLength;
  }

  /// <summary>
  /// Creates a new model from the existing one. Resets all local settinsg to default. 
  /// </summary>
  /// <remarks>
  /// Same model in this part is copied several times, and they are organized into a hierarchy. So
  /// if there were any scale or rotation adjustments they will accumulate thru the hirerachy
  /// breaking the whole model. That's why all local transformations must be default.
  /// </remarks>
  /// <param name="model">Model to copy.</param>
  /// <param name="objName">name of the new model.</param>
  /// <returns>Cloned model with local transformations set to default.</returns>
  GameObject CloneModel(GameObject model, string objName) {
    var obj = Instantiate(model);
    obj.name = objName;
    obj.transform.localPosition = Vector3.zero;
    obj.transform.localScale = Vector3.one;
    obj.transform.localRotation = Quaternion.identity;
    return obj;
  }

  /// <summary>Creates complete joint model from a prefab.</summary>
  GameObject MakeJointModel(GameObject jointPrefab) {
    // FIXME support scale
    var jointLever = jointPrefab.transform.Find(JointModelName).gameObject;
    var jointModel = Instantiate(jointLever);
    jointModel.name = JointModelName;

    var jointModelPivot = jointPrefab.transform.Find(PivotAxleTransformName);
    jointModelPivot = Instantiate(jointModelPivot);
    jointModelPivot.parent = jointModel.transform;
    jointModelPivot.name = PivotAxleTransformName;

    return jointModel;
  }

  /// <summary>Creates joint levers from a prefab in the main part model.</summary>
  /// <remarks>Prefab is deleted once all levers are created.</remarks>
  void CreateLeverModels() {
    var srcJointModel = MakeJointModel(GameDatabase.Instance.GetModelPrefab(sourceJointModel));

    // Source part joint model.
    srcPartJoint = CloneModel(srcJointModel, SrcPartJointObjName).transform;
    Hierarchy.MoveToParent(srcPartJoint, plugNodeTransform);
    srcPartJointPivot = Hierarchy.FindTransformInChildren(srcPartJoint, PivotAxleTransformName);

    // Source strut joint model.
    srcStrutJoint = CloneModel(srcJointModel, SrcStrutJointObjName).transform;
    var srcStrutPivot = Hierarchy.FindTransformInChildren(srcStrutJoint, PivotAxleTransformName);
    srcJointHandleLength = Vector3.Distance(srcStrutJoint.position, srcStrutPivot.position);
    Hierarchy.MoveToParent(srcStrutJoint, srcPartJointPivot,
                           newPosition: new Vector3(0, 0, srcJointHandleLength),
                           newRotation: Quaternion.LookRotation(Vector3.back));

    var tgtJointModel = MakeJointModel(GameDatabase.Instance.GetModelPrefab(targetJointModel));

    // Target strut joint model.
    tgtStrutJoint = CloneModel(tgtJointModel, TgtStrutJointObjName).transform;
    tgtStrutJointPivot = Hierarchy.FindTransformInChildren(tgtStrutJoint, PivotAxleTransformName);
    tgtJointHandleLength = Vector3.Distance(tgtStrutJoint.position, tgtStrutJointPivot.position);
    Hierarchy.MoveToParent(tgtStrutJoint, srcPartJointPivot);

    // Target part joint model.
    var tgtPartJoint = CloneModel(tgtJointModel, TgtPartJointObjName).transform;
    var tgtPartJointPivot = Hierarchy.FindTransformInChildren(tgtPartJoint, PivotAxleTransformName);
    Hierarchy.MoveToParent(tgtPartJoint, tgtStrutJointPivot,
                           newPosition: new Vector3(0, 0, tgtJointHandleLength),
                           newRotation: Quaternion.LookRotation(Vector3.back));

    // Joint template models are not needed anymore.
    Destroy(srcJointModel);
    Destroy(tgtJointModel);
  }

  /// <summary>Creates piston models from a prefab in a separate model file.</summary>
  void CreatePistonModels() {
    UpdateValuesFromModel();
    pistons = new GameObject[pistonsCount];
    var pistonDiameterScale = 1f;
    var random = new System.Random(0xbeef);  // Just some seed value to make values consistent.
    var randomRotation = Quaternion.identity;
    for (var i = 0; i < pistonsCount; ++i) {
      var piston = UnityEngine.Object.Instantiate(pistonPrefab, srcStrutJoint) as GameObject;
      piston.name = "piston" + i;
      if (pistonModelRandomRotation) {
        // Add a bit of randomness to the pipe model textures. Keep rotation diff above 30 degrees.
        randomRotation = Quaternion.Euler(
            0, 0, randomRotation.eulerAngles.z + 30f + (float) random.NextDouble() * 330f);
      }
      // Source strut joint rotation is reversed. All pistons but the last one are relative to the
      // source joint.
      piston.transform.localRotation = randomRotation * Quaternion.LookRotation(Vector3.back);
      piston.transform.localScale = new Vector3(
          pistonModelScale.x * pistonDiameterScale,
          pistonModelScale.z * pistonDiameterScale,  // Model's Z is game's Y.
          pistonModelScale.y);
      pistonDiameterScale -= pistonDiameterScaleDelta;
      pistons[i] = piston;
    }
    // First piston rigidly attached at the bottom of the source joint model.
    pistons[0].transform.localPosition =
        GetUnscaledStrutVector(new Vector3(0, 0, -pistonLength / 2));
    // Last piston rigidly attached at the bottom of the target joint model.
    randomRotation = Quaternion.Euler(0, 0, pistons.Last().transform.localRotation.eulerAngles.z);
    Hierarchy.MoveToParent(pistons.Last().transform, tgtStrutJoint,
                           newPosition: GetUnscaledStrutVector(new Vector3(0, 0, -pistonLength / 2)),
                           newRotation: randomRotation * Quaternion.LookRotation(Vector3.forward));
  }

  /// <summary>Returns the world position of the source link "pivot".</summary>
  /// <param name="refTransform">The transfrom to count the position relative to.</param>
  /// <returns>The position in world coordinates.</returns>
  Vector3 GetLinkVectorSourcePos(Transform refTransform) {
    // Don't use the stock translation methods since the handle length is already scaled. We don't
    // want the scale to be counted twice.
    return refTransform.position + refTransform.rotation * new Vector3(0, 0, srcJointHandleLength);
  }

  /// <summary>Returns the world position of the target link "pivot".</summary>
  /// <param name="refTransform">The transfrom to count the position relative to.</param>
  /// <returns>The position in world coordinates.</returns>
  Vector3 GetLinkVectorTargetPos(Transform refTransform) {
    // Don't use the stock translation methods since the handle length is already scaled. We don't
    // want the scale to be counted twice.
    return refTransform.position + refTransform.rotation * new Vector3(0, 0, tgtJointHandleLength);
  }

  /// <summary>Creates the telescopic pipe meshes.</summary>
  /// <remarks>
  /// If there were meshes created alreadym they will be destroyed. So this method can be called to
  /// refresh the part settings.
  /// </remarks>
  void CreatePipeMeshes(bool recreate) {
    var pipeRoot = Hierarchy.FindTransformByPath(
        partModelTransform, AttachNodeObjName + "/" + SrcPartJointObjName);
    if (pipeRoot != null && !recreate) {
      return;
    }
    if (pipeRoot != null) {
      HostedDebugLog.Warning(this, "Re-creating pipe meshes...");
      UnityEngine.Object.DestroyImmediate(pipeRoot.gameObject);
    }
    
    CreateLeverModels();
    CreatePistonModels();
    UpdateValuesFromModel();

    // Init parked state. It must go after all the models are created.
    if (parkedOrientations.Count > 0 && !isLinked) {
      persistedParkedOrientation = parkedOrientations[0].direction;
    }
    if (persistedParkedLength < minLinkLength) {
      persistedParkedLength = minLinkLength;
    } else if (persistedParkedLength > maxLinkLength) {
      persistedParkedLength = maxLinkLength;
    }

    UpdateLinkLengthAndOrientation();
  }
  #endregion
}

}  // namespace
