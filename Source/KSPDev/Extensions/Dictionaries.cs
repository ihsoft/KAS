// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Collections.Generic;

namespace KSPDev.Extensions {

/// <summary>Helper extensions for generic dictionary container.</summary>
public static class Dictionaries {
  /// <summary>
  /// Returns a value from dictionary by the key. If key is not defined yet then a new default entry
  /// is created and returned.
  /// </summary>
  /// <example>
  /// It's most useful when dealing with dictionaries of a complex type:
  /// <code><![CDATA[
  /// var a = new Dictionary<int, HashSet<string>>();
  /// // An empty string set for key 1 is created, and "abc" is added in it.
  /// a.SetDefault(1).Add("abc");
  /// // "def" is added into existing string set at key 1. 
  /// a.SetDefault(1).Add("def");
  /// ]]></code>
  /// </example>
  /// <param name="dict">Dictionary to get value from.</param>
  /// <param name="key">Key to lookup.</param>
  /// <typeparam name="K">Type of the dictionary key.</typeparam>
  /// <typeparam name="V">Type of the dictionary value.</typeparam>
  /// <returns>Either an existing value for the key or a default instance of the value.</returns>
  public static V SetDefault<K, V>(this Dictionary<K, V> dict, K key) where V : new() {
    V value;
    if (dict.TryGetValue(key, out value)) {
      return value;
    }
    value = new V();
    dict.Add(key, value);
    return value;
  }
}

}  // namespace
