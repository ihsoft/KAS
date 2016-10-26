// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using UnityEngine;

// Name of the namespace denotes the API version. If new version is released the old one remains
// which let's legacy mods to work properly.
namespace KASAPIv1 {

/// <summary>Various tools to deal with KSP part joints.</summary>
public interface IJointUtils {
  /// <summary>Outputs all propertis of the joint to the string.</summary>
  /// <param name="joint">Joint to dump settings for.</param>
  /// <returns>Linefeed formatted block of text.</returns>
  string DumpJoint(ConfigurableJoint joint);

  /// <summary>Initializes joint to a consistent state.</summary>
  /// <remarks>
  /// It's not the same as creating a default joint. The state is consistent but different from the
  /// default:
  /// <list type="">
  /// <item>All linear and angular drive modes set to <see cref="ConfigurableJointMotion.Locked"/>.
  /// </item>
  /// <item>All drives, springs and limits are zeroed.</item>
  /// <item>Coordinate system is reset to local. Y looks up, and X looks right.</item>
  /// <item>
  /// Connected body is <i>not</i> touched. Connection, if any, won't be broken on the reset.
  /// </item>
  /// <item>
  /// Any state accumulated so far (e.g. relative rotation or position) will be lost, and joint will
  /// remember new relative rotation/position of the connected objects.
  /// </item>
  /// </list>
  /// <para>
  /// Use this method before setting up a new or existing joint. By resetting the joint you ensure
  /// it's in a consistent state, and further adjustments will always give the same result
  /// regardless to how the joint was created and what components were affecting it.
  /// </para>
  /// </remarks>
  /// <param name="joint">Joint to reset.</param>
  void ResetJoint(ConfigurableJoint joint);

  /// <summary>Sets up joint so what it becomes a prismatic joint.</summary>
  /// <remarks>
  /// It's a <see href="http://docs.nvidia.com/gameworks/content/gameworkslibrary/physx/guide/Manual/Joints.html#prismatic-joint">
  /// standard PhysX configuration</see>. Main axis is set to Z. Moving along it is allowed but can
  /// be constrained by a spring and limit. Drive mode is set to
  /// <see cref="JointDriveMode.Position"/>.
  /// <para>
  /// Only main axis linear settings are changed. Consider using <see cref="ResetJoint"/> to
  /// eliminate side effects from the previous settings of the joint.
  /// </para>
  /// <para>
  /// Pure prismatic joint assumes 5 out of the 6 degrees of freedom to be locked (everything,
  /// except the main axis linear motion). Consider setting <see cref="Joint.enablePreprocessing"/>
  /// to <c>true</c> since it may improve PhysXperformance.
  /// </para>
  /// <para>
  /// For performance reasons some parameters combindations may result in different motion modes:
  /// <list>
  /// <item>
  /// When <paramref name="springForce"/> is <c>Infinite</c> or <paramref name="distanceLimit"/> is
  /// <c>0</c> the main axis linear movement mode is set to
  /// <see cref="ConfigurableJointMotion.Locked"/>. If you plan to change force/limit don't forget
  /// to update the modes as well.
  /// </item>
  /// <item>
  /// When <paramref name="springForce"/> is <c>0</c> and <paramref name="distanceLimit"/> is
  /// <c>Infinite</c> the main axis linear movement mode is set to
  /// <see cref="ConfigurableJointMotion.Free"/>. If you plan to change either of the parameters
  /// don't forget to update the mode as well.
  /// </item>
  /// </list>
  /// </para>
  /// <para>
  /// Regardless to the modes set all the other parameters are also applied. I.e. you don't need to
  /// re-apply them when changing mode.
  /// </para>
  /// </remarks>
  /// <param name="joint">Joint to setup.</param>
  /// <param name="springForce">
  /// Force to apply per unit of linear stretching to return the joined bodies back to the original
  /// distance. Also, see remarks to the method.
  /// </param>
  /// <param name="springDamperRatio">
  /// Percentage of the spring force to use for dampering oscillation effect.
  /// </param>
  /// <param name="maxSpringForce">
  /// Maximum spring force to apply when joint distance deviates from the original.
  /// </param>
  /// <param name = "distanceLimit">
  /// Maximum allowed distance relative to the original value.  Also, see remarks to the method.
  /// </param>
  /// <param name = "distanceLimitForce">
  /// Force to apply to keep distance in limits. If it's <c>0</c> then the limit is impassible.
  /// </param>
  /// <param name = "distanceLimitDamperRatio">
  /// Percentage of the limit force to use for dampering oscillation effect.
  /// </param>
  void SetupPrismaticJoint(ConfigurableJoint joint,
                           float springForce = 0,
                           float springDamperRatio = 0.1f,
                           float maxSpringForce = Mathf.Infinity,
                           float distanceLimit = Mathf.Infinity,
                           float distanceLimitForce = 0,
                           float distanceLimitDamperRatio = 0.1f);

