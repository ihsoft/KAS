using System;
using UnityEngine;

namespace KSPDev.LogUtils {

/// <summary>
/// Merge with KSPDev.LogUtils.DbgFormatter
/// </summary>
public static class DbgFormatter2 {
  /// <summary>Returns string represenation of a vector with more precision.</summary>
  /// <param name="vec">Vector to dump.</param>
  /// <returns>String representation.</returns>
  public static string Vector(Vector3 vec) {
    return string.Format("({0:0.0###}, {1:0.0###}, {2:0.0###})", vec.x, vec.y, vec.z);
  }
}
  
}  // namespace
