// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using System.Linq;
using UnityEngine;
using KASAPIv1;
using KSPDev.ModelUtils;
using KSPDev.GUIUtils;

namespace KAS {

/// <summary>
/// Module that keeps all pieces of the link in the model. I.e. it's a material representation of
/// the part that can link to another part.
/// </summary>
public class KASModuleTelescopicPipeStrut : AbstractProceduralModel, ILinkRenderer {

  #region Localizable GUI strings
  /// <summary>
  /// Message to display when link cannot be created due to an obstacle in the way. 
  /// </summary>
  protected static Message<string> LinkCollidesWithObjectMsg = "Link collides with {0}";
  /// <summary>
  /// Message to display when link strut orientation cannot be changed due to it would hit the
  /// surface.
  /// </summary>
  protected static Message LinkCollidesWithSurfaceMsg = "Link collides with surface";
  #endregion

  #region Persistent fields
  /// <summary>Persistent config field. Orientation of the unlinked strut.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field that is saved/restored with the vessel. It's
  /// handled by the KSP core and must <i>not</i> be altered directly. Moreover, in spite of it's
  /// declared <c>public</c> it must not be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField(isPersistant = true)]
  public Vector3 parkedOrientation = Vector3.zero;
  /// <summary>Persistent config field. Extended length of the unlinked strut.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field that is saved/restored with the vessel. It's
  /// handled by the KSP core and must <i>not</i> be altered directly. Moreover, in spite of it's
  /// declared <c>public</c> it must not be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField(isPersistant = true)]
  public float parkedLength = 0;  // If 0 then minimum link length will be used.
  #endregion

