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
/// The "public" property is a property which visible to everyone and can change due to extrenal
/// calls or the internal logic. It's up to the implementation to decide if the notification should
/// be sent in case of changing a particular value. However, this notification mechanism should not
/// be used on the values that can change at the high rate.
/// </para>
/// <para>
/// The event is sent blindly to all the modules on the part. The listeners must implement own
/// logic to filter the incoming notifications.
/// </para>
/// </remarks>
public interface IKasPropertyChangeListener {
  /// <summary>Notifies that the property value has changed.</summary>
  /// <param name="owner">
  /// The owner of the property which value has changed. The type of this object must be the type
  /// that declares the property. And this type must be a descendant of <c>PartModule</c>.  
  /// </param>
  /// <param name="name">
  /// The name of the property. It isn't required to be the real name of the property in the code.
  /// However, it's highlty encouraged to keep the consistency.
  /// </param>
  void OnKASPropertyChanged(object owner, string name);
}

}  // namespace
