// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Linq;
using UnityEngine;

namespace KSPDev.ModelUtils {

/// <summary>Various tools to deal with game object hierarchy.</summary>
public static class Hierarchy2 {
  /// <summary>Destroys the object in a way which is safe for physical callback methods.</summary>
  /// <remarks>
  /// The Unity <c>UnityEngine.Object.Destroy</c> method only marks object for deletion, but before
  /// the next fixed frame cycle completed, the object still can be found in the hierarchy. And it
  /// may trigger physics before the final cleanup. This method ensures that none of these
  /// side-effects happen and it <i>doesn't</i> use physics incompatible <c>DestroyImmediate</c>
  /// method.
  /// </remarks>
  public static void SafeDestory(Transform obj) {
    SafeDestory(obj.gameObject);
  }

  /// <inheritdoc cref="SafeDestory(Transform)"/>
  public static void SafeDestory(GameObject obj) {
    obj.transform.parent = null;
    obj.name = "$disposed";
    obj.SetActive(false);
    UnityEngine.Object.Destroy(obj);
  }
}

}  // namespace
