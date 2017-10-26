// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.LogUtils;
using KSPDev.KSPInterfaces;

namespace KAS {

/// <summary>Module that keeps track of the vessels info as they get linked and unlinked.</summary>
/// <remarks>
/// This module is primarily designed for the KAS utils methods that deal with the links. However,
/// the thrird party mods can also read/write this information when they need to.
/// </remarks>
/// <seealso cref="ILinkUtils.CoupleParts"/>
/// <seealso cref="ILinkUtils.DecoupleParts"/>
public class KASModuleVesselInfo : PartModule,
    // KAS interfaces.
    ILinkVesselInfo,
    // KSPDev syntax sugar interfaces.
    IPartModule, IsDestroyable {

  #region ILinkVesselInfo
  /// <inheritdoc/>
  public DockedVesselInfo vesselInfo { get; set; }
  #endregion

  const string ConfigFieldName = "VESSELINFO";

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    GameEvents.onVesselRename.Add(OnVesselRename);
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    var vesselInfoNode = node.GetNode(ConfigFieldName);
    if (vesselInfoNode != null) {
      vesselInfo = new DockedVesselInfo();
      vesselInfo.Load(vesselInfoNode);
    }
  }

  /// <inheritdoc/>
  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    if (vesselInfo != null) {
      var vesselInfoNode = node.AddNode(ConfigFieldName);
      vesselInfo.Save(vesselInfoNode);
    }
  }
  #endregion

  #region IsDestroyable implementation
  /// <inheritdoc/>
  public virtual void OnDestroy() {
    GameEvents.onVesselRename.Remove(OnVesselRename);
  }
  #endregion

  #region Local untility methods
  /// <summary>
  /// Reacts on the vessel name change and updates the vessel info in the module as needed.
  /// </summary>
  void OnVesselRename(GameEvents.HostedFromToAction<Vessel, string> action) {
    if (action.host == vessel && vesselInfo != null
        && vesselInfo.rootPartUId == action.host.rootPart.flightID) {
      vesselInfo.name = action.host.vesselName;
      vesselInfo.vesselType = action.host.vesselType;
      HostedDebugLog.Fine(this, "Update vessel info to: name={0}, type={1}",
                          vesselInfo.name, vesselInfo.vesselType);
    }
  }
  #endregion
}

}  // namespace
