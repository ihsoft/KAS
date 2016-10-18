// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Collections;
using UnityEngine;

namespace KSPDev.ProcessingUtils {

/// <summary>Set of tools to execute a delayed code.</summary>
/// <remarks>
/// Use theese tools when code needs to be executed with some delay or at a specific moment of the
/// game.
/// <code><![CDATA[
/// class MyComponent : MonoBehaviour {
///   void MyAsyncMethod(string a, int b) {
///     Debug.DebugLogInfo("MyAsyncMethod({0}, {1})", a, b);
///   }
///   void Update() {
///     // Call method at the end of the current frame.
///     AsyncCall.CallOnEndOfFrame(this, x => MyAsyncMethod("a", 1));
///     // Call method after 5 seconds timeout.
///     AsyncCall.CallOnTimeout(this, 5.0f, x => MyAsyncMethod("b", 2));
///     // Call method on the next fixed update.
///     AsyncCall.CallOnFixedUpdate(this, x => MyAsyncMethod("c", 3));
///   }
/// }
/// ]]></code>
/// </remarks>
public static class AsyncCall {
  /// <summary>Delayed execution delegate.</summary>
  /// <param name="list">Optional list of parameters.</param>
  public delegate void Action0(params object[] list);

  /// <summary>Delays execution of the delegate till the end of the current frame.</summary>
  /// <remarks>
  /// Caller can continue executing its logic. The delegate will be called at the end of
  /// the frame via Unity StartCoroutine mechanism. The delegate will be called only once.
  /// </remarks>
  /// <param name="mono">
  /// Unity object to run coroutine on. If this object dies then the async call will not be invoked.
  /// </param>
  /// <param name="action">Delegate to execute.</param>
  /// <param name="args">Arguments to pass to the delegate.</param>
  /// <returns>Coroutine instance.</returns>
  /// <seealso href="https://docs.unity3d.com/Manual/Coroutines.html">Unity 3D: Coroutines</seealso>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/WaitForEndOfFrame.html">
  /// Unity 3D: WaitForEndOfFrame</seealso>
  public static Coroutine CallOnEndOfFrame(
      MonoBehaviour mono, Action0 action, params object[] args) {
    return mono.StartCoroutine(WaitForEndOfFrameCoroutine(action, args));
  }

  /// <summary>Delays execution of the delegate for the specified amount of time.</summary>
  /// <remarks>
  /// Caller can continue executing its logic. The delegate will be called once the timeout is
  /// expired via Unity StartCoroutine mechanism. The delegate will be called only once.
  /// <para>Using returned instance caller may cancel the call before the timeout expired.</para>
  /// </remarks>
  /// <param name="mono">
  /// Unity object to run coroutine on. If this object dies then the async call will not be invoked.
  /// </param>
  /// <param name="seconds">Timeout in seconds.</param>
  /// <param name="action">Delegate to execute.</param>
  /// <param name="args">Arguments to pass to the delegate.</param>
  /// <returns>Coroutine instance.</returns>
  /// <seealso href="https://docs.unity3d.com/Manual/Coroutines.html">Unity 3D: Coroutines</seealso>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/WaitForSeconds.html">
  /// Unity 3D: WaitForSeconds</seealso>
  public static Coroutine CallOnTimeout(
      MonoBehaviour mono, float seconds, Action0 action, params object[] args) {
    return mono.StartCoroutine(WaitForSecondsCoroutine(seconds, action, args));
  }

  /// <summary>Delays execution of the delegate till the next fixed update.</summary>
  /// <remarks>
  /// Caller can continue executing its logic. The delegate will be called at the beginning of the
  /// next fixed (physics) update via Unity StartCoroutine mechanism. The delegate will be called
  /// only once.
  /// </remarks>
  /// <param name="mono">
  /// Unity object to run coroutine on. If this object dies then the async call will not be invoked.
  /// </param>
  /// <param name="action">Delegate to execute.</param>
  /// <param name="args">Arguments to pass to the delegate.</param>
  /// <returns>Coroutine instance.</returns>
  /// <seealso href="https://docs.unity3d.com/Manual/Coroutines.html">Unity 3D: Coroutines</seealso>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/WaitForFixedUpdate.html">
  /// Unity 3D: WaitForFixedUpdate</seealso>
  public static Coroutine CallOnFixedUpdate(
      MonoBehaviour mono, Action0 action, params object[] args) {
    return mono.StartCoroutine(WaitForFixedUpdateCoroutine(action, args));
  }

  #region Coroutines
  static IEnumerator WaitForEndOfFrameCoroutine(Action0 action, params object[] list) {
    yield return new WaitForEndOfFrame();
    action(list);
  }

  static IEnumerator WaitForSecondsCoroutine(float seconds, Action0 action, params object[] list) {
    yield return new WaitForSeconds(seconds);
    action(list);
  }

  static IEnumerator WaitForFixedUpdateCoroutine(Action0 action, params object[] list) {
    yield return new WaitForFixedUpdate();
    action(list);
  }
  #endregion
}

}  // namespace
