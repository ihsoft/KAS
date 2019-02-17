// Kerbal Attachment System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;
using KSPDev.DebugUtils;
using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KAS.Debug {

/// <summary>
/// Dialog for adjusting parts.
/// </summary>
[KSPAddon(KSPAddon.Startup.FlightAndEditor, false /*once*/)]
[PersistentFieldsDatabase("KAS/settings/KASConfig")]
sealed class ControllerPartEditorTool : MonoBehaviour,
    // KSPDev interfaces
    IHasGUI {

  #region Configuration settings
  /// <summary>Keyboard key to trigger the GUI.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [PersistentField("Debug/partAlignToolKey")]
  public string openGUIKey = "";
  #endregion

  #region Local fields
  /// <summary>GUI dialog's title.</summary>
  const string DialogTitle = "KAS part adjustment tool";

  /// <summary>Dialogs instance. There must be only one in the game.</summary>
  static PartDebugAdjustmentDialog dlg;

  /// <summary>Keyboard event that opens/closes the remote GUI.</summary>
  static Event openGUIEvent;
  #endregion

  #region IHasGUI implementation
  /// <inheritdoc/>
  public void OnGUI() {
    if (openGUIEvent != null && Event.current.Equals(openGUIEvent)) {
      Event.current.Use();
      if (dlg == null) {
        dlg = DebugGui.MakePartDebugDialog(
            DialogTitle, group: Debug.KASDebugAdjustableAttribute.DebugGroup);
      } else {
        DebugGui.DestroyPartDebugDialog(dlg);
        dlg = null;
      }
    }
  }
  #endregion

  #region MonoBehavour methods
  void Awake() {
    ConfigAccessor.ReadFieldsInType(GetType(), instance: this);
    if (!string.IsNullOrEmpty(openGUIKey)) {
      openGUIEvent = Event.KeyboardEvent(openGUIKey);
    }
  }
  #endregion
}

}  // namespace
