// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using UnityEngine;
using KSPDev.GUIUtils;

namespace KSPDev.GUIUtils {

/// <summary>Interface for the modules that can render debug GUI.</summary>
/// <remarks>
/// If a module implements this interface, then it can present and allow changing some of the
/// internal settings. Any code that is interested in this ability can invoke the interface methods.
/// </remarks>
public interface IDebugAdjustable {
  /// <summary>
  /// Render a blcok of GUI controls to show/change the module's internal state.
  /// </summary>
  /// <param name="actionsList">The action list to add handler into.</param>
  /// <param name="captionStyle">The GUI style of the caption.</param>
  /// <param name="captionLayouts">The GUI layout optiosn for the caption.</param>
  /// <param name="valueStyle">The GUI style for the value(s).</param>
  /// <param name="valueLayouts">The GUI layout optiosn for the value(s).</param>
  void RenderGUI(GuiActionsList actionsList,
                 GUIStyle captionStyle, GUILayoutOption[] captionLayouts,
                 GUIStyle valueStyle, GUILayoutOption[] valueLayouts);
}

}  // namespace
