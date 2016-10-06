// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.KSPInterfaces {

public interface IsDestroyable {
  /// <summary>Triggers when Unity object is about to destroy.</summary>
  void OnDestroy();
}

}  // namespace