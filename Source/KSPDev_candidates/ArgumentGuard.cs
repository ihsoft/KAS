// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using KSPDev.LogUtils;
using System;
using System.Collections;
using UnityEngine;

namespace KSPDev.ProcessingUtils {

/// <summary>Set of tools to check the API method arguments.</summary>
/// <remarks>
/// The public API method <i>can</i> be called with an appropriate arguments. It's always a good
/// idea to fail earlier with a clear message rather than trying to use the wrong value and crash.
/// Always check the arguments for the accepted values. The only good reason to break this rule is
/// providing a high frequency API method.
/// </remarks>
public static class ArgumentGuard {
  /// <summary>Throws if argument is null.</summary>
  /// <param name="arg">The argument value to check.</param>
  /// <param name="argName">The argument name.</param>
  /// <param name="message">An optional message to preprsent in the error.</param>
  /// <param name="context">
  /// The context of the check. It can be anything, buit if it's one of the supported <i>hosts</i>
  /// in the <see cref="HostedDebugLog.Error(Part, string, object[])"/> method, then it will be
  /// nicely reported.
  /// </param>
  /// <exception cref="ArgumentNullException">If the argument is <c>null</c>.</exception>
  public static void NotNull(object arg, string argName,
                             string message = null, object context = null) {
    if (arg == null) {
      LogContextError(context, "Argument '{0}' is NULL: {1}", argName, message);
      if (message == null) {
        throw new ArgumentNullException(argName);
      }
      throw new ArgumentNullException(argName, message);
    }
  }

  /// <summary>Throws if string is null.</summary>
  /// <param name="arg">The argument value to check.</param>
  /// <param name="argName">The argument name.</param>
  /// <param name="message">An optional message to preprsent in the error.</param>
  /// <param name="context">
  /// The context of the check. It can be anything, buit if it's one of the supported <i>hosts</i>
  /// in the <see cref="HostedDebugLog.Error(Part, string, object[])"/> method, then it will be
  /// nicely reported.
  /// </param>
  /// <exception cref="ArgumentNullException">If the argument is <c>null</c>.</exception>
  /// <exception cref="ArgumentException">If the argument is an empty string.</exception>
  public static void NotNullOrEmpty(string arg, string argName,
                                    string message = null, object context = null) {
    NotNull(arg, argName, message: message, context: context);
    if (arg == "") {
      message = string.Format("Argument '{0}' is EMPTY: {1}", argName, message);
      LogContextError(context, message);
      throw new ArgumentException(argName, message);
    }
  }

  /// <summary>Throws if ordinary value is beyond the bounds.</summary>
  /// <param name="arg">The argument value to check.</param>
  /// <param name="argName">The argument name.</param>
  /// <param name="minValue">The minumum allowed value.</param>
  /// <param name="maxValue">The maximum allowed value.</param>
  /// <param name="message">An optional message to present in case of the error.</param>
  /// <param name="context">
  /// The context of the check. It can be anything, buit if it's one of the supported <i>hosts</i>
  /// in the <see cref="HostedDebugLog.Error(Part, string, object[])"/> method, then it will be
  /// nicely reported.
  /// </param>
  /// <exception cref="ArgumentOutOfRangeException">If the argument is an empty string.</exception>
  public static void InRange<T>(
      T arg, string argName, T minValue, T maxValue,
      string message = null, object context = null) where T : IComparable {
    if (arg.CompareTo(minValue) < 0 || arg.CompareTo(maxValue) > 0) {
      LogContextError(context, "Argument '{0}' not in range: min={1}, max={2}",
                      argName, minValue, maxValue);
      throw new ArgumentOutOfRangeException(
          argName, arg, string.Format("Allowed range is [{0}; {1}]", minValue, maxValue));
    }
  }

  #region Local utility methods
  static void LogContextError(object context, string message, params object[] args) {
    if (context is Part) {
      HostedDebugLog.Error(context as Part, message, args);
    } else if (context is PartModule) {
      HostedDebugLog.Error(context as PartModule, message, args);
    } else if (context is Transform) {
      HostedDebugLog.Error(context as Transform, message, args);
    } else {
      DebugEx.Error("[CONTEXT:{0}] {1}", context, string.Format(message, args));
    }
  }
  #endregion
}

}  // namespace