  #region Part's config fields
  /// <summary>Config setting. Number of pistons in the link.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public int pistonsCount = 3;
  /// <summary>Config setting. Diameter of the outer piston.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public float outerPistonDiameter = 0.15f;
  /// <summary>Config setting. Length of a single piston.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public float pistonLength = 0.2f;
  /// <summary>
  /// Config setting. Thickness of pisont's wall. Diameter of a nested piston is less than parent's
  /// diameter by 2x of this value.  
  /// </summary>
  /// <remarks>
  /// E.g. if parent's diameter was <c>0.4m</c> and wall thickness is <c>0.01m</c> then nested
  /// piston's diameter will be: <c>0.4 - 2*0.01 = 0.4 - 0.02 = 0.38m</c>.
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public float pistonWallThickness = 0.01f;
  /// <summary>Config setting. Texture to cover pistons with.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public string pistonTexturePath = "";
  /// <summary>
  /// Config setting. Minimum allowed overlap of the pistons in the extended state.
  /// </summary>
  /// <remarks>
  /// Used to determine minimum and maximum length of the link in terms of visual representation.
  /// Note, that renderer doesn't deal with joint limits. Length limits are only applied to the
  /// meshes used for the link representation.
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public float pistonMinShift = 0.02f;
  /// <summary>
  /// Config setting. User friendly name for a menu item to adjust unlinked strut orientation.
  /// </summary>
  /// <remarks>
  /// This value is encoded like this: &lt;orientation vector&gt;,&lt;menu item title&gt;
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  /// <seealso cref="ExtractOrientationVector"/>
  /// <seealso cref="ExtractPositionName"/>
  [KSPField]
  public string parkedOrientationMenu0 = "";
  /// <summary>
  /// Config setting. User friendly name for a menu item to adjust unlinked strut orientation.
  /// </summary>
  /// <remarks>
  /// This value is encoded like this: &lt;orientation vector&gt;,&lt;menu item title&gt;
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  /// <seealso cref="ExtractOrientationVector"/>
  /// <seealso cref="ExtractPositionName"/>
  [KSPField]
  public string parkedOrientationMenu1 = "";
  /// <summary>
  /// Config setting. User friendly name for a menu item to adjust unlinked strut orientation.
  /// </summary>
  /// <remarks>
  /// This value is encoded like this: &lt;orientation vector&gt;,&lt;menu item title&gt;
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  /// <seealso cref="ExtractOrientationVector"/>
  /// <seealso cref="ExtractPositionName"/>
  [KSPField]
  public string parkedOrientationMenu2 = "";
  /// <summary>Config setting. Name of the renderer for this procedural part.</summary>
  /// <remarks>
  /// This setting is used to let link source know primary renderer for the linked state.
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  /// <seealso cref="ILinkSource"/>
  /// <seealso cref="ILinkRenderer.cfgRendererName"/>
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
      Meshes.UpdateMaterials(
          srcPartJoint.gameObject, newShaderName: _shaderNameOverride ?? shaderName);
    }
  }
  string _shaderNameOverride;

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

  #region Event names. Keep them in sync with the event names!
  /// <summary>Name of the relevant event. It must match name of the method.</summary>
  /// <seealso cref="ParkedOrientationMenuAction0"/>
  /// <seealso cref="parkedOrientationMenu0"/>
  protected const string MenuAction0Name = "ParkedOrientationMenuAction0";
  /// <summary>Name of the relevant event. It must match name of the method.</summary>
  /// <seealso cref="ParkedOrientationMenuAction1"/>
  /// <seealso cref="parkedOrientationMenu1"/>
  protected const string MenuAction1Name = "ParkedOrientationMenuAction1";
  /// <summary>Name of the relevant event. It must match name of the method.</summary>
  /// <seealso cref="ParkedOrientationMenuAction2"/>
  /// <seealso cref="parkedOrientationMenu2"/>
  protected const string MenuAction2Name = "ParkedOrientationMenuAction2";
  /// <summary>Name of the relevant event. It must match name of the method.</summary>
  protected const string ExtendAtMaxMenuActionName = "ExtendAtMaxMenuAction";
  /// <summary>Name of the relevant event. It must match name of the method.</summary>
  protected const string RetractToMinMenuActionName = "RetractToMinMenuAction";
  #endregion

  #region Model name constants
  /// <summary>A transform that is a root for the whole pipe modelset.</summary>
  /// <remarks>It doesn't have to match part's atatch node transform.</remarks>
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
  protected const string JointObjName = "Joint";
  /// <summary>
  /// Name of the transform that is used to conenct two levers to form a complete joint. 
  /// </summary>
  protected const string PivotAxileObjName = "PivotAxile";
  #endregion

  #region Model transforms & properties
  /// <summary>Model that connects pipe assembly with the source part.</summary> 
  protected Transform srcPartJoint { get; private set; }
  /// <summary>Pivot axis model at the source part.</summary>
  protected Transform srcPartJointPivot { get; private set; }
  /// <summary>Model at the pipe start.</summary>
  /// <remarks>It's orientation is reversed, and it's positioned so what its pivot axis matches
  /// <see cref="srcPartJointPivot"/>. I.e. forward direction in the local space is
  /// <see cref="Vector3.back"/>.</remarks>
  protected Transform srcStrutJoint { get; private set; }
  /// <summary>Model at the pipe end.</summary>
  protected Transform trgStrutJoint { get; private set; }
  /// <summary>Pivot axis model at the pipe end.</summary>
  protected Transform trgStrutJointPivot { get; private set; }
  /// <summary>Distance of source part joint pivot from it's base.</summary>
  protected float srcJointHandleLength { get; private set; }
  /// <summary>Distance of target part joint pivot from it's base.</summary>
  protected float trgJointHandleLength { get; private set; }
  /// <summary>Pistons that form the strut.</summary>
  protected GameObject[] pistons { get; private set; }
  /// <summary>Minmum link length that doesn't break telescopic pipe renderer.</summary>
  protected float minLinkLength { get; private set; }
  /// <summary>Maximum link length that doesn't break telescopic pipe renderer.</summary>
  protected float maxLinkLength { get; private set; }
  #endregion

  #region Inheritable properties 
  /// <summary>Tells if source on the part is linked.</summary>
  protected bool isLinked {
    get { return linkSource != null && linkSource.linkState == LinkState.Linked; }
  }
  /// <summary>Link source module that operates on this part. There can be only one.</summary>
  /// <remarks>It's get populated in the <see cref="OnStart"/> method.</remarks>
  protected ILinkSource linkSource { get; private set; }
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    UpdateMenuItems();  // For editor mode.
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    UpdateMenuItems();  // For flight mode.
  }

  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
    linkSource = part.FindModulesImplementing<ILinkSource>()
        .FirstOrDefault(x => x.cfgLinkRendererName == rendererName);
    UpdateMenuItems();
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
      Debug.LogErrorFormat(
          "Part's source doesn't match renderer source: pivot={0}, source={1}, err={2}",
          sourceTransform.position,
          source.position,
          Vector3.SqrMagnitude(source.position - sourceTransform.position));
    }
    targetTransform = target;
    UpdateMenuItems();
  }

  /// <inheritdoc/>
  public virtual void StopRenderer() {
    targetTransform = null;
    UpdateMenuItems();
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
        (int)(KspLayerMask.PARTS | KspLayerMask.SURFACE | KspLayerMask.KERBALS),
        QueryTriggerInteraction.Ignore);
    foreach (var hit in hits) {
      if (hit.transform.root != source.root && hit.transform.root != target.root) {
        var hitPart = hit.transform.root.GetComponent<Part>();
        // Use partInfo.title to properly display kerbal names.
        return hitPart != null
            ? LinkCollidesWithObjectMsg.Format(hitPart.partInfo.title)
            : LinkCollidesWithSurfaceMsg.ToString();
      }
    }
    return null;
  }
  #endregion

  // FIXME: check colliders.
  #region GUI menu action handlers
  /// <summary>Event handler. Changes orientation of the unlinked strut.</summary>
  /// <seealso cref="MenuAction0Name"/>
  /// <seealso cref="parkedOrientationMenu0"/>
  [KSPEvent(guiName = "Pipe position 0", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = true, active = false)]
  public void ParkedOrientationMenuAction0() {
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu0);
    UpdateLinkLengthAndOrientation();
  }

  /// <summary>Event handler. Changes orientation of the unlinked strut.</summary>
  /// <seealso cref="MenuAction1Name"/>
  /// <seealso cref="parkedOrientationMenu1"/>
  [KSPEvent(guiName = "Pipe position 1", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = true, active = false)]
  public void ParkedOrientationMenuAction1() {
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu1);
    UpdateLinkLengthAndOrientation();
  }

  /// <summary>Event handler. Changes orientation of the unlinked strut.</summary>
  /// <seealso cref="MenuAction2Name"/>
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

  //FIXME: drop
  void DumpHirerahcy(Transform m) {
    while (m != null) {
      Debug.LogWarningFormat("Transform '{0}' has local scale {1}", m.name, m.localScale);
      m = m.parent;
    }
  }

  #region AbstractProceduralModel implementation
  /// <inheritdoc/>
  protected override void CreatePartModel() {
    var jointModel = part.FindModelTransform(JointObjName).gameObject;
    if (jointModel == null) {
      Debug.LogErrorFormat("No joint model found in {0}!!!!", part.name);
      return;
    }
    
    //DumpHirerahcy(jointModel.transform);
    var jointModelPivot = jointModel.transform.Find(PivotAxileObjName);
    //DumpHirerahcy(jointModelPivot.transform);
    var plugNodeTransform = part.FindModelTransform("plugNode");
    //DumpHirerahcy(plugNodeTransform);
    
    jointModel.transform.parent = null;

    // Source part joint model.
    srcPartJoint = CloneModel(jointModel.gameObject, SrcPartJointObjName).transform;
    Hierarchy.MoveToParent(srcPartJoint, plugNodeTransform);
    srcPartJointPivot = Hierarchy.FindTransformInChildren(srcPartJoint, PivotAxileObjName);

    // Source strut joint model.
    srcStrutJoint = CloneModel(jointModel.gameObject, SrcStrutJointObjName).transform;
    var srcStrutPivot = Hierarchy.FindTransformInChildren(srcStrutJoint, PivotAxileObjName);
    srcJointHandleLength = Vector3.Distance(srcStrutJoint.position, srcStrutPivot.position);
    Hierarchy.MoveToParent(srcStrutJoint, srcPartJointPivot,
                           newPosition: new Vector3(0, 0, srcJointHandleLength),
                           newRotation: Quaternion.LookRotation(Vector3.back));

    // Target strut joint model.
    trgStrutJoint = CloneModel(jointModel.gameObject, TrgStrutJointObjName).transform;
    trgStrutJointPivot = Hierarchy.FindTransformInChildren(trgStrutJoint, PivotAxileObjName);
    trgJointHandleLength = Vector3.Distance(trgStrutJoint.position, trgStrutJointPivot.position);
    Hierarchy.MoveToParent(trgStrutJoint, srcPartJointPivot);

    // Target part joint model.
    var trgPartJoint = CloneModel(jointModel.gameObject, TrgStrutJointObjName).transform;
    var trgPartJointPivot = Hierarchy.FindTransformInChildren(trgPartJoint, PivotAxileObjName);
    Hierarchy.MoveToParent(trgPartJoint, trgStrutJointPivot,
                           newPosition: new Vector3(0, 0, trgJointHandleLength),
                           newRotation: Quaternion.LookRotation(Vector3.back));
      
    // Pistons.
    pistons = new GameObject[pistonsCount];
    var startDiameter = outerPistonDiameter;
    var material = CreateMaterial(GetTexture(pistonTexturePath));
    for (var i = 0; i < pistonsCount; ++i) {
      var piston =
          Meshes.CreateCylinder(startDiameter, pistonLength, material, parent: srcStrutJoint);
      piston.name = "piston" + i;
      Colliders.SetSimpleCollider(piston, PrimitiveType.Capsule);
      // Source strut joint rotation is reversed. All pistons but the last one are relative to the
      // source joint.
      piston.transform.localRotation = Quaternion.LookRotation(Vector3.back);
      startDiameter -= 2 * pistonWallThickness;
      pistons[i] = piston;
    }
    // First piston rigidly attached at the bottom of the source joint model.
    pistons[0].transform.localPosition = new Vector3(0, 0, -pistonLength / 2);
    // Last piston rigidly attached at the bottom of the target joint model.
    Hierarchy.MoveToParent(pistons.Last().transform, trgStrutJoint,
                           newPosition: new Vector3(0, 0, -pistonLength / 2),
                           newRotation: Quaternion.LookRotation(Vector3.forward));
    
    CalculateLengthLimits();
    //FIXME: use info level
    Debug.LogWarningFormat(
        "Procedural part {0}: minLinkLength={1}, maxLinkLength={2}, attachNodePosition.Y={3}",
        part.name, minLinkLength, maxLinkLength, srcStrutPivot.position.y);

    // Joint template model is not needed anymore.
    UnityEngine.Object.DestroyImmediate(jointModel.gameObject);

    // Init parked state. It must go after all the models are created.
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu0);
    if (Mathf.Approximately(parkedLength, 0)) {
      // Cannot get length from the joint module since it may not be existing at the moment.
      parkedLength = minLinkLength;
    }

    UpdateLinkLengthAndOrientation();
  }

  /// <inheritdoc/>
  protected override void LoadPartModel() {
    // Source pivot.
    srcPartJoint = Hierarchy.FindTransformByPath(
        partModelTransform, AttachNodeObjName + "/" + SrcPartJointObjName);
    srcPartJointPivot = Hierarchy.FindTransformInChildren(srcPartJoint, PivotAxileObjName);

    // Source strut joint.
    srcStrutJoint = Hierarchy.FindTransformInChildren(srcPartJointPivot, SrcStrutJointObjName);
    var srcStrutPivot = Hierarchy.FindTransformInChildren(srcStrutJoint, PivotAxileObjName);
    srcJointHandleLength = Vector3.Distance(srcStrutJoint.position, srcStrutPivot.position);

    // Target strut joint.
    trgStrutJoint = Hierarchy.FindTransformInChildren(srcPartJointPivot, TrgStrutJointObjName);
    trgStrutJointPivot = Hierarchy.FindTransformInChildren(trgStrutJoint, PivotAxileObjName);
    trgJointHandleLength = Vector3.Distance(trgStrutJoint.position, trgStrutJointPivot.position);

    // Pistons.
    pistons = new GameObject[pistonsCount];
    for (var i = 0; i < pistonsCount; ++i) {
      pistons[i] = Hierarchy.FindTransformInChildren(partModelTransform, "piston" + i).gameObject;
    }

    CalculateLengthLimits();
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
      // Joints attached via a pivot should be properly aligned againts each other since they are
      // connected with a common pivot axile which is parallel to their X axis.
      // 1. Rotate srcPartJoint around Z axis so what its pivot axile (X) is perpendicular to
      //    the link vector.
      srcPartJoint.rotation = Quaternion.LookRotation(srcPartJoint.forward, -linkVector);
      // 2. Rotate srcPivot around X axis (pivot axile) so what its forward vector points to the
      //    target part attach node.
      srcPartJointPivot.localRotation =
          Quaternion.Euler(Vector3.Angle(linkVector, srcPartJoint.forward), 0, 0);
      // 3. Shift trgStrutJoint along Z axis so what it touches target joint node with the trgPivot
      //    pivot axile. Link length consists of srcStrutJoint and trgStrutJoint model lengths but
      //    the former points backwards, so it doesn't add to the positive Z value.
      trgStrutJoint.localPosition =
          new Vector3(0, 0, GetClampedLinkLength(linkVector) - trgJointHandleLength);
      // 4. Rotate trgStrutJoint around Z axis so what its pivot axile (X) is perpendicular to
      //    the target part attach node.
      trgStrutJoint.rotation =
          Quaternion.LookRotation(trgStrutJoint.forward, targetTransform.forward);
      // 5. Rotate trgPivot around X axis (pivot axile) so what its forward vector points along
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

  /// <summary>Updates menu item names and visibility states.</summary>
  protected virtual void UpdateMenuItems() {
    Events[MenuAction0Name].guiName = ExtractPositionName(parkedOrientationMenu0);
    Events[MenuAction1Name].guiName = ExtractPositionName(parkedOrientationMenu1);
    Events[MenuAction2Name].guiName = ExtractPositionName(parkedOrientationMenu2);
    Events[MenuAction0Name].active = Events[MenuAction0Name].guiName != "" && !isLinked;
    Events[MenuAction1Name].active = Events[MenuAction1Name].guiName != "" && !isLinked;
    Events[MenuAction2Name].active = Events[MenuAction2Name].guiName != "" && !isLinked;
    Events[ExtendAtMaxMenuActionName].active = !isLinked;
    Events[RetractToMinMenuActionName].active = !isLinked;
  }
  #endregion

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

  #region Private utility methods
  /// <summary>Returns parked menu item name.</summary>
  /// <param name="cfgSetting">String from config of the following format:
  /// <c>X,Y,Z,&lt;menu text&gt;</c>, where <c>X,Y,Z</c> defines direction in node's local
  /// coordinates, and <c>menu text</c> is a string to show in context menu.</param>
  /// <returns></returns>
  string ExtractPositionName(string cfgSetting) {
    var lastCommaPos = cfgSetting.LastIndexOf(',');
    return lastCommaPos != -1
        ? cfgSetting.Substring(lastCommaPos + 1)
        : cfgSetting;
  }

  /// <summary>Returns direction vector for a parked menu item.</summary>
  /// <param name="cfgSetting">String from config of the following format:
  /// <c>X,Y,Z,&lt;menu text&gt;</c>, where <c>X,Y,Z</c> defines direction in node's local
  /// coordinates, and <c>menu text</c> is a string to show in context menu.</param>
  /// <returns></returns>
  Vector3 ExtractOrientationVector(string cfgSetting) {
    var lastCommaPos = cfgSetting.LastIndexOf(',');
    if (lastCommaPos == -1) {
      Debug.LogWarningFormat("Cannot extract direction from string: {0}", cfgSetting);
      return Vector3.forward;
    }
    return ConfigNode.ParseVector3(cfgSetting.Substring(0, lastCommaPos));
  }

  /// <summary>Calculates and populates min/max link lengths from the model.</summary>
  void CalculateLengthLimits() {
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
  #endregion
}

}  // namespace