  /// <summary>Sets up joint so what it becomes a spherical hinge joint.</summary>
  /// <remarks>
  /// It's a <see href="http://docs.nvidia.com/gameworks/content/gameworkslibrary/physx/guide/Manual/Joints.html#spherical-joint">
  /// standard PhysiX configuration</see>. Main axis is set to Z, and angular rotation around it is
  /// completely unrestricted. Secondary axes are X&amp;Y can be restricted by applying spring force
  /// and/or limits. Drive mode is set to <see cref="JointDriveMode.Position"/>.
  /// <para>
  /// Only angular settings are set. If joint had linear constraints defined they will stay
  /// unchanged. Consider using <see cref="ResetJoint"/> to eliminate side effects from the previous
  /// settings of the joint.
  /// </para>
  /// <para>
  /// Pure spherical joint assumes 3 out of the 6 degrees of freedom to be locked (all the three
  /// axes linear motions). Consider setting <see cref="Joint.enablePreprocessing"/> to <c>true</c>
  /// since it may improve PhysX performance.
  /// </para>
  /// <para>
  /// For performance reasons some parameters combindations may result in different angular modes:
  /// <list>
  /// <item>
  /// When <paramref name="springForce"/> is <c>Infinite</c> or <paramref name="angleLimit"/> is
  /// <c>0</c> Y&amp;Z rotation modes are set to <see cref="ConfigurableJointMotion.Locked"/>. If
  /// you plan to change force/limit don't forget to update the modes as well.
  /// </item>
  /// <item>
  /// When <paramref name="springForce"/> is <c>0</c> and <paramref name="angleLimit"/> is
  /// <c>Infinite</c> Y&amp;Z rotation modes are set to <see cref="ConfigurableJointMotion.Free"/>.
  /// If you plan to change either of the parameters don't forget to update the modes as well.
  /// </item>
  /// </list>
  /// </para>
  /// <para>
  /// Regardless to the modes set all the other parameters are also applied. I.e. you don't need to
  /// re-apply them when changing mode.
  /// </para>
  /// </remarks>
  /// <param name="joint">Joint to setup.</param>
  /// <param name="springForce">
  /// Torque force to apply when joint angle deviates from the original.
  /// </param>
  /// <param name="springDamperRatio">
  /// Percentage of the torque force to use for dampering oscillation effect.
  /// </param>
  /// <param name="maxSpringForce">
  /// Maximum torque force to apply when joint angle deviates from the original.
  /// </param>
  /// <param name="angleLimit">Maximum rotation angle (degrees) around Y or Z axis.</param>
  /// <param name="angleLimitForce">
  /// Force to apply to keep joint in limits. If it's <c>0</c> then the limit is impassible.
  /// </param>
  /// <param name="angleLimitDamperRatio">
  /// Percentage of the limit force to use for dampering oscillation effect.
  /// </param>
  void SetupSphericalJoint(ConfigurableJoint joint,
                           float springForce = 0,
                           float springDamperRatio = 0.1f,
                           float maxSpringForce = Mathf.Infinity,
                           float angleLimit = Mathf.Infinity,
                           float angleLimitForce = 0,
                           float angleLimitDamperRatio = 0.1f);
}

}  // namespace
