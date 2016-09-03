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

namespace KAS {

// FIXME: docs
public class KASModuleTelescopicPipeStrut : AbstractJointPart {
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
  [KSPField(isPersistant = true)]
  public Vector3 parkedOrientation = Vector3.up;
  [KSPField]
  public string parkedOrientationMenu0 = "";
  [KSPField]
  public string parkedOrientationMenu1 = "";
  [KSPField]
  public string parkedOrientationMenu2 = "";
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
    //FIXME: update on load and start
    UpdateMenuItems();
  }
  #endregion

  bool isConnected = false;
  void UpdateMenuItems() {
    Events[parkedOrientationMenuAction0].active =
        ExtractPositionName(parkedOrientationMenu0) != "" && !isConnected;
    Events[parkedOrientationMenuAction0].guiActiveEditor =
        Events[parkedOrientationMenuAction0].active;
    Events[parkedOrientationMenuAction1].active =
        ExtractPositionName(parkedOrientationMenu1) != "" && !isConnected;
    Events[parkedOrientationMenuAction1].guiActiveEditor =
        Events[parkedOrientationMenuAction1].active;
    Events[parkedOrientationMenuAction2].active =
        ExtractPositionName(parkedOrientationMenu2) != "" && !isConnected;
    Events[parkedOrientationMenuAction2].guiActiveEditor =
        Events[parkedOrientationMenuAction2].active;
  }

  #region Action handlers
  [KSPEvent(guiName = "Pipe position 0", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = false, active = false)]
  public void ParkedOrientationMenuAction0() {
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu0);
    UpdatePistons();
  }

  [KSPEvent(guiName = "Pipe position 1", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = false, active = false)]
  public void ParkedOrientationMenuAction1() {
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu1);
    UpdatePistons();
  }

  [KSPEvent(guiName = "Pipe position 2", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = false, active = false)]
  public void ParkedOrientationMenuAction2() {
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu2);
    UpdatePistons();
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
  
  /// <inheritdoc/>
  protected override void CreatePartModel() {
    //FIXME
    Debug.LogWarning("** CreatePartModel");
    var attachNode = CreateAttachNodeTransform();

    // Part's joint model.
    var partJoint = CreateStrutJointModel(PartJointObjName);
    MoveToParent(partJoint, attachNode);
    partJointPivot = FindTransformInChildren(partJoint, PivotAxileObjName);
    partJointPivot.localRotation = Quaternion.LookRotation(parkedOrientation);

    // Source strut joint model.
    var srcStrutJoint = CreateStrutJointModel(SrcStrutJointObjName, createAxile: false);
    var srcStrutJointPivot = FindTransformInChildren(srcStrutJoint, PivotAxileObjName);
    MoveToParent(srcStrutJoint, partJointPivot,
                 newPosition: srcStrutJointPivot.position - srcStrutJoint.position, 
                 newRotation: Quaternion.LookRotation(Vector3.back));

    // Target strut joint model.
    var trgStrutJoint = CreateStrutJointModel(TrgStrutJointObjName, createAxile: false);
    var trgStrutPivot = FindTransformInChildren(trgStrutJoint, PivotAxileObjName);
    var minLength = pistonLength + pistonMinShift * (pistonsCount - 1);
    MoveToParent(trgStrutJoint, partJointPivot,
                 newPosition: srcStrutJoint.localPosition + new Vector3(0, 0, minLength));
    trgStrutPivot = trgStrutJoint.transform;

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
    MoveToParent(pistons.Last().transform, trgStrutJoint,
                 newPosition: new Vector3(0, 0, -pistonLength / 2),
                 newRotation: Quaternion.LookRotation(Vector3.forward));

    //FIXME
    var test = FindTransformByPath(
        partModelTransform,
        AttachNodeObjName + "/" + PartJointObjName + "/**/" + PivotAxileObjName);
    Debug.LogWarningFormat("** FOUND expected for [{1}]: {0}",
                           test == partJointPivot,
                           AttachNodeObjName + "/" + PartJointObjName + "/**/" + PivotAxileObjName);

    UpdatePistons();
  }

  /// <inheritdoc/>
  protected override void LoadPartModel() {
    //FIXME
    Debug.LogWarning("** LoadPartModel");
    partJointPivot = FindTransformByPath(
        partModelTransform,
        AttachNodeObjName + "/" + PartJointObjName + "/**/" + PivotAxileObjName);
    Debug.LogWarningFormat("** loaded joint pivot: {0}", partJointPivot);
    pistons = new GameObject[pistonsCount];
    for (var i = 0; i < pistonsCount; ++i) {
      pistons[i] = FindTransformInChildren(partModelTransform, "piston" + i).gameObject;
    }
  }

  void UpdatePistons() {
    partJointPivot.localRotation = Quaternion.LookRotation(parkedOrientation);
    if (pistons.Length > 2) {
      var offset = pistons[0].transform.localPosition.z;
      var step = Vector3.Distance(pistons.Last().transform.position, pistons[0].transform.position)
          / (pistonsCount - 1);
      //FIXME
      Debug.LogWarningFormat("** pistons Step: {0}", step);
      for (var i = 1; i < pistons.Length - 1; ++i) {
        offset -= step;  // Pistons are distributed to -Z direction of the pviot.
        pistons[i].transform.localPosition = new Vector3(0, 0, offset);
      }
    }
  }

  void DeletePistons() {
    if (pistons != null) {
      foreach (var piston in pistons) {
        piston.DestroyGameObject();
      }
      pistons = null;
    }
  }

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
}

}  // namespace
