// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;
using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KAS {

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
  const string DialogTitle = "Part adjustment tool";
  
  /// <summary>Actual screen position of the console window.</summary>
  static Rect windowRect = new Rect(100, 100, 400, 1);

  /// <summary>A title bar location.</summary>
  static Rect titleBarRect = new Rect(0, 0, 10000, 20);

  /// <summary>A list of actions to apply at the end of the GUI frame.</summary>
  static readonly GuiActionsList guiActions = new GuiActionsList();

  /// <summary>Keyboard event that opens/closes the remote GUI.</summary>
  static Event openGUIEvent;

  /// <summary>Tells if GUI is open.</summary>
  bool isGUIOpen;

  /// <summary>The part being adjusted.</summary>
  Part parentPart;

  /// <summary>Tells if the parent part capture mode is enabled.</summary>
  bool parentPartTracking;

  /// <summary>The part modules to adjust.</summary>
  List<KeyValuePair<string, IDebugAdjustable[]>> adjustableModules =
      new List<KeyValuePair<string, IDebugAdjustable[]>>();
  #endregion

  #region GUI styles & contents
  GUIStyle guiNoWrapStyle;
  GUIStyle guiCaptionStyle;
  GUIStyle guiValueStyle;
  #endregion

  #region IHasGUI implementation
  /// <inheritdoc/>
  public void OnGUI() {
    if (openGUIEvent != null && Event.current.Equals(openGUIEvent)) {
      Event.current.Use();
      isGUIOpen = !isGUIOpen;
    }
    if (isGUIOpen) {
      windowRect = GUILayout.Window(
          GetInstanceID(), windowRect, ConsoleWindowFunc, DialogTitle,
          GUILayout.MaxHeight(1), GUILayout.MinWidth(300));
    }
  }
  #endregion

  #region MonoBehavour methods
  void Awake() {
    ConfigAccessor.ReadFieldsInType(GetType(), instance: this);
    if (!string.IsNullOrEmpty(openGUIKey)) {
      DebugEx.Info("ControllerPartEditorTool controller created");
      openGUIEvent = Event.KeyboardEvent(openGUIKey);
    }
  }
  #endregion

  /// <summary>Shows a window that displays the winch controls.</summary>
  /// <param name="windowId">Window ID.</param>
  void ConsoleWindowFunc(int windowId) {
    MakeGuiStyles();

    if (guiActions.ExecutePendingGuiActions()) {
      if (parentPartTracking) {
        SetPart(Mouse.HoveredPart);
      }
      if (parentPartTracking && Input.GetMouseButtonDown(0)) {
        parentPartTracking = false;
      }
    }

    using (new GuiEnabledStateScope(!parentPartTracking)) {
      if (GUILayout.Button("Set part")) {
        guiActions.Add(() => parentPartTracking = true);
      }
    }
    using (new GuiEnabledStateScope(parentPartTracking)) {
      if (GUILayout.Button("Cancel set mode...")) {
        guiActions.Add(() => parentPartTracking = false);
      }
    }
    var parentPartName =
        "Part: " + (parentPart != null ? DbgFormatter.PartId(parentPart) : "NONE");
    GUILayout.Label(parentPartName, guiNoWrapStyle);

    // Render the adjustable fields.
    foreach (var pair in adjustableModules) {
      using (new GUILayout.VerticalScope(GUI.skin.box)) {
        GUILayout.Label("Adjustable: " + pair.Key);
        foreach (var adjustable in pair.Value) {
          adjustable.RenderGUI(guiActions,
                               guiCaptionStyle, new GUILayoutOption[0],
                               guiValueStyle, new[] {GUILayout.Width(100)});
        }
      }
    }

    if (GUILayout.Button("Close", GUILayout.ExpandWidth(false))) {
      guiActions.Add(() => isGUIOpen = false);
    }

    // Allow the window to be dragged by its title bar.
    GuiWindow.DragWindow(ref windowRect, titleBarRect);
  }

  /// <summary>Sets the part to be adjusted.</summary>
  /// <param name="part">The part to set.</param>
  void SetPart(Part part) {
    parentPart = part;
    if (part != null) {
      adjustableModules = part.Modules
          .OfType<IDebugAdjustable>()
          .Where(m => m.GetType().AssemblyQualifiedName.StartsWith("KAS."))
          .GroupBy(
              a => (a as PartModule).moduleName,
              a => a,
              (k, g) => new KeyValuePair<string, IDebugAdjustable[]>(k, g.ToArray()))
          .OrderBy(x => x.Key)
          .ToList();
    } else {
      adjustableModules.Clear();
    }
  }

  /// <summary>Creates the styles. Only does it once.</summary>
  void MakeGuiStyles() {
    if (guiNoWrapStyle == null) {
      guiNoWrapStyle = new GUIStyle(GUI.skin.box) {
        wordWrap = false,
      };
      guiCaptionStyle = new GUIStyle(GUI.skin.label) {
        wordWrap = false,
        alignment = TextAnchor.MiddleLeft,
        padding = GUI.skin.textField.padding,
        margin = GUI.skin.textField.margin,
        border = GUI.skin.textField.border,
      };
      guiValueStyle = new GUIStyle(GUI.skin.label) {
        padding = new RectOffset(0, 0, 0, 0),
        margin = new RectOffset(0, 0, 0, 0),
        border = new RectOffset(0, 0, 0, 0),
        alignment = TextAnchor.MiddleRight,
      };
    }
  }
}

}  // namespace
