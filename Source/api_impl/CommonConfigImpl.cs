// Kerbal Attachment System
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
using KSPDev.ConfigUtils;

// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable ConvertToConstant.Local
// ReSharper disable once CheckNamespace
namespace KASImpl {

/// <summary>Container for the various global settings of the mod.</summary>
[PersistentFieldsDatabase("KAS/settings/KASConfig", "")]
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
