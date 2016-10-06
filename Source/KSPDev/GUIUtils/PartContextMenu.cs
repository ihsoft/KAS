// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Linq;

namespace KSPDev.GUIUtils {

public static class PartContextMenu {
  public static void InvalidateContextMenu(Part part) {
    var windows = UnityEngine.Object.FindObjectsOfType(typeof(UIPartActionWindow))
        .Cast<UIPartActionWindow>()
        .Where(w => w.part == part);
    foreach (var window in windows) {
      window.displayDirty = true;
    }
  }
}

}  // namespace
