// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KSPDev.GUIUtils {

/// <summary>A simple wrapper around the GUI tooltip.</summary>
/// <remarks>It tracks changes of the current tooltip and emits a <c>GUILayout.Label</c> when appropriate.</remarks>
public class GuiTooltip {

  /// <summary>Tells even an empty tooltip control still have to be rendered.</summary>
  readonly bool _alwaysEmitTheControl;

  /// <summary>A delay for the tooltip control to hide when empty.</summary>
  /// <seealso cref="_alwaysEmitTheControl"/>
  readonly float _hideDelay;

  /// <summary>The tooltip, captured in the last frame.</summary>
  /// <remarks>It always behind one frame!</remarks>
  string _tooltip = "";

  /// <summary>The last time a non-empty tooltip was spotted </summary>
  /// <remarks>Used to determine if the control should be hidden.</remarks>
  /// <seealso cref="_hideDelay"/>
  float _latestNonEmptyTooltipRecordedTs;

  /// <summary>A controller for the Unity GUI tooltip.</summary>
  /// <remarks>
  /// It gives a better control on how and when the GUI element tooltip is shown. The tooltip content is rendered as a
  /// <c>GUILayout.Label</c> with the style provided. The Unity GUI layout mechanism applies limitations on when the
  /// code can decide if the control should or should not be shown. This class handles all the requirements.
  /// </remarks>
  /// <param name="alwaysEmitTheControl">
  /// If set to <c>tru</c>, then an empty label will be presented even when there is no active tooltip in the frame. It
  /// may be useful for the fixed size layouts. 
  /// </param>
  /// <param name="hideDelay">
  /// Tells for how long the empty tooltip is to be show as an element until a new non-empty value is obtained. It helps
  /// to mitigate the UI flickering when the tooltip controls are located close to each other. The value is of the
  /// <c>seconds</c> unit. The sparse dialogs may want to set this value to zero. On the other hand, the compact dialogs
  /// with a lot of hinted elements may want to set some delay. As a rule of thumb, in UX a <c>200ms</c> delay is
  /// considered to be not noticeable by a regular human user.  
  /// </param>
  public GuiTooltip(bool alwaysEmitTheControl = false, float hideDelay = 0.2f) {
    _alwaysEmitTheControl = alwaysEmitTheControl;
    _hideDelay = hideDelay;
  } 

  /// <summary>Presents the tooltip control and updates the internal state</summary>
  /// <remarks>
  /// Must be called in every <c>OnGUI</c> call at the place where the tooltip content is supposed to be shown.
  /// </remarks>
  /// <param name="style">The style of the tooltip. If not provided, then <c>GUI.skin.label</c> will be used.</param>
  public void Update(GUIStyle style = null) {
    if (_tooltip != "" || _alwaysEmitTheControl || _latestNonEmptyTooltipRecordedTs + _hideDelay >= Time.time) {
      GUILayout.Label(_tooltip, style ?? GUI.skin.label);
    }
    if (Event.current.type == EventType.Repaint) {
      _tooltip = GUI.tooltip;
      if (_tooltip != "") {
        _latestNonEmptyTooltipRecordedTs = Time.time;
      }
    }
  }
}

}
