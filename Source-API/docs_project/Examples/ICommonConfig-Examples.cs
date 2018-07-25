// Kerbal Attachment System - Examples
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.LogUtils;
using UnityEngine;

namespace Examples {

public class ICommonConfigExample1 {
  #region ShortcutsDemo
  public void ShortcutsDemo() {
    var simpleY = Event.KeyboardEvent("Y");
    var alt_Y = Event.KeyboardEvent("&Y");
    var shift_Y = Event.KeyboardEvent("#Y");
    var ctrl_Y = Event.KeyboardEvent("$Y");
    var ctrl2_Y = Event.KeyboardEvent("^Y");  // Yup! It's CTRL too.
    var numpad0 = Event.KeyboardEvent("[0]");
    var just0 = Event.KeyboardEvent("0");

    // And here is how it's checked in the OnGUI() method.
    if (Event.current.Equals(numpad0)) {
      DebugEx.Info("You've pressed the numpad 0 key");
    }
  }
  #endregion
}

};  // namespace
