// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {

/// <summary>
/// An interface to notify a module on the part if there is a <i>public</i> KAS property changed in
/// some other module.
/// </summary>
/// <remarks>
/// <para>
/// The "public" property as a property which is visible to everyone and has a setter. It's up to
/// the module implementation to decide if the notification should be sent.
/// </para>
/// <para>
/// The event is sent blindly to all the modules on the part. The listeners must implement own
/// logic to filter the incoming notifications.
/// </para>
/// </remarks>
public interface IKasPropertyChangeListener {
  /// <summary>Notifies that the property change is about to happen.</summary>
  /// <param name="module">The module which property value has changed.</param>
  /// <param name="name">
  /// The name of the property. It isn't required to be the real name of the property in the code.
  /// </param>
  /// <param name="oldValue">The current value of the prioperty.</param>
  /// <param name="newValue">
  /// The new value which will be set once the notification is handled.
  /// </param>
  void OnKASPropertyChanged(PartModule module, string name, object oldValue, object newValue);
}

}  // namespace
