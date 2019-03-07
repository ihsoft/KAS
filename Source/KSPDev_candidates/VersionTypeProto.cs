// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.ConfigUtils {

/// <summary>A proto for handling system <see cref="Version"/> type.</summary>
public class VersionTypeProto : AbstractOrdinaryValueTypeProto {
  /// <inheritdoc/>
  public override bool CanHandle(Type type) {
    return type == typeof(Version);
  }

  /// <inheritdoc/>
  public override string SerializeToString(object value) {
    return value.ToString();
  }

  /// <inheritdoc/>
  public override object ParseFromString(string value, Type type) {
    return new Version(value);
  }
}

}  // namespace
