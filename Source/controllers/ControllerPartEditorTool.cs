// Kerbal Attachment System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;
using KSPDev.DebugUtils;
using KSPDev.GUIUtils;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KAS.Debug {

/// <summary>
/// Dialog for adjusting parts.
/// </summary>
[KSPAddon(KSPAddon.Startup.FlightAndEditor, false /*once*/)]
[PersistentFieldsDatabase("KAS/settings/KASConfig")]
internal sealed class ControllerPartEditorTool : MonoBehaviour,
    // KSPDev interfaces
    IHasGUI {

  #region Configuration settings
  /// <summary>Keyboard key to trigger the GUI.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [PersistentField("Debug/partAlignToolKey")]
  public string openGuiKey = "";
  #endregion

  #region Local fields
  /// <summary>GUI dialog's title.</summary>
  const string DialogTitle = "KAS part adjustment tool";

  /// <summary>Dialogs instance. There must be only one in the game.</summary>
  static PartDebugAdjustmentDialog _dlg;

  /// <summary>Keyboard event that opens/closes the remote GUI.</summary>
  static Event _openGuiEvent;
  #endregion

  #region IHasGUI implementation
  /// <inheritdoc/>
  public void OnGUI() {
    if (_openGuiEvent == null || !Event.current.Equals(_openGuiEvent)) {
      return;
    }
    Event.current.Use();
    if (_dlg == null) {
      _dlg = DebugGui.MakePartDebugDialog(
          DialogTitle, group: KASDebugAdjustableAttribute.DebugGroup);
    } else {
      DebugGui.DestroyPartDebugDialog(_dlg);
      _dlg = null;
    }
  }
  #endregion

  #region MonoBehavour methods
  void Awake() {
    ConfigAccessor.ReadFieldsInType(GetType(), instance: this);
    if (!string.IsNullOrEmpty(openGuiKey)) {
      _openGuiEvent = Event.KeyboardEvent(openGuiKey);
    }
  }
  #endregion
}

}  // namespace
