// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.KSPInterfaces {

/// <summary>Declares callbacks that are called when a joint between two parts is changed.</summary>
public interface IJointEventsListener {
  /// <summary>Triggers when connection is broken due to too strong force applied.</summary>
  /// <param name="breakForce">Actual force that has been applied.</param>
  void OnJointBreak(float breakForce);
}

}  // namespace
