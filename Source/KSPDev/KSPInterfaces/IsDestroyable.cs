// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.KSPInterfaces {

/// <summary>Interface for modules that need not now if script object is destroyed.</summary>
public interface IsDestroyable {
  /// <summary>Triggers when Unity object is about to destroy.</summary>
  void OnDestroy();
}

}  // namespace
