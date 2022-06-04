// Kerbal Attachment System
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Text;
using System.Linq;
using KASAPIv2;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.PartUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KAS {

/// <summary>Module that initiates an attachment on impact.</summary>
/// <remarks>
/// <p>
/// The part with this module "sticks" to another part if it hits it with an enough force and at the angle within the
/// limits. The behavior is the same as on the dart that hits the target. On hit the dart part couples with the target
/// part and becomes a part of its vessel.
/// </p>
/// <p>
/// If the target is surface, then a static attachment to the world is made. However, this state is intentionally NOT
/// maintained on save/load. KSP never places vessels precisely on load, so a static attachment has a good chance of
/// awakening Kraken. For this reason, when the vessel is loaded, all its surface attachments will be forcibly reset.
/// </p>
/// </remarks>
// Next localization ID: #kasLOC_14007.
// ReSharper disable once InconsistentNaming
public sealed class KASModuleDart : AbstractPartModule,
                                    IModuleInfo, IKSPDevModuleInfo, 
                                    IHasContextMenu {
  #region Localizable GUI strings.
  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ModuleTitle = new(
      "#kasLOC_14000",
      defaultTemplate: "KAS Dart",
      description: "Title of the module to present in the editor details window.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<ForceType> ImpactForceRequiredInfo = new(
      "#kasLOC_14001",
      defaultTemplate: "Impact force needed: <<1>>",
      description: "Info string that tells how strong must be the impact to have the attachment established."
      + "\nArgument <<1>> is the force of type ForceType.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<ForceType> SurfaceAttachStrengthInfo = new(
      "#kasLOC_14002",
      defaultTemplate: "Surface attach strength: <<1>>",
      description: "Info string that tells how strong is the surface attachment if this kind is allowed for the part."
      + "\nArgument <<1>> is the force of type ForceType.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CanAttachToPartInfo = new(
      "#kasLOC_14003",
      defaultTemplate: "Attaches to parts",
      description: "Info string that indicates that the dart can attach to a vessel part.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CanAttachToSurfaceInfo = new(
      "#kasLOC_14004",
      defaultTemplate: "Attaches to surface",
      description: "Info string that indicates that the dart can attach to a celestial body.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CanAttachToSpaceObjectInfo = new(
      "#kasLOC_14005",
      defaultTemplate: "Attaches to asteroid or comet",
      description: "Info string that indicates that the dart can attach to space object like an asteroid or a comet.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message PrimaryInfo = new(
      "#kasLOC_14006",
      defaultTemplate: "Attaches on impact\nRequires kerbal to detach",
      description: "Info string that indicates that this part attaches on impact and requires a kerbal to be EVA to"
      + " detach. This string is shown in the main part information area.");
  #endregion

  #region Part's config fields
  /// <summary>The direction of the part's contact point.</summary>
  /// <remarks>
  /// The closest collider point in this direction (from the part's zero position) will be the point where the hit is
  /// checked. The direction will be the normal at this point.
  /// </remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Hit point direction")]
  public Vector3 hitPointDirection = Vector3.down;

  /// <summary>The maximum allowed angle between the normals at the hit point.</summary>
  /// <remarks>
  /// The normals at the part's contact point and the target collider hit normal must be less than this setting to have
  /// the attachment made.
  /// </remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Maximum hit angle")]
  public float maxHitAngle = 45.0f;

  /// <summary>The impact force in newtons needed to make the attachment.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Minimum impact force")]
  public float forceNeeded = 5;

  /// <summary>Indicates if the dart can attach to a part.</summary>
  /// <remarks>The dart will couple with the part if the hit impulse condition is met.</remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("can attach to part")]
  public bool attachToPart;

  /// <summary>Indicates if the dart can attach to the planet surface.</summary>
  /// <remarks>A static attachment will be made at the hit point.</remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("can attach to surface")]
  public bool attachToSurface;

  /// <summary>Indicates if the dart can attach to the asteroid surface.</summary>
  /// <remarks>The dart will couple with the asteroid if the hit force condition is met.</remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("can attach to asteroid")]
  public bool attachToAsteroid;

  /// <summary>Indicates if the dart's forward orientation should be aligned to the hit normal.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("align to hit normal")]
  public bool alignToHitNormal;

  /// <summary>The joint strength when attaching to the planet surface.</summary>
  /// <remarks>It doesn't make sense if <seealso cref="attachToSurface"/> is not set.</remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Break force")]
  public float breakForce = 10;

  /// <summary>The sound to play when the dart attaches to the surface.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Surface attach sound")]
  public string surfaceAttachSndPath = "";

  /// <summary>The sound to play when the dart attaches to a part.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Part attach sound")]
  public string partAttachSndPath = "";

  /// <summary>The sound to play when the dart is detached from the target via EVA.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Detach sound")]
  public string detachSndPath = "";

  /// <summary>The context menu item name to disconnect an attached dart via EVA.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Detach event text")]
  public string detachDartEventText = "Detach dart";
  #endregion

  #region Local fields & properties
  /// <summary>Max distance to search for the dart collider hit point.</summary>
  const float MaxColliderBound = 100.0f;

  /// <summary>The current joint of the dart and the hit object.</summary>
  ConfigurableJoint _joint;

  /// <summary>The velocity of this part RB in the <i>previous</i> physical frame.</summary>
  /// <remarks>The current frame velocity may be incorrect due to collision enhancer behavior.</remarks>
  Vector3 _lastFrameVelocity;

  /// <summary>The velocity of this part RB in the <i>current</i> physical frame.</summary>
  /// <remarks>It's a buffer for tracking the last frame velocity.</remarks>
  Vector3 _thisFrameVelocity;
  #endregion

  #region Context menu events/actions
  /// <summary>A context menu item that detaches the dart.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(tag = null)]
  public void DetachDartEvent() {
    if (!isAttached) {
      return;
    }
    if (tgtPartId == 0) {
      isAttached = false;
      DestroyImmediate(_joint);
    } else {
      isAttached = false;
      if (part.parent.flightID == tgtPartId) {
        part.decouple();
      } else if (part.children.Count == 1 && part.children[0].flightID == tgtPartId) {
        part.children[0].decouple();
      } else {
        HostedDebugLog.Error(this, "Cannot find attached part: id={0}", tgtPartId);
      }
    }
    tgtPartId = 0;
    UISoundPlayer.instance.Play(detachSndPath);
    UpdateContextMenu();
    HostedDebugLog.Info(this, "Dart detached");
  }
  #endregion

  #region Persistant fields
  /// <summary>Indicates that the drat is attached.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPField(isPersistant = true)]
  public bool isAttached;

  /// <summary>The flight ID of the part the dart is attached to.</summary>
  /// <remarks>Zero if attached to surface.</remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPField(isPersistant = true)]
  public uint tgtPartId;
  #endregion

  #region IHasContextMenu implementation
  /// <inheritdoc/>
  public void UpdateContextMenu() {
    if (part.partInfo == null || part.partInfo.partPrefab == part) {
      return; // It's a prefab.
    }
    PartModuleUtils.SetupEvent(this, DetachDartEvent, e => {
      e.active = isAttached;
    });
  }
  #endregion

  #region IsLocalizableModule overrides
  /// <inheritdoc/>
  public override void LocalizeModule() {
    base.LocalizeModule();
    PartModuleUtils.SetupEvent(this, DetachDartEvent, e => {
      e.guiName = detachDartEventText;
    });
  }
  #endregion

  #region IHasDebugAdjustables overrides
  /// <inheritdoc/>
  public override void OnDebugAdjustablesUpdated() {
    base.OnDebugAdjustablesUpdated();
    LocalizeModule();
  }
  #endregion

  #region IModuleInfo implementation
  /// <inheritdoc cref="IKSPDevModuleInfo.GetInfo" />
  public override string GetInfo() {
    var sb = new StringBuilder();
    sb.AppendLine(ImpactForceRequiredInfo.Format(forceNeeded));
    if (attachToSurface) {
      sb.AppendLine(SurfaceAttachStrengthInfo.Format(breakForce));
    }
    if (attachToAsteroid || attachToPart || attachToSurface) {
      sb.AppendLine();
    }
    if (attachToSurface) {
      sb.AppendLine(ScreenMessaging.SetColorToRichText(CanAttachToSurfaceInfo, Color.cyan));
    }
    if (attachToPart) {
      sb.AppendLine(ScreenMessaging.SetColorToRichText(CanAttachToPartInfo, Color.cyan));
    }
    if (attachToAsteroid) {
      sb.AppendLine(ScreenMessaging.SetColorToRichText(CanAttachToSpaceObjectInfo, Color.cyan));
    }
    return sb.ToString().Trim();
  }

  /// <inheritdoc cref="IKSPDevModuleInfo.GetModuleTitle" />
  public string GetModuleTitle() {
    return ModuleTitle;
  }

  /// <inheritdoc cref="IKSPDevModuleInfo.GetDrawModulePanelCallback" />
  public Callback<Rect> GetDrawModulePanelCallback() {
    return null;
  }

  /// <inheritdoc cref="IKSPDevModuleInfo.GetPrimaryField" />
  public string GetPrimaryField() {
    return PrimaryInfo;
  }
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  protected override void CheckSettingsConsistency() {
    base.CheckSettingsConsistency();
    if (isAttached && part.parent == null) {
      HostedDebugLog.Warning(this, "Reset attachment state: the surface attach is purposely not retained");
      isAttached = false;
    }
    if (!attachToAsteroid && !attachToPart && !attachToSurface) {
      HostedDebugLog.Warning(this, "Part must be attachable to something!");
      attachToPart = true;
      alignToHitNormal = true;
    }
  }

  /// <inheritdoc/>
  protected override void InitModuleSettings() {
    base.InitModuleSettings();
    UpdateContextMenu();
  }

  /// <inheritdoc/>
  public override void OnStart(StartState state) {
    base.OnStart(state);
    RegisterGameEventListener(GameEvents.onVesselGoOnRails, OnVesselGoOnRailsEvent);
    RegisterGameEventListener(GameEvents.onPartDeCouple, OnPartDeCoupleEvent);
    RegisterGameEventListener(GameEvents.OnCollisionEnhancerHit, OnCollisionEnhancerHitEvent);
  }
  #endregion

  #region MonoBehaviour messages
  /// <summary>Captures the RB velocity BEFORE collision enhancer acted on it.</summary>
  /// <remarks>
  /// We use the velocity from the frame before the collision as it's recognized by  the CollisionEnhancer. It's not the
  /// ideal an approximation, but it works in most cases. After all, all we need form the relative velocity is to check
  /// if the attachment can be made.
  /// </remarks>
  void FixedUpdate() {
    if (part.rb == null) {
      return;
    }
    _lastFrameVelocity = _thisFrameVelocity;
    _thisFrameVelocity = part.rb.velocity;
  }

  /// <summary>Reacts on the dart collision with anything but the surface.</summary>
  /// <remarks>The surface hits are managed via the collision enhancer.</remarks>
  /// <seealso cref="OnCollisionEnhancerHitEvent"/>
  void OnCollisionEnter(Collision collision) {
    var thisPartRb = part.rb;
    if (thisPartRb.collisionDetectionMode != CollisionDetectionMode.Continuous) {
      return;  // Not in the ejected mode.
    }

    // The direction in which the impact logic is to be applied.
    var partTransform = transform;
    var rayDirection = partTransform.TransformDirection(hitPointDirection).normalized;

    // Find the hit for this collision.
    var contactPt = part.collider.ClosestPoint(partTransform.position + rayDirection * MaxColliderBound);
    var oppositePt = part.collider.ClosestPoint(partTransform.position - rayDirection * MaxColliderBound);
    var rayLenght = (oppositePt - contactPt).magnitude + 0.1f;  // Add 10cm for collision solver error.
    var hit = Physics.RaycastAll(
            oppositePt, rayDirection, rayLenght, (int) KspLayerMask.Part, QueryTriggerInteraction.Ignore)
        .FirstOrDefault(x => x.collider == collision.collider);
    if (hit.Equals(default(RaycastHit))) {
      return;  // Not our turn.
    }
    thisPartRb.collisionDetectionMode = CollisionDetectionMode.Discrete;

    MaybeAttachOnHit(hit, collision.relativeVelocity);
  }
  #endregion

  #region Local utility methods
  /// <summary>Attaches to the target part.</summary>
  IEnumerator WaitAndCouple(Part hitPart) {
    var partTransform = transform;
    partTransform.parent = hitPart.transform;
    var keepPos = partTransform.localPosition;
    var keepRot = partTransform.localRotation;
    yield return null;  // Cannot couple in the physics callback.
    HostedDebugLog.Info(this, "Coupling with the hit part: {0}", hitPart);
    DestroyImmediate(_joint);  // It must be immediate to let the coupling logic to work.
    var untrackedObjectSize = (UntrackedObjectClass)Enum.Parse(
        typeof(UntrackedObjectClass), hitPart.vessel.DiscoveryInfo.size.Value);
    partTransform.localPosition = keepPos;
    partTransform.localRotation = keepRot;
    partTransform.parent = null;
    part.Couple(hitPart);
    if (hitPart.FindModuleImplementing<ModuleAsteroid>() != null
        || hitPart.FindModuleImplementing<ModuleComet>() != null) {
      // This line of logic is simply copied from the stock grapple part. No idea what's its purpose.
      vessel.DiscoveryInfo.SetUntrackedObjectSize(untrackedObjectSize);
      UISoundPlayer.instance.Play(surfaceAttachSndPath);
    } else {
      UISoundPlayer.instance.Play(partAttachSndPath);
    }
    GameEvents.onVesselWasModified.Fire(vessel);
  }

  /// <summary>Resets the collision mode.</summary>
  void OnVesselGoOnRailsEvent(Vessel v) {
    if (v == vessel) {
      // When on rails, all the parts RBs are kinematic, and they cannot have Continuous colliders.
      // If the mode is not reset, the dart logic starts behaving really weird.
      part.rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
    }
  }

  /// <summary>Reacts on a part coupling and adjusts its colliders as needed.</summary>
  void OnPartDeCoupleEvent(Part p) {
    if (!isAttached || (p != part && p != part.parent)) {
      return;
    }
    HostedDebugLog.Info(this, "Dart attachment has been broken externally");
    isAttached = false;
    UpdateContextMenu();
  }

  /// <summary>Reacts on hits with the celestial bodies.</summary>
  /// <remarks>
  /// It's a special case since <c>CollisionEnhancer</c> enters the game. It's not the case when the dart hits a part.
  /// </remarks>
  /// <seealso cref="OnCollisionEnter"/>
  void OnCollisionEnhancerHitEvent(Part p, RaycastHit hit) {
    if (p != part || part.rb.collisionDetectionMode != CollisionDetectionMode.Continuous) {
      return;
    }
    part.rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
    MaybeAttachOnHit(hit, _lastFrameVelocity);
  }

  /// <summary>Verifies if the hit is right, and attaches. Or doesn't.</summary>
  /// <param name="hit">Where the hit of the dart has happen.</param>
  /// <param name="relativeVelocity">The velocity at the hit.</param>
  void MaybeAttachOnHit(RaycastHit hit, Vector3 relativeVelocity) {
    var thisPartRb = part.rb;
    var partTransform = transform;
    var rayDirection = partTransform.TransformDirection(hitPointDirection).normalized;

    // Check for impact force.
    var hitForce = relativeVelocity.magnitude * thisPartRb.mass / Time.fixedDeltaTime;
    if (hitForce < forceNeeded) {
      HostedDebugLog.Info(this, "Dart impact force is too low: impulse={0}, required={1}", hitForce, forceNeeded);
      return;
    }

    // Check for ricochet.
    var hitAngle = Vector3.Angle(-rayDirection, hit.normal);
    if (hitAngle > maxHitAngle) {
      HostedDebugLog.Info(this, "Ignore collision due to angle restriction: collider={0}, angle={1}, maxAngle={2}",
                          hit.collider, hitAngle, maxHitAngle);
      return;
    }

    // Adjust the collision point to match the dart's hit point.
    var contactPt = part.collider.ClosestPoint(partTransform.position + rayDirection * MaxColliderBound);
    if (alignToHitNormal) {
      partTransform.position = hit.point + hit.normal * (transform.position - contactPt).magnitude;
      partTransform.rotation = Quaternion.FromToRotation(hitPointDirection, -hit.normal);
    } else {
      // This is not a precise location, but the approach will work fine in the most situations.
      partTransform.position -= contactPt - hit.point;
    }

    // Attach to the hit object.
    var jointStrength = float.PositiveInfinity;
    if (hit.rigidbody == null) {
      if (attachToSurface) {
        HostedDebugLog.Info(this, "Dart hits surface and attaches to it at: {0}", hit.point);
        jointStrength = breakForce;
        isAttached = true;
        tgtPartId = 0;
        UISoundPlayer.instance.Play(surfaceAttachSndPath);
      } else {
        HostedDebugLog.Fine(this, "Ignoring the surface hit: the dart is not allowed to attach to surface");
      }
    } else if (attachToAsteroid || attachToPart) {
      var hitPart = FlightGlobals.GetPartUpwardsCached(hit.transform.gameObject);
      if (hitPart == null) {
        HostedDebugLog.Warning(this, "The hit object is not a part: {0}", hit.transform);
        return;
      }
      var isSpaceObject = hitPart.FindModuleImplementing<ModuleAsteroid>() != null
          || hitPart.FindModuleImplementing<ModuleComet>() != null;
      if (!attachToAsteroid && isSpaceObject) {
        HostedDebugLog.Fine(
            this, "Ignoring the space object hit: the dart is not allowed to attach to an asteroid or a comet");
        return;
      }
      if (!attachToPart && !isSpaceObject) {
        HostedDebugLog.Fine(this, "Ignoring the part hit: the dart is not allowed to attach to a part");
        return;
      }
      isAttached = true;
      tgtPartId = hitPart.flightID;
      StartCoroutine(WaitAndCouple(hitPart));
    }

    // Always make the joint to capture the impulse!
    if (isAttached) {
      _joint = gameObject.AddComponent<ConfigurableJoint>();
      KASAPI.JointUtils.ResetJoint(_joint);
      KASAPI.JointUtils.SetupFixedJoint(_joint);
      _joint.breakForce = jointStrength;
      UpdateContextMenu();
    }
  }
  #endregion
}
}
