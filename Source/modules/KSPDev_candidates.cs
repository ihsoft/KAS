// This is an intermediate module for methods and classes that are considred as candidates for
// KSPDev Utilities. Ideally, this module is always empty but there may be short period of time
// when new functionality lives here and not in KSPDev.

namespace KSPDev.KSPInterfaces {

/// <summary>Documented analogue of KSP IJointLockState interface.</summary>
/// <remarks>
/// Inherit from <see cref="IJointLockState"/> to be able reporting unlocked joints in a vessel.
/// <see cref="IKSPDevJointLockState"/> is a full equivalent except it's documented. Inheriting
/// modules from both interfaces gives better code documentation.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// public class MyModule : PartModule, IJointLockState, IKSPDevJointLockState {
///   /// <inheritdoc/>
///   public override bool IsJointUnlocked() {
///     return true;
///   }
/// }
/// ]]></code>
/// </example>
public interface IKSPDevJointLockState {
  /// <summary>Tells if the parts can move relative to each other.</summary>
  /// <remarks>
  /// It's important to override this method when the joint is not rigid. For the rigid joints the
  /// game may create autostruts when appropriate which will adhere the parts to each other.
  /// <para>This method is called on the child part to check it's joint state to the parent.</para>
  /// </remarks>
  /// <returns><c>true</c> if the joint are not fixed relative to each other.</returns>
  bool IsJointUnlocked();
}

}  // namespace
