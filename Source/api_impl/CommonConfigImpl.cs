// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.ConfigUtils;

namespace KASImpl {

/// <summary>Container for the various global settings of the mod.</summary>
[PersistentFieldsDatabase("KAS/settings", "KASConfig")]
public class CommonConfigImpl : ICommonConfig {
  #region ICommonConfig implementation
  /// <inheritdoc/>
  public string sndPathBipWrong { get { return _sndPathBipWrong; } }

  /// <inheritdoc/>
  public string keyDropConnector { get { return _keyDropConnector; } }

  /// <inheritdoc/>
  public string keyPickupConnector { get { return _keyPickupConnector; } }
  #endregion

  [PersistentField("Sounds/bipWrong")]
  string _sndPathBipWrong = "";

  [PersistentField("Winch/dropConnectorKey")]
  string _keyDropConnector = "Y";

  [PersistentField("Winch/pickupConnectorKey")]
  string _keyPickupConnector = "Y";

  internal CommonConfigImpl() {
    ConfigAccessor.ReadFieldsInType(GetType(), this);
  }
}
  
}  // namespace
