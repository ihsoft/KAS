// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Collections.Generic;

namespace KSPDev.Extensions {

public static class Dictionaries {
  public static V SetDefault<K, V>(this Dictionary<K, V> dict, K key) where V : new() {
    V value;
    if (dict.ContainsKey(key)) {
      value = dict[key];
    } else {
      value = new V();
      dict.Add(key, value);
    }
    return value;
  }
}

}  // namespace
