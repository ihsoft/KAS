// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KSPDev.GUIUtils {

/// <summary>Helper class to play sounds in game GUI. Such sounds are not 3D aligned.</summary>
/// <remarks>
/// Use this player when soucre of the sound is player keyboard actions or a mouse pointer. This
/// class implements all the boilerplate to load and play sound resources.
/// </remarks>
/// <example>
/// Here is an example of playing two different sounds on pressing "O" or "P" keys.
/// <code><![CDATA[
/// class MyModule : PartModule {
///   public override OnAwake() {
///     // We don't want to loose latency on "ooo.ogg" playing.
///     UISoundPlayer.instance.CacheSound("ooo.ogg");
///   }
///
///   public override OnUpdate() {
///     if (Input.GetKeyDown("O")) {
///       UISoundPlayer.instance.Play("ooo.ogg");  // Played from cache. No delay.
///     }
///     if (Input.GetKeyDown("P")) {
///       UISoundPlayer.instance.Play("ppp.ogg");  // May delay game while loading the resource.
///     }
///   }
/// }
/// ]]></code>
/// </example>
[KSPAddon(KSPAddon.Startup.EveryScene, false /*once*/)]
public sealed class UISoundPlayer : MonoBehaviour {
  /// <summary>Returns instance for the current scene.</summary>
  public static UISoundPlayer instance;

  /// <summary>Global scene cache for all the sounds.</summary>
  static readonly Dictionary<string, AudioSource> audioCache =
      new Dictionary<string, AudioSource>();

  /// <summary>Plays the specified sound.</summary>
  /// <remarks>
  /// Every request is cached unless requested otherwise. Subsequent calls to the play method won't
  /// require audio clip loading.
  /// </remarks>
  /// <param name="audioPath">File path relative to <c>GameData</c>.</param>
  /// <param name="dontCache">If <c>true</c> then audio will not be cached.</param>
  public void Play(string audioPath, bool dontCache = false) {
    var audio = GetOrLoadAudio(audioPath, dontCache);
    if (audio != null) {
      audio.Play();
    }
  }

  /// <summary>Loads the sound into cache but doesn't play it.</summary>
  /// <remarks>
  /// Use this method when sound is expected to frequently played in the scene. If it worth spending
  /// a bit more time in the loading to win some latency during the play time then it pre-caching
  /// sounds is a good idea.
  /// </remarks>
  /// <param name="audioPath">File path relative to <c>GameData</c>.</param>
  public void CacheSound(string audioPath) {
    GetOrLoadAudio(audioPath, dontCache: false);
  }

  /// <summary>Initializes <see cref="instance"/>.</summary>
  void Awake() {
    instance = this;
    audioCache.Clear();
  }

  /// <summary>Loads audio sample and plays it.</summary>
  /// <param name="audioPath">File path relative to <c>GameData</c>.</param>
  /// <param name="dontCache">If <c>true</c> then audio will not be cached.</param>
  /// <returns>Audio resource if loaded or found in the cache, otherwise <c>null</c>.</returns>
  AudioSource GetOrLoadAudio(string audioPath, bool dontCache) {
    if (HighLogic.LoadedScene == GameScenes.LOADING
        || HighLogic.LoadedScene == GameScenes.LOADINGBUFFER) {
      // Resources are not avaialble during game load. 
      return null;
    }
    AudioSource audio;
    if (audioCache.TryGetValue(audioPath, out audio)) {
      return audio;
    }
    if (!GameDatabase.Instance.ExistsAudioClip(audioPath)) {
      Debug.LogErrorFormat("Cannot locate audio clip: {0}", audioPath);
      return null;
    }
    Debug.LogFormat("Loading sound audio clip: {0}", audioPath);
    audio = gameObject.AddComponent<AudioSource>();
    audio.volume = GameSettings.UI_VOLUME;
    audio.spatialBlend = 0;  // Set as 2D audiosource
    audio.clip = GameDatabase.Instance.GetAudioClip(audioPath);
    if (!dontCache) {
      audioCache[audioPath] = audio;
    }
    return audio;
  }
}

}  // namespace