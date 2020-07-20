// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.
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
  /// <param name="obj">The object to destroy. Can be <c>null</c>.</param>
  public static void SafeDestroy(Transform obj) {
    if (obj != null) {
      SafeDestroy(obj.gameObject);
    }
  }

  /// <inheritdoc cref="SafeDestroy(Transform)"/>
  public static void SafeDestroy(GameObject obj) {
    if (obj != null) {
      obj.transform.SetParent(null, worldPositionStays: false);
      obj.name = "$disposed";
      obj.SetActive(false);
      UnityEngine.Object.Destroy(obj);
    }
  }

  /// <inheritdoc cref="SafeDestroy(Transform)"/>
  public static void SafeDestroy(Component obj) {
    if (obj != null) {
      SafeDestroy(obj.gameObject);
    }
  }
}

}  // namespace
