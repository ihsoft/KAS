// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using System.Linq;
using TestScripts;
using UnityEngine;
using KASAPIv1;
using HighlightingSystem;
using KSPDev.ModelUtils;

namespace KAS {

// FIXME: docs
public class KASModuleTelescopicPipeStrut
    : AbstractJointPart, ILinkRenderer, ILinkStateEventListener {
  // These fileds must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  [KSPField]
  public int pistonsCount = 3;
  [KSPField]
  public float outerPistonDiameter = 0.15f;
  [KSPField]
  public float pistonLength = 0.2f;
  [KSPField]
  public float pistonWallThickness = 0.01f;
  [KSPField]
  public string pistonTexturePath = "";
  
  [KSPField]
  public float pistonMinShift = 0.02f;
  [KSPField]
  public string parkedOrientationMenu0 = "";
  [KSPField]
  public string parkedOrientationMenu1 = "";
  [KSPField]
  public string parkedOrientationMenu2 = "";
  #endregion

  // These fileds must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  [KSPField(isPersistant = true)]
  public Vector3 parkedOrientation = Vector3.zero;
  [KSPField(isPersistant = true)]
  //FIXME not valid to have 0
  public float parkedLength = 0; // If 0 then minimum link length will be used.
  #endregion

  protected const string parkedOrientationMenuAction0 = "ParkedOrientationMenuAction0";
  protected const string parkedOrientationMenuAction1 = "ParkedOrientationMenuAction1";
  protected const string parkedOrientationMenuAction2 = "ParkedOrientationMenuAction2";

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    Events[parkedOrientationMenuAction0].guiName = ExtractPositionName(parkedOrientationMenu0);
    Events[parkedOrientationMenuAction1].guiName = ExtractPositionName(parkedOrientationMenu1);
    Events[parkedOrientationMenuAction2].guiName = ExtractPositionName(parkedOrientationMenu2);
    UpdateMenuItems();
    if (parkedOrientation == Vector3.zero) {
      parkedOrientation = ExtractOrientationVector(parkedOrientationMenu0);
    }
  }

  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
    //parkedOrientation = 
    linkSource = part.FindModuleImplementing<ILinkSource>();
    if (linkSource == null) {
      Debug.LogErrorFormat("Wring setup of: {0}", part.name);
    }
    UpdateMenuItems();
  }
  #endregion

  #region ILinkRenderer implemetation
  public string cfgRendererName { get { return rendererName; } }
  [KSPField]
  public string rendererName = "";

  public virtual Color? colorOverride { get; set; }
  public virtual string shaderNameOverride { get; set; }
  public virtual bool isPhysicalCollider { get; set; }

  /// <inheritdoc/>
  public virtual void StartRenderer(Transform source, Transform target) {
    // Source pivot is fixed for the part. Do a safe check to verify if requestor asked for the
    // right coordinates.
    if (Mathf.Approximately(Vector3.SqrMagnitude(source.position - partJointPivot.position),
                            Mathf.Epsilon)) {
      Debug.LogErrorFormat("Render source doesn't match pivot point: pivot={0}, source={1}",
                           partJointPivot.position, source.position);
    }
    //FIXME
    Debug.LogWarning("Draw to the target");
  }

  /// <inheritdoc/>
  public virtual void StopRenderer() {
    
  }

  /// <inheritdoc/>
  public virtual void UpdateLink() {
    
  }

  /// <inheritdoc/>
  public virtual string CheckColliderHits(Transform source, Transform target) {
    return "TEST ERROR";
  }
  #endregion

  protected ILinkSource linkSource { get; private set; }
  protected bool isLinked {
    get { return linkSource != null && linkSource.linkState == LinkState.Linked; }
  }

  // FIXME: check colliders.
  #region Action handlers
  [KSPEvent(guiName = "Pipe position 0", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = false, active = false)]
  public void ParkedOrientationMenuAction0() {
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu0);
    UpdateLinkLengthAndOrientation();
  }

  [KSPEvent(guiName = "Pipe position 1", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = false, active = false)]
  public void ParkedOrientationMenuAction1() {
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu1);
    UpdateLinkLengthAndOrientation();
  }

  [KSPEvent(guiName = "Pipe position 2", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = false, active = false)]
  public void ParkedOrientationMenuAction2() {
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu2);
    UpdateLinkLengthAndOrientation();
  }

  [KSPEvent(guiName = "Extend to max", guiActiveEditor = true, active = true)]
  public void ExtendAtMaxMenuAction() {
    var trgJoint = Hierarchy.FindTransformByPath(partJointPivot, "**/" + TrgStrutJointObjName);
    trgJoint.localPosition = new Vector3(0, 0, maxLinkLength - trgJointHandleLength);
    UpdateLinkLengthAndOrientation();
  }

  [KSPEvent(guiName = "Retract to min", guiActiveEditor = true, active = true)]
  public void RetractToMinMenuAction() {
    var trgJoint = Hierarchy.FindTransformByPath(partJointPivot, "**/" + TrgStrutJointObjName);
    trgJoint.localPosition = new Vector3(0, 0, minLinkLength - trgJointHandleLength);
    UpdateLinkLengthAndOrientation();
  }
  #endregion

  // FIXME: docs for all below
  protected GameObject[] pistons;
  protected const string PartJointObjName = "partJoint";
  protected const string SrcStrutJointObjName = "srcStrutJoint";
  protected const string TrgStrutJointObjName = "trgStrutJoint";

  protected Transform partJointPivot { get; private set;}
