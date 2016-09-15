// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.KSPInterfaces {

public interface IKSPDevModuleInfo {
  /// <summary>Returns description for the editor part's browser.</summary>
  /// <remarks>Declared as virtual is <see cref="PartModule"/> and, hence, needs to be overridden.
  /// Though, it's also a part of <see cref="IModuleInfo"/>.</remarks>
  /// <returns>HTML formatted text to show the in GUI.</returns>
  /// FIXME: move strings to constants.
  /// FIXME: is it HTML?
  string GetInfo();
  
  /// <summary>Returns module title to show in the editor part's details panel.</summary>
  /// <returns>Title of the module.</returns>
  string GetModuleTitle();

  /// <summary>Unused.</summary>
  /// <returns>Always <c>null</c>.</returns>
  Callback<UnityEngine.Rect> GetDrawModulePanelCallback();

  /// <summary>Unused.</summary>
  /// <returns>Always <c>null</c>.</returns>
  string GetPrimaryField();
}

}
