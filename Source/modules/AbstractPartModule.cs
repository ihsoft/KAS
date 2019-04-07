// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;
using KSPDev.DebugUtils;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using System;
using System.Collections.Generic;

namespace KAS {

/// <summary>Base class for the KAS modules.</summary>
/// <remarks>
/// This module implements common logic to deal with part's configuration, persistence and
/// localization.
/// <para>
/// The descendants of this module can use the custom persistent fields of groups:
/// </para>
/// <list type="bullet">
/// <item><c>StdPersistentGroups.PartConfigLoadGroup</c></item>
/// <item><c>StdPersistentGroups.PartPersistant</c></item>
/// </list>
/// </remarks>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.PersistentFieldAttribute']/*"/>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.StdPersistentGroups']/*"/>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.IsLocalizableModule']/*"/>
public abstract class AbstractPartModule : PartModule,
    // KSPDev interfaces
    IsLocalizableModule, IHasDebugAdjustables,
    // KSPDev syntax sugar interfaces.
    IPartModule, IsDestroyable {

  #region IHasDebugAdjustables implementation
  /// <inheritdoc/>
  public virtual void OnBeforeDebugAdjustablesUpdate() {
  }

  /// <inheritdoc/>
  public virtual void OnDebugAdjustablesUpdated() {
    InitModuleSettings();
  }
  #endregion

  #region Local fields
  /// <summary>Tells if <see cref="InitModuleSettings"/> was called on the part.</summary>
  bool moduleSettingsLoaded;

  /// <summary>List of events to call to cleanup registered game event listeners.</summary>
  /// <remarks>They are called from the destroy method.</remarks>
  readonly List<Action> unregisterListenerActions = new List<Action>();
  #endregion

  #region IsLocalizableModule implementation
  /// <inheritdoc/>
  public virtual void LocalizeModule() {
    LocalizationLoader.LoadItemsInModule(this);
  }
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    ConfigAccessor.CopyPartConfigFromPrefab(this);
    base.OnAwake();
    LocalizeModule();
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    ConfigAccessor.ReadPartConfig(this, cfgNode: node);
    ConfigAccessor.ReadFieldsFromNode(node, GetType(), this, StdPersistentGroups.PartPersistant);
    base.OnLoad(node);
    if (!moduleSettingsLoaded) {
      moduleSettingsLoaded = true;
      InitModuleSettings();
    }
  }

  /// <inheritdoc/>
  public override void OnStart(StartState state) {
    base.OnStart(state);
    if (!moduleSettingsLoaded) {
      moduleSettingsLoaded = true;
      if (!HighLogic.LoadedSceneIsEditor) {
        HostedDebugLog.Fine(this, "Late load of module settings. Save file inconsistency?");
      }
      InitModuleSettings();
    }
  }

  /// <inheritdoc/>
  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    ConfigAccessor.WriteFieldsIntoNode(node, GetType(), this, StdPersistentGroups.PartPersistant);
  }
  #endregion

  #region IsDestroyable implementation
  /// <inheritdoc/>
  public virtual void OnDestroy() {
    unregisterListenerActions.ForEach(x => x.Invoke());
  }
  #endregion

  #region New inheritable methods
  /// <summary>Verifies that all part's settings are consistent.</summary>
  /// <remarks>
  /// If there are contradicting settings detected, they must be fixed so that the part could behave
  /// consistently. A warning must be logged to point out what was fixed and to what value.
  /// <para>
  /// Implementations may call this method multiple times at different stages. At the very least it
  /// get called on the module load, but this must <i>not</i> be assumed the only use-case.
  /// </para>
  /// </remarks>
  /// <seealso cref="InitModuleSettings"/>
  protected virtual void CheckSettingsConsistency() {
  }

  /// <summary>Shows a UI messages with regard to the currently active vessel.</summary>
  /// <remarks>
  /// The UI messages from the active vessel are shown at the highest priority to bring attention
  /// of the player. The messages from the inactive vessels are shown only as a status, that is not
  /// intended to distract the player from the current vessel operations.
  /// </remarks>
  /// <param name="msg">The message to show.</param>
  /// <param name="isError">
  /// Tells if the messages is an error condition report. Such messages will be highlighed with the
  /// color.
  /// </param>
  protected void ShowStatusMessage(string msg, bool isError = false) {
    if (FlightGlobals.ActiveVessel != vessel) {
      msg = string.Format("[{0}]: {1}", vessel.vesselName, msg);
    }
    if (isError) {
      msg = ScreenMessaging.SetColorToRichText(msg, ScreenMessaging.ErrorColor);
    }
    var duration = isError
        ? ScreenMessaging.DefaultErrorTimeout
        : ScreenMessaging.DefaultMessageTimeout;
    var location = FlightGlobals.ActiveVessel == vessel
        ? ScreenMessageStyle.UPPER_CENTER
        : (isError ? ScreenMessageStyle.UPPER_RIGHT : ScreenMessageStyle.UPPER_LEFT);
    ScreenMessages.PostScreenMessage(msg, duration, location);
  }

  /// <summary>Initializes the module state according to the settings.</summary>
  /// <remarks>
  /// This method is normally called from <c>OnLoad</c> method, when all the part components are
  /// created, but some of them may be not initialized yet. Under some circumstances it can be
  /// called from the <c>OnStart</c> method (e.g. in the editor or when loading an inconsistent save
  /// file).
  /// <para>
  /// This method is a good place for the module to become aware of the other part modules, but it's
  /// not the right place to deal with the other module settings.
  /// </para>
  /// <para>
  /// This method can be called multiple times in the part's life time, so keep this method
  /// ideponent. Repetative calls to this method should not break the part's logic.
  /// </para>
  /// </remarks>
  protected virtual void InitModuleSettings() {
    CheckSettingsConsistency();
  }

  /// <summary>Registers a game event listenr and cleans it up on module destruction.</summary>
  /// <param name="eventData">The game event to register for.</param>
  /// <param name="listener">The event listener.</param>
  protected void RegisterGameEventListener<T>(
      EventData<T> eventData, EventData<T>.OnEvent listener) {
    eventData.Add(listener);
    unregisterListenerActions.Add(() => eventData.Remove(listener));
  }
  #endregion
}

}  // namespace