//  protected Transform srcStrutJoint { get; private set; }
//  protected Transform trgStrutJoint { get; private set; }
//  protected Transform trgStrutPivot { get; private set;}
  protected float srcJointHandleLength { get; private set; }
  protected float trgJointHandleLength { get; private set; }
  /// <summary>Maximum possible link length with this part.</summary>
  protected float maxLinkLength {
    get {
      return
          srcJointHandleLength
          + pistonsCount * (pistonLength - pistonMinShift)
          + trgJointHandleLength;
    }
  }
  /// <summary>Minimum possible link length with this part.</summary>
  protected float minLinkLength {
    get {
      return
          srcJointHandleLength
          + pistonLength + (pistonsCount - 1) * pistonMinShift
          + trgJointHandleLength;
    }
  }
  
  /// <inheritdoc/>
  protected override void CreatePartModel() {
    var attachNode = CreateAttachNodeTransform();

    // Set init state.
    if (Mathf.Approximately(parkedLength, float.Epsilon)) {
      parkedLength = pistonLength + pistonMinShift * (pistonsCount - 1);
    }

    // Part's joint model.
    var partJoint = CreateStrutJointModel(PartJointObjName);
    Hierarchy.MoveToParent(partJoint, attachNode);
    partJointPivot = Hierarchy.FindTransformInChildren(partJoint, PivotAxileObjName);
    partJointPivot.localRotation = Quaternion.LookRotation(parkedOrientation);

    // Source strut joint model.
    var srcStrutJoint = CreateStrutJointModel(SrcStrutJointObjName, createAxile: false);
    var srcStrutPivot = Hierarchy.FindTransformInChildren(srcStrutJoint, PivotAxileObjName);
    srcJointHandleLength = Vector3.Distance(srcStrutJoint.position, srcStrutPivot.position);
    Hierarchy.MoveToParent(srcStrutJoint, partJointPivot,
                            newPosition: srcStrutPivot.position - srcStrutJoint.position,
                            newRotation: Quaternion.LookRotation(Vector3.back));

    // Target strut joint model.
    var trgStrutJoint = CreateStrutJointModel(TrgStrutJointObjName, createAxile: false);
    var trgStrutPivot = Hierarchy.FindTransformInChildren(trgStrutJoint, PivotAxileObjName);
    trgJointHandleLength = Vector3.Distance(trgStrutJoint.position, trgStrutPivot.position);
    Hierarchy.MoveToParent(trgStrutJoint, partJointPivot,
                            newPosition: new Vector3(0, 0, srcJointHandleLength + parkedLength));

    // Pistons.
    pistons = new GameObject[pistonsCount];
    var startDiameter = outerPistonDiameter;
    var material = CreateMaterial(GetTexture(pistonTexturePath));
    for (var i = 0; i < pistonsCount; ++i) {
      var piston = CreateCylinder(startDiameter, pistonLength, material, parent: srcStrutJoint);
      piston.name = "piston" + i;
      // Source strut joint rotation is reversed.
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

    UpdateLinkLengthAndOrientation();
  }

  /// <inheritdoc/>
  protected override void LoadPartModel() {
    // Main pivot.
    partJointPivot = Hierarchy.FindTransformByPath(
        partModelTransform,
        AttachNodeObjName + "/" + PartJointObjName + "/**/" + PivotAxileObjName);
    // Source joint.
    var srcStrutJoint = Hierarchy.FindTransformInChildren(partJointPivot, SrcStrutJointObjName);
    var srcStrutPivot = Hierarchy.FindTransformInChildren(srcStrutJoint, PivotAxileObjName);
    srcJointHandleLength = Vector3.Distance(srcStrutJoint.position, srcStrutPivot.position);
    // Target joint.
    var trgStrutJoint = Hierarchy.FindTransformInChildren(partJointPivot, TrgStrutJointObjName);
    var trgStrutPivot = Hierarchy.FindTransformInChildren(trgStrutJoint, PivotAxileObjName);
    trgJointHandleLength = Vector3.Distance(trgStrutJoint.position, trgStrutPivot.position);
    // Pistons.
    pistons = new GameObject[pistonsCount];
    for (var i = 0; i < pistonsCount; ++i) {
      pistons[i] = Hierarchy.FindTransformInChildren(partModelTransform, "piston" + i).gameObject;
    }
  }

  /// <summary>Adjusts link models to the changed target position.</summary>
  /// FIXME: pass target as argument
  protected virtual void UpdateLinkLengthAndOrientation() {
    // FIXME: only use parked orientation when is not connected. 
    partJointPivot.localRotation = Quaternion.LookRotation(parkedOrientation);
    if (pistons.Length > 2) {
      var offset = pistons[0].transform.localPosition.z;
      var step = Vector3.Distance(pistons.Last().transform.position, pistons[0].transform.position)
          / (pistonsCount - 1);
      for (var i = 1; i < pistons.Length - 1; ++i) {
        offset -= step;  // Pistons are distributed to -Z direction of the pviot.
        pistons[i].transform.localPosition = new Vector3(0, 0, offset);
      }
    }
  }

  #region ILinkStateEventListener implementation
  /// <inheritdoc/>
  public void OnKASLinkCreatedEvent(KASEvents.LinkEvent info) {
    Debug.LogWarningFormat("** LINKED!");
  }

  /// <inheritdoc/>
  public void OnKASLinkBrokenEvent(KASEvents.LinkEvent info) {
    Debug.LogWarningFormat("** UNLINKED!");
  }
  #endregion

  #region Privat utility methods
  string ExtractPositionName(string cfgDirectionString) {
    var lastCommaPos = cfgDirectionString.LastIndexOf(',');
    return lastCommaPos != -1
        ? cfgDirectionString.Substring(lastCommaPos + 1)
        : cfgDirectionString;
  }

  Vector3 ExtractOrientationVector(string cfgDirectionString) {
    var lastCommaPos = cfgDirectionString.LastIndexOf(',');
    if (lastCommaPos == -1) {
      Debug.LogWarningFormat("Cannot extract direction from string: {0}", cfgDirectionString);
      return Vector3.forward;
    }
    return ConfigNode.ParseVector3(cfgDirectionString.Substring(0, lastCommaPos));
  }

  void UpdateMenuItems() {
    Events[parkedOrientationMenuAction0].active =
        ExtractPositionName(parkedOrientationMenu0) != "" && !isLinked;
    Events[parkedOrientationMenuAction0].guiActiveEditor =
        Events[parkedOrientationMenuAction0].active;
    Events[parkedOrientationMenuAction1].active =
        ExtractPositionName(parkedOrientationMenu1) != "" && !isLinked;
    Events[parkedOrientationMenuAction1].guiActiveEditor =
        Events[parkedOrientationMenuAction1].active;
    Events[parkedOrientationMenuAction2].active =
        ExtractPositionName(parkedOrientationMenu2) != "" && !isLinked;
    Events[parkedOrientationMenuAction2].guiActiveEditor =
        Events[parkedOrientationMenuAction2].active;
  }
  #endregion
}

}  // namespace
