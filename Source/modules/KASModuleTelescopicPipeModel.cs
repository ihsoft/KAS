// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.PartUtils;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>
/// Module that keeps all pieces of the link in the model. I.e. it's a material representation of
/// the part that can link to another part.
/// </summary>
public class KASModuleTelescopicPipeModel : AbstractProceduralModel,
    // KAS interfaces.
    ILinkRenderer,
    // KSPDev interfaces.
    IHasContextMenu {

  #region Localizable GUI strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  protected static readonly Message<string> LinkCollidesWithObjectMsg = new Message<string>(
      "#kasLOC_04000",
      defaultTemplate: "Link collides with: <<1>>",
      description: "Message to display when the link cannot be created due to an obstacle."
      + "\nArgument <<1>> is a title of the part that would collide with the proposed link.",
      example: "Link collides with: Mk2 Cockpit");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message LinkCollidesWithSurfaceMsg = new Message(
      "#kasLOC_04001",
      defaultTemplate: "Link collides with the surface",
      description: "Message to display when the link strut orientation cannot be changed due to it"
      + " would hit the surface.");
  #endregion

  #region Persistent fields
  /// <summary>Orientation of the unlinked strut.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public Vector3 parkedOrientation = Vector3.zero;

  /// <summary>Extended length of the unlinked strut.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public float parkedLength = 0;  // If 0 then minimum link length will be used.
  #endregion

  #region Part's config fields
  /// <summary>
  /// Model for a joint lever at the soucre part. Two such models are used to form a complete joint.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sourceJointModel = "KAS-1.0/Models/Joint/model";

  /// <summary>
  /// Model for a joint lever at the target part. Two such models are used to form a complete joint.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string targetJointModel = "KAS-1.0/Models/Joint/model";

  /// <summary>Number of pistons in the link.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public int pistonsCount = 3;

  /// <summary>Model for the pistons.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string pistonModel = "KAS-1.0/Models/Piston/model";

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
  public float pistonDiameterScaleDelta = 0.1f;

  /// <summary>Minimum allowed overlap of the pistons in the extended state.</summary>
  /// <remarks>
  /// Used to determine minimum and maximum length of the link in terms of visual representation.
  /// Note, that renderer doesn't deal with joint limits. Length limits are only applied to the
  /// meshes used for the link representation.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float pistonMinShift = 0.02f;

  /// <summary>User friendly name for a menu item to adjust unlinked strut orientation.</summary>
  /// <remarks>
  /// This value is encoded like this: &lt;orientation vector&gt;,&lt;menu item title&gt;. Set it to
  /// empty string to not show the menu item.
  /// </remarks>
  /// <seealso cref="ExtractOrientationVector"/>
  /// <seealso cref="ExtractPositionName"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string parkedOrientationMenu0 = "";

  /// <summary>User friendly name for a menu item to adjust unlinked strut orientation.</summary>
  /// <remarks>
  /// This value is encoded like this: &lt;orientation vector&gt;,&lt;menu item title&gt;. Set it to
  /// empty string to not show the menu item.
  /// </remarks>
  /// <seealso cref="ExtractOrientationVector"/>
  /// <seealso cref="ExtractPositionName"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string parkedOrientationMenu1 = "";

  /// <summary>User friendly name for a menu item to adjust unlinked strut orientation.</summary>
  /// <remarks>
  /// This value is encoded like this: &lt;orientation vector&gt;,&lt;menu item title&gt;. Set it to
  /// empty string to not show the menu item.
  /// </remarks>
  /// <seealso cref="ExtractOrientationVector"/>
  /// <seealso cref="ExtractPositionName"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string parkedOrientationMenu2 = "";

  /// <summary>Name of the renderer for this procedural part.</summary>
  /// <remarks>
  /// This setting is used to let link source know primary renderer for the linked state.
  /// </remarks>
  /// <seealso cref="ILinkSource"/>
  /// <seealso cref="ILinkRenderer.cfgRendererName"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string rendererName = "";
  #endregion

  #region ILinkRenderer properties
  /// <inheritdoc/>
  public string cfgRendererName { get { return rendererName; } }

  /// <inheritdoc/>
  public virtual Color? colorOverride {
    get { return _colorOverride; }
    set {
      _colorOverride = value;
      Meshes.UpdateMaterials(srcPartJoint.gameObject, newColor: _colorOverride ?? materialColor);
    }
  }
  Color? _colorOverride;

  /// <inheritdoc/>
  public virtual string shaderNameOverride {
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
  public virtual float stretchRatio {
    get { return 1.0f; }
    set {
      HostedDebugLog.Warning(
          this, "Stretch ratio of the telescopic link is fixed and cannnot be changed to {0}",
          value);
    }
  }

  /// <inheritdoc/>
  public virtual bool isPhysicalCollider {
    get { return _isPhysicalCollider; }
    set {
      _isPhysicalCollider = value;
      Colliders.UpdateColliders(srcPartJoint.gameObject, isEnabled: value);
    }
  }
  bool _isPhysicalCollider;

  /// <inheritdoc/>
  public bool isStarted {
    get { return targetTransform != null; }
  }

  /// <inheritdoc/>
  public Transform sourceTransform {
    get { return srcPartJointPivot; }
  }

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
  protected const string AttachNodeObjName = "plugNode";

  /// <summary>Name of model that connects pipe with the source part.</summary>
  protected const string SrcPartJointObjName = "srcPartJoint";

  /// <summary>Name of model at the pipe start.</summary>
  protected const string SrcStrutJointObjName = "srcStrutJoint";

  /// <summary>Name of model at the pipe end.</summary>
  protected const string TrgStrutJointObjName = "trgStrutJoint";

  /// <summary>Name of model that connects pipe with the target part.</summary>
  protected const string TrgPartJointObjName = "trgPartJoint";

  /// <summary>
  /// Name of the joint model in the part's model. It's used as a template to create all the joint
  /// levers.
  /// </summary>
  protected const string JointModelName = "Joint";

  /// <summary>
  /// Name of the transform that is used to connect two levers to form a complete joint. 
  /// </summary>
  protected const string PivotAxleTransformName = "PivotAxle";

  /// <summary>Name of the piston object in the piston's model.</summary>
  protected const string PistonModelName = "Piston";
  #endregion

  #region Model transforms & properties
  /// <summary>Model that represents a joint at the source part.</summary>
  /// <value>An object in the part's model. It's never <c>null</c>.</value>
  protected Transform srcPartJoint { get; private set; }

  /// <summary>Pivot axis object on the source joint.</summary>
  /// <value>An object in the part's model. It's never <c>null</c>.</value>
  protected Transform srcPartJointPivot { get; private set; }

  /// <summary>Model at the pipe's start that connects to the source joint pivot.</summary>
  /// <value>An object in the part's model. It's never <c>null</c>.</value>
  /// <remarks>It's orientation is reversed. I.e .it "looks" at the source joint pivot.</remarks>
  protected Transform srcStrutJoint { get; private set; }

  /// <summary>Model at the pipe's end that connects to the target joint pivot.</summary>
  /// <value>An object in the part's model. It's never <c>null</c>.</value>
  protected Transform trgStrutJoint { get; private set; }

  /// <summary>Pivot axis object at the pipe's end.</summary>
  /// <value>An object in the part's model. It's never <c>null</c>.</value>
  protected Transform trgStrutJointPivot { get; private set; }

  /// <summary>Pistons that form the strut pipe.</summary>
  /// <value>A list of meshes.</value>
  protected GameObject[] pistons { get; private set; }

  /// <summary>
  /// Distance of the source part joint pivot from it's base. It's calculated from the model.
  /// </summary>
  /// <value>The distance in meters.</value>
  protected float srcJointHandleLength { get; private set; }

  /// <summary>
  /// Distance of the target part joint pivot from it's base. It's calculated from the model.
  /// </summary>
  /// <value>The distance in meters.</value>
  protected float trgJointHandleLength { get; private set; }

  /// <summary>
  /// The minimum length to which the telescopic pipe can shrink. It's calculated from the model.
  /// </summary>
  /// <value>The distance in meters.</value>
  protected float minLinkLength { get; private set; }

  /// <summary>
  /// The maximum length to which the telescopic pipe can expand. It's calculated from the model.
  /// </summary>
  /// <value>The distance in meters.</value>
  protected float maxLinkLength { get; private set; }

  /// <summary>Diameter of the outer piston. It's calculated from the model.</summary>
  /// <value>The diameter in metters.</value>
  /// <remarks>It's primarily used to cast a collider.</remarks>
  /// <seealso cref="CheckColliderHits"/>
  protected float outerPistonDiameter { get; private set; }

  /// <summary>Length of a single piston. It's calculated from the model.</summary>
  /// <value>The distance in meters.</value>
  protected float pistonLength { get; private set; }

  /// <summary>Prefab for the piston models.</summary>
  /// <value>A model reference from the part's model. It's not a copy!</value>
  protected GameObject pistonPrefab {
    get {
      return GameDatabase.Instance.GetModelPrefab(pistonModel).transform
          .FindChild(PistonModelName).gameObject;
    }
  }

  /// <summary>Tells if the source on the part is linked.</summary>
  /// <value>The current state of the link.</value>
  protected bool isLinked {
    get { return targetTransform != null; }
  }
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    UpdateContextMenu();  // For editor mode.
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    UpdateContextMenu();  // For flight mode.
  }

  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
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
  #endregion

  #region ILinkRenderer implemetation
  /// <inheritdoc/>
  public virtual void StartRenderer(Transform source, Transform target) {
    // Source pivot is fixed for this part. Do a safe check to verify if requestor asked for the
    // right coordinates.
    if (Vector3.SqrMagnitude(source.position - sourceTransform.position) > 0.0005f) {
      HostedDebugLog.Error(
          this, "Part's source doesn't match the renderer's source: pivot={0}, source={1}, err={2}",
          sourceTransform.position,
          source.position,
          Vector3.SqrMagnitude(source.position - sourceTransform.position));
    }
    targetTransform = target;
    UpdateContextMenu();
  }

  /// <inheritdoc/>
  public virtual void StopRenderer() {
    targetTransform = null;
    UpdateContextMenu();
  }

  /// <inheritdoc/>
  public virtual void UpdateLink() {
    UpdateLinkLengthAndOrientation();
  }

  /// <inheritdoc/>
  public virtual string CheckColliderHits(Transform source, Transform target) {
    var linkVector = target.position - source.position;
    var hits = Physics.SphereCastAll(
        source.position, outerPistonDiameter / 2, linkVector, GetClampedLinkLength(linkVector),
        (int)(KspLayerMask.Part | KspLayerMask.SurfaceCollider | KspLayerMask.Kerbal),
        QueryTriggerInteraction.Ignore);
    foreach (var hit in hits) {
      if (hit.transform.root != source.root && hit.transform.root != target.root) {
        var hitPart = hit.transform.root.GetComponent<Part>();
        // Use partInfo.title to properly display kerbal names.
        return hitPart != null
            ? LinkCollidesWithObjectMsg.Format(hitPart.partInfo.title)
            : LinkCollidesWithSurfaceMsg.Format();
      }
    }
    return null;
  }
  #endregion

  #region IHasContextMenu implemenation
  /// <inheritdoc/>
  public virtual void UpdateContextMenu() {
    PartModuleUtils.SetupEvent(this, ParkedOrientationMenuAction0, x => {
      x.guiName = ExtractPositionName(parkedOrientationMenu0);
      x.active = x.guiName != "" && !isLinked;
    });
    PartModuleUtils.SetupEvent(this, ParkedOrientationMenuAction1, x => {
      x.guiName = ExtractPositionName(parkedOrientationMenu1);
      x.active = x.guiName != "" && !isLinked;
    });
    PartModuleUtils.SetupEvent(this, ParkedOrientationMenuAction2, x => {
      x.guiName = ExtractPositionName(parkedOrientationMenu2);
      x.active = x.guiName != "" && !isLinked;
    });
    PartModuleUtils.SetupEvent(this, ExtendAtMaxMenuAction, x => x.active = !isLinked);
    PartModuleUtils.SetupEvent(this, RetractToMinMenuAction, x => x.active = !isLinked);
  }
  #endregion

  // FIXME: check colliders.
  #region GUI menu action handlers
  /// <summary>Event handler. Changes orientation of the unlinked strut.</summary>
  /// <seealso cref="parkedOrientationMenu0"/>
  [KSPEvent(guiName = "Pipe position 0", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = true, active = false)]
  public void ParkedOrientationMenuAction0() {
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu0);
    UpdateLinkLengthAndOrientation();
  }

  /// <summary>Event handler. Changes orientation of the unlinked strut.</summary>
  /// <seealso cref="parkedOrientationMenu1"/>
  [KSPEvent(guiName = "Pipe position 1", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = true, active = false)]
  public void ParkedOrientationMenuAction1() {
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu1);
    UpdateLinkLengthAndOrientation();
  }

  /// <summary>Event handler. Changes orientation of the unlinked strut.</summary>
  /// <seealso cref="parkedOrientationMenu2"/>
  [KSPEvent(guiName = "Pipe position 2", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = true, active = false)]
  public void ParkedOrientationMenuAction2() {
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu2);
    UpdateLinkLengthAndOrientation();
  }

  /// <summary>Event handler. Extends unlinked strut at maximum length.</summary>
  /// <seealso cref="maxLinkLength"/>
  [KSPEvent(guiName = "Extend to max", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = true, active = false)]
  public void ExtendAtMaxMenuAction() {
    parkedLength = maxLinkLength;
    UpdateLinkLengthAndOrientation();
  }

  /// <summary>Event handler. Retracts unlinked strut to the minimum length.</summary>
  /// <seealso cref="minLinkLength"/>
  [KSPEvent(guiName = "Retract to min", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = true, active = false)]
  public void RetractToMinMenuAction() {
    parkedLength = minLinkLength;
    UpdateLinkLengthAndOrientation();
  }
  #endregion

  #region AbstractProceduralModel implementation
  /// <inheritdoc/>
  protected override void CreatePartModel() {
    CreateLeverModels();
    CreatePistonModels();
    UpdateValuesFromModel();
    // Log basic part values to help part's designers.
    HostedDebugLog.Info(this,
        "Procedural model: minLinkLength={0}, maxLinkLength={1}, attachNodePosition.Y={2},"
        + " pistonLength={3}, outerPistonDiameter={4}",
        minLinkLength, maxLinkLength,
        Hierarchy.FindTransformInChildren(srcStrutJoint, PivotAxleTransformName).position.y,
        pistonLength, outerPistonDiameter);

    // Init parked state. It must go after all the models are created.
    parkedOrientation = parkedOrientationMenu0 != ""
        ? ExtractOrientationVector(parkedOrientationMenu0)
        : Vector3.forward;
    if (Mathf.Approximately(parkedLength, 0)) {
      parkedLength = minLinkLength;
    }

    UpdateLinkLengthAndOrientation();
  }

  /// <inheritdoc/>
  protected override void LoadPartModel() {
    // Source pivot.
    srcPartJoint = Hierarchy.FindTransformByPath(
        partModelTransform, AttachNodeObjName + "/" + SrcPartJointObjName);
    srcPartJointPivot = Hierarchy.FindTransformInChildren(srcPartJoint, PivotAxleTransformName);

    // Source strut joint.
    srcStrutJoint = Hierarchy.FindTransformInChildren(srcPartJointPivot, SrcStrutJointObjName);
    var srcStrutPivot = Hierarchy.FindTransformInChildren(srcStrutJoint, PivotAxleTransformName);
    srcJointHandleLength = Vector3.Distance(srcStrutJoint.position, srcStrutPivot.position);

    // Target strut joint.
    trgStrutJoint = Hierarchy.FindTransformInChildren(srcPartJointPivot, TrgStrutJointObjName);
    trgStrutJointPivot = Hierarchy.FindTransformInChildren(trgStrutJoint, PivotAxleTransformName);
    trgJointHandleLength = Vector3.Distance(trgStrutJoint.position, trgStrutJointPivot.position);

    // Pistons.
    pistons = new GameObject[pistonsCount];
    for (var i = 0; i < pistonsCount; ++i) {
      pistons[i] = Hierarchy.FindTransformInChildren(partModelTransform, "piston" + i).gameObject;
    }

    UpdateValuesFromModel();
    UpdateLinkLengthAndOrientation();
  }
  #endregion

  #region Inheritable methods
  /// <summary>Adjusts link models to the changed target position.</summary>
  protected virtual void UpdateLinkLengthAndOrientation() {
    if (!isStarted) {
      // Simply align everyting along Z axis, and rotate source pivot according to the settings.
      srcPartJoint.localRotation = Quaternion.identity;
      srcPartJointPivot.localRotation = Quaternion.LookRotation(parkedOrientation);
      trgStrutJoint.localPosition = new Vector3(0, 0, parkedLength - trgJointHandleLength);
      trgStrutJoint.localRotation = Quaternion.identity;
      trgStrutJointPivot.localRotation = Quaternion.identity;
    } else {
      var linkVector = targetTransform.position - sourceTransform.position;
      // Here is the link model hierarchy:
      // srcPartJoint => srcPivot => srcStrutJoint => trgStrutJoint => trgPivot => trgPartJoint.
      // Joints attached via a pivot should be properly aligned against each other since they are
      // connected with a common pivot axle which is parallel to their X axis.
      // 1. Rotate srcPartJoint around Z axis so what its pivot axle (X) is perpendicular to
      //    the link vector.
      srcPartJoint.rotation = Quaternion.LookRotation(srcPartJoint.forward, -linkVector);
      // 2. Rotate srcPivot around X axis (pivot axle) so what its forward vector points to the
      //    target part attach node.
      srcPartJointPivot.localRotation =
          Quaternion.Euler(Vector3.Angle(linkVector, srcPartJoint.forward), 0, 0);
      // 3. Shift trgStrutJoint along Z axis so what it touches target joint node with the trgPivot
      //    pivot axle. Link length consists of srcStrutJoint and trgStrutJoint model lengths but
      //    the former points backwards, so it doesn't add to the positive Z value.
      trgStrutJoint.localPosition =
          new Vector3(0, 0, GetClampedLinkLength(linkVector) - trgJointHandleLength);
      // 4. Rotate trgStrutJoint around Z axis so what its pivot axle (X) is perpendicular to
      //    the target part attach node.
      trgStrutJoint.rotation =
          Quaternion.LookRotation(trgStrutJoint.forward, targetTransform.forward);
      // 5. Rotate trgPivot around X axis (pivot axle) so that its forward vector points along
      //    target attach node direction.
      trgStrutJointPivot.localRotation =
        Quaternion.Euler(Vector3.Angle(trgStrutJoint.forward, -targetTransform.forward), 0, 0);
    }

    // Distribute pistons between the first and the last while keepin the direction.
    if (pistons.Length > 2) {
      var offset = pistons[0].transform.localPosition.z;
      var scalablePistons = pistons.Length - 1;
      var step = Vector3.Distance(pistons.Last().transform.position, pistons[0].transform.position)
          / scalablePistons;
      for (var i = 1; i < scalablePistons; ++i) {
        offset -= step;  // Pistons are distributed to -Z direction of the pviot.
        pistons[i].transform.localPosition = new Vector3(0, 0, offset);
      }
    }
  }
  #endregion

  #region Inherotable utility methods 
  /// <summary>Returns link length. Adjusts it to min/max length.</summary>
  /// <param name="linkVector">Link vector.</param>
  /// <returns>Clamped link length</returns>
  /// <seealso cref="minLinkLength"/>
  /// <seealso cref="maxLinkLength"/>
  protected float GetClampedLinkLength(Vector3 linkVector) {
    var linkLength = linkVector.magnitude;
    if (linkLength < minLinkLength) {
      return minLinkLength;
    }
    if (linkLength > maxLinkLength) {
      return maxLinkLength;
    }
    return linkLength;
  }

  /// <summary>Returns a direction vector for the parked string.</summary>
  /// <param name="cfgSetting">String from the config of the following format:
  /// <c>X,Y,Z,&lt;menu text&gt;</c>, where <c>X,Y,Z</c> defines a direction in the node's local
  /// coordinates, and <c>menu text</c> is a string to show in the context menu.</param>
  /// <returns>Direction vector for the action.</returns>
  protected Vector3 ExtractOrientationVector(string cfgSetting) {
    var lastCommaPos = cfgSetting.LastIndexOf(',');
    if (lastCommaPos == -1) {
      HostedDebugLog.Warning(this, "Cannot extract direction from string: {0}", cfgSetting);
      return Vector3.forward;
    }
    return ConfigNode.ParseVector3(cfgSetting.Substring(0, lastCommaPos));
  }

  /// <summary>Returns a context menu item name from the packed string.</summary>
  /// <param name="cfgSetting">String from the config of the following format:
  /// <c>X,Y,Z,&lt;menu text&gt;</c>, where <c>X,Y,Z</c> defines a direction in the node's local
  /// coordinates, and <c>menu text</c> is a string to show in the context menu.</param>
  /// <returns>Display string for the action.</returns>
  protected static string ExtractPositionName(string cfgSetting) {
    var lastCommaPos = cfgSetting.LastIndexOf(',');
    return lastCommaPos != -1
        ? cfgSetting.Substring(lastCommaPos + 1)
        : cfgSetting;
  }
  #endregion

  #region Private utility methods
  /// <summary>Calculates and populates min/max link lengths from the model.</summary>
  void UpdateValuesFromModel() {
    var pistonSize =
        Vector3.Scale(pistonPrefab.GetComponent<Renderer>().bounds.size, pistonModelScale);
    pistonLength = pistonSize.y;
    outerPistonDiameter = Mathf.Max(pistonSize.x, pistonSize.z);
    minLinkLength =
        srcJointHandleLength
        + pistonLength + (pistonsCount - 1) * pistonMinShift
        + trgJointHandleLength;
    maxLinkLength =
        srcJointHandleLength
        + pistonsCount * (pistonLength - pistonMinShift)
        + trgJointHandleLength;
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
    var jointLever = jointPrefab.transform.FindChild(JointModelName).gameObject;
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
    var plugNodeTransform = part.FindModelTransform("plugNode");
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

    var trgJointModel = MakeJointModel(GameDatabase.Instance.GetModelPrefab(targetJointModel));

    // Target strut joint model.
    trgStrutJoint = CloneModel(trgJointModel, TrgStrutJointObjName).transform;
    trgStrutJointPivot = Hierarchy.FindTransformInChildren(trgStrutJoint, PivotAxleTransformName);
    trgJointHandleLength = Vector3.Distance(trgStrutJoint.position, trgStrutJointPivot.position);
    Hierarchy.MoveToParent(trgStrutJoint, srcPartJointPivot);

    // Target part joint model.
    var trgPartJoint = CloneModel(trgJointModel, TrgPartJointObjName).transform;
    var trgPartJointPivot = Hierarchy.FindTransformInChildren(trgPartJoint, PivotAxleTransformName);
    Hierarchy.MoveToParent(trgPartJoint, trgStrutJointPivot,
                           newPosition: new Vector3(0, 0, trgJointHandleLength),
                           newRotation: Quaternion.LookRotation(Vector3.back));

    // Joint template models are not needed anymore.
    Destroy(srcJointModel);
    Destroy(trgJointModel);
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
    pistons[0].transform.localPosition = new Vector3(0, 0, -pistonLength / 2);
    // Last piston rigidly attached at the bottom of the target joint model.
    randomRotation = Quaternion.Euler(0, 0, pistons.Last().transform.localRotation.eulerAngles.z);
    Hierarchy.MoveToParent(pistons.Last().transform, trgStrutJoint,
                           newPosition: new Vector3(0, 0, -pistonLength / 2),
                           newRotation: randomRotation * Quaternion.LookRotation(Vector3.forward));
  }
  #endregion
}

}  // namespace
