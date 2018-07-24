// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {

/// <summary>Container for the various global settings of the mod.</summary>
public interface ICommonConfig {
  /// <summary>URL of the sound for the impossible action.</summary>
  /// <value>An empty string or a path to the sounds resource.</value>
  string sndPathBipWrong { get; }

  /// <summary>Keyboard key to trigger the drop connector event.</summary>
  /// <value>The Unity coded keyboard event string.</value>
  /// <example><code source="Examples/ICommonConfig-Examples.cs" region="ShortcutsDemo"/></example>
  string keyDropConnector { get; }

  /// <summary>Keyboard key to trigger the pickup connector event.</summary>
  /// <value>The Unity coded keyboard event string.</value>
  /// <example><code source="Examples/ICommonConfig-Examples.cs" region="ShortcutsDemo"/></example>
  string keyPickupConnector { get; }
}
  
}  // namespace
