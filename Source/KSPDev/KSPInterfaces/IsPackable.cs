// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.KSPInterfaces {

/// <summary>Interface to track physics state changes in the part's module.</summary>
public interface IsPackable {
  /// <summary>Triggers when physics starts on the part.</summary>
  void OnPartUnpack();
  /// <summary>Triggers when physics stops on the part.</summary>
  void OnPartPack();
}

}  // namespace
