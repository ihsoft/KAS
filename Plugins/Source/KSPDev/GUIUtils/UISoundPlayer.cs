// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KSPDev.GUIUtils {

[KSPAddon(KSPAddon.Startup.EveryScene, false /*once*/)]
public sealed class UISoundPlayer : MonoBehaviour {
  /// <summary>Returns instance for the current scene.</summary>
  public static UISoundPlayer instance;

  /// <summary>Global scene cache for all the sounds.</summary>
  static readonly Dictionary<string, AudioSource> audioCache =
      new Dictionary<string, AudioSource>();

  /// <summary>Plays the specified sound.</summary>
  /// <remarks>Every request is cached unless requested otherwise. Subsequent calls to the play
  /// method won't require audio clip loading.</remarks>
  /// <param name="soundPath"></param>
  /// <param name="dontCache">If <c>true</c> then audio will not be cached.</param>
  public void Play(string soundPath, bool dontCache = false) {
    if (!audioCache.ContainsKey(soundPath)) {
      LoadAndPlayAudio(soundPath, dontCache: dontCache, dontPlay: false);
      return;
    }
    audioCache[soundPath].Play();
  }

  /// <summary>Loads the sound into cache but doesn't play it.</summary>
  /// <remarks>Use this method when sound is expected to frequently played in the scene. If it worth
  /// spending a bit more time in the loading to win some latency during the play time then it
  /// pre-caching sounds is a good idea.</remarks>
  /// <param name="audioPath">File path relative to <c>GameData</c>.</param>
  public void CacheSound(string audioPath) {
    if (!audioCache.ContainsKey(audioPath)) {
      LoadAndPlayAudio(audioPath, dontCache: false, dontPlay: true);
    }
  }

  /// <summary>Initializes <see cref="instance"/>.</summary>
  /// <remarks>Overridden from <see cref="MonoBehaviour"/></remarks>
  void Awake() {
    instance = this;
    audioCache.Clear();
  }

  /// <summary>Loads audio sample and plays it.</summary>
  /// <param name="audioPath">File path relative to <c>GameData</c>.</param>
  /// <param name="dontCache">If <c>true</c> then audio will not be cached.</param>
  /// <param name="dontPlay">If <c>true</c> then audio will not be played.</param>
  void LoadAndPlayAudio(string audioPath, bool dontCache, bool dontPlay) {
    if (!GameDatabase.Instance.ExistsAudioClip(audioPath)) {
      Debug.LogErrorFormat("Cannot locate audio clip: {0}", audioPath);
      return;
    }
    Debug.LogFormat("Loading sound audio clip: {0}", audioPath);
    var audio = gameObject.AddComponent<AudioSource>();
    audio.volume = GameSettings.UI_VOLUME;
    audio.spatialBlend = 0;  //set as 2D audiosource
    audio.clip = GameDatabase.Instance.GetAudioClip(audioPath);
    if (!dontCache) {
      audioCache[audioPath] = audio;
    }
    audio.Play();
  }
}

}  // namespace