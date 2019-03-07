// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using KSPDev.LogUtils;
using System;

namespace KSPDev.ProcessingUtils {

/// <summary>Set of tools to check various preconditions during the code execution.</summary>
/// <remarks>
/// This class offers single line checkers for the case when a critical "go"/"no go" decision needs
/// to be made. Instead of flooding your code with <c>if/throw</c> conditions, simply call a
/// precondition check. If the condition met, then execution continues. Otherwise, an exception with
/// a clear message is thrown.
/// </remarks>
public static class Preconditions {
  /// <summary>Throws if value is null.</summary>
  /// <param name="arg">The value to check.</param>
  /// <param name="message">An optional message to present in the error.</param>
  /// <param name="context">The optional "owner" object.</param>
  /// <exception cref="NullReferenceException">If the argument is <c>null</c>.</exception>
  public static void NotNull(object arg, string message = null, object context = null) {
    if (arg == null) {
      throw new NullReferenceException(MakeContextError(context, message));
    }
  }

  /// <summary>Throws if string is null or empty.</summary>
  /// <param name="arg">The value to check.</param>
  /// <param name="message">An optional message to present in the error.</param>
  /// <param name="context">The optional "owner" object.</param>
  /// <exception cref="NullReferenceException">If the value is <c>null</c>.</exception>
  /// <exception cref="InvalidOperationException">If the value is an empty string.</exception>
  public static void NotNullOrEmpty(string arg, string message = null, object context = null) {
    NotNull(arg, message: message, context: context);
    if (arg == "") {
      message = message ?? "Value is an empty string";
      throw new InvalidOperationException(MakeContextError(context, message));
    }
  }

  /// <summary>Throws if value form a config node is null or empty string.</summary>
  /// <param name="arg">The value to check.</param>
  /// <param name="path">The path to the value or node.</param>
  /// <param name="context">The optional "owner" object.</param>
  /// <exception cref="NullReferenceException">If the value is <c>null</c>.</exception>
  /// <exception cref="InvalidOperationException">If the value is an empty string.</exception>
  public static void ConfValueExists(object arg, string path, object context = null) {
    if (arg is string) {
      NotNullOrEmpty(arg as string, message: "No value at '" + path + "'", context: context);
    } else {
      NotNull(arg, message: "No node value at '" + path + "'", context: context);
    }
  }

  /// <summary>Makes error text with respect to the context.</summary>
  /// <param name="context">
  /// The context or <c>null</c>. Context can be any string (keep it short) or an object.
  /// </param>
  /// <param name="message">The message to present.</param>
  /// <returns>The complete error string.</returns>
  public static string MakeContextError(object context, string message) {
    var res = "";
    if (context != null) {
      if (!string.IsNullOrEmpty(context as string)) {
        res = "[Context:" + context + "]";
      } else {
        res = DebugEx.ObjectToString(context).ToString();
      }
    }
    if (!string.IsNullOrEmpty(message)) {
      res += (res != "" ? " " : "") + message;
    }
    return res;
  }

  /// <summary>Makes error text with respect to the context.</summary>
  /// <param name="context">
  /// The context or <c>null</c>. Context can be any string (keep it short) or an object.
  /// </param>
  /// <param name="message">The message to present.</param>
  /// <param name="args">The arguments to format the message.</param>
  /// <returns>The complete error string.</returns>
  public static string MakeContextError(object context, string message, params object[] args) {
    return MakeContextError(context, string.Format(message, args));
  }
}

}  // namespace
