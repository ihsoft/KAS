// Kerbal Attachment System API
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.LogUtils;
using System.Text;
using UnityEngine;

// ReSharper disable UseStringInterpolation
// ReSharper disable once CheckNamespace
namespace KASImpl {

class JointUtilsImpl : KASAPIv2.IJointUtils {
  /// <inheritdoc/>
  public string DumpJoint(ConfigurableJoint joint) {
    if (joint == null) {
      return "<NULL JOINT>";
    }
    var msg = DumpBaseJoint(joint);
    // Geometry.
    msg.Append("secondaryAxis: ").Append(DbgFormatter.Vector(joint.secondaryAxis)).AppendLine();
    // X axis settings.
    msg.Append("xDrive: ").Append(Dump(joint.xDrive)).AppendLine();
    msg.Append("xMotion: ").Append(joint.xMotion).AppendLine();
    msg.Append("angularXMotion: ").Append(joint.angularXMotion).AppendLine();
    msg.Append("angularXLimitSpring: ").Append(Dump(joint.angularXLimitSpring)).AppendLine();
    msg.Append("angularXDrive: ").Append(Dump(joint.angularXDrive)).AppendLine();
    msg.Append("lowAngularXLimit: ").Append(Dump(joint.lowAngularXLimit)).AppendLine();
    msg.Append("highAngularXLimit: ").Append(Dump(joint.highAngularXLimit)).AppendLine();
    // Y axis settings.
    msg.Append("yDrive: ").Append(Dump(joint.yDrive)).AppendLine();
    msg.Append("yMotion: ").Append(joint.yMotion).AppendLine();
    msg.Append("angularYMotion: ").Append(joint.angularYMotion).AppendLine();
    msg.Append("angularYLimit: ").Append(Dump(joint.angularYLimit)).AppendLine();
    // Z axis settings.
    msg.Append("zDrive: ").Append(Dump(joint.zDrive)).AppendLine();
    msg.Append("zMotion: ").Append(joint.zMotion).AppendLine();
    msg.Append("angularZMotion: ").Append(joint.angularZMotion).AppendLine();
    msg.Append("angularZLimit: ").Append(Dump(joint.angularZLimit)).AppendLine();
    // Multiple axis settings.
    msg.Append("linearLimit: ").Append(Dump(joint.linearLimit)).AppendLine();
    msg.Append("linearLimitSpring: ").Append(Dump(joint.linearLimitSpring)).AppendLine();
    msg.Append("angularYZDrive: ").Append(Dump(joint.angularYZDrive)).AppendLine();
    msg.Append("angularYZLimitSpring: ").Append(Dump(joint.angularYZLimitSpring)).AppendLine();

    return msg.ToString();
  }

  /// <inheritdoc/>
  public string DumpSpringJoint(SpringJoint joint) {
    if (joint == null) {
      return "<NULL JOINT>";
    }
    var msg = DumpBaseJoint(joint);

    // Distance joint specific settings.    
    msg.Append("spring: ").Append(joint.spring).AppendLine();
    msg.Append("damper: ").Append(joint.damper).AppendLine();
    msg.Append("maxDistance: ").Append(joint.maxDistance).AppendLine();
    msg.Append("minDistance: ").Append(joint.minDistance).AppendLine();
    msg.Append("tolerance: ").Append(joint.tolerance).AppendLine();

    return msg.ToString();
  }

  /// <inheritdoc/>
  public void ResetJoint(ConfigurableJoint joint) {
    joint.xDrive = new JointDrive();
    joint.xMotion = ConfigurableJointMotion.Locked;
    joint.yDrive = new JointDrive();
    joint.yMotion = ConfigurableJointMotion.Locked;
    joint.zDrive = new JointDrive();
    joint.zMotion = ConfigurableJointMotion.Locked;
    joint.angularXLimitSpring = new SoftJointLimitSpring();
    joint.angularXMotion = ConfigurableJointMotion.Locked;
    joint.angularYLimit = new SoftJointLimit();
    joint.angularYMotion = ConfigurableJointMotion.Locked;
    joint.angularZLimit = new SoftJointLimit();
    joint.angularZMotion = ConfigurableJointMotion.Locked;
    joint.angularYZLimitSpring = new SoftJointLimitSpring();
    joint.angularYZDrive = new JointDrive();
    joint.linearLimit = new SoftJointLimit();
    joint.linearLimitSpring = new SoftJointLimitSpring();
    joint.targetRotation = Quaternion.identity;
    joint.targetAngularVelocity = Vector3.zero;
    joint.axis = Vector3.right;
    joint.secondaryAxis = Vector3.up;
  }

  /// <inheritdoc/>
  public void SetupPrismaticJoint(ConfigurableJoint joint,
                                  float springForce = 0,
                                  float springDamperRatio = 0.1f,
                                  float maxSpringForce = Mathf.Infinity,
                                  float distanceLimit = Mathf.Infinity,
                                  float distanceLimitForce = 0,
                                  float distanceLimitDamperRatio = 0.1f) {
    // Swap X&Z axes so that the joint's forward vector becomes a primary axis.
    joint.axis = Vector3.forward;
    joint.secondaryAxis = Vector3.right;
    // Setup linear joint parameters.
    joint.xDrive = new JointDrive() {
      positionSpring = springForce,
      positionDamper = float.IsInfinity(springForce) ? 0 : springForce * springDamperRatio,
      maximumForce = maxSpringForce
    };
    joint.linearLimit = new SoftJointLimit() {
      limit = distanceLimit
    };
    joint.linearLimitSpring = new SoftJointLimitSpring() {
      spring = distanceLimitForce,
      damper = float.IsInfinity(distanceLimitForce)
          ? 0
          : distanceLimitForce * distanceLimitDamperRatio
    };
    // Optimize mode basing on the input parameters.
    if (float.IsInfinity(springForce) || Mathf.Approximately(distanceLimit, 0)) {
      // Joint doesn't allow linear movement - lock it.
      joint.xMotion = ConfigurableJointMotion.Locked;
    } else if (Mathf.Approximately(springForce, 0f) && float.IsInfinity(distanceLimit)) {
      // Joint doesn't restrict linear movement - make it free.
      joint.xMotion = ConfigurableJointMotion.Free;
    } else {
      joint.xMotion = ConfigurableJointMotion.Limited;
    }
  }

  /// <inheritdoc/>
  public void SetupSphericalJoint(ConfigurableJoint joint,
                                  float springForce = 0,
                                  float springDamperRatio = 0.1f,
                                  float maxSpringForce = Mathf.Infinity,
                                  float angleLimit = Mathf.Infinity,
                                  float angleLimitForce = 0,
                                  float angleLimitDamperRatio = 0.1f) {
    // Swap X&Z axes so what joint's forward vector becomes a primary axis.
    joint.axis = Vector3.forward;
    joint.secondaryAxis = Vector3.right;
    // Setup angular joint parameters.
    joint.angularXMotion = ConfigurableJointMotion.Free;
    joint.angularYZDrive = new JointDrive() {
      positionSpring = springForce,
      positionDamper = float.IsInfinity(springForce) ? 0 :  springForce * springDamperRatio,
      maximumForce = maxSpringForce
    };
    joint.angularYZLimitSpring = new SoftJointLimitSpring() {
      spring = angleLimitForce,
      damper = angleLimitForce * angleLimitDamperRatio
    };
    joint.angularYLimit = new SoftJointLimit() {
      limit = angleLimit
    };
    joint.angularZLimit = new SoftJointLimit() {
      limit = angleLimit
    };
    // Optimize mode basing on the input parameters.
    if (float.IsInfinity(springForce) || Mathf.Approximately(angleLimit, 0)) {
      // Joint doesn't allow rotation - just lock it.
      joint.angularYMotion = ConfigurableJointMotion.Locked;
      joint.angularZMotion = ConfigurableJointMotion.Locked;
    } else if (Mathf.Approximately(springForce, 0) && float.IsInfinity(angleLimit)) {
      // Joint doesn't restrict rotation - allow free mode.
      joint.angularYMotion = ConfigurableJointMotion.Free;
      joint.angularZMotion = ConfigurableJointMotion.Free;
    } else {
      joint.angularYMotion = ConfigurableJointMotion.Limited;
      joint.angularZMotion = ConfigurableJointMotion.Limited;
    }
  }

  /// <inheritdoc/>
  public void SetupDistanceJoint(ConfigurableJoint joint,
                                 float springForce = 0,
                                 float springDamper = 0,
                                 float maxDistance = Mathf.Infinity) {
    // Swap X&Z axes so that the joint's forward vector becomes a primary axis.
    joint.axis = Vector3.forward;
    joint.secondaryAxis = Vector3.right;
    // Setup linear joint parameters.
    joint.linearLimit = new SoftJointLimit() {
      limit = maxDistance
    };
    joint.linearLimitSpring = new SoftJointLimitSpring() {
      spring = springForce,
      damper = springDamper
    };
    joint.xMotion = ConfigurableJointMotion.Limited;
    joint.angularXMotion = ConfigurableJointMotion.Free;
    joint.yMotion = ConfigurableJointMotion.Limited;
    joint.angularYMotion = ConfigurableJointMotion.Free;
    joint.zMotion = ConfigurableJointMotion.Limited;
    joint.angularZMotion = ConfigurableJointMotion.Free;
  }

  /// <inheritdoc/>
  public void SetupFixedJoint(ConfigurableJoint joint) {
    // Swap X&Z axes so that the joint's forward vector becomes a primary axis.
    joint.axis = Vector3.forward;
    joint.secondaryAxis = Vector3.right;
    joint.xMotion = ConfigurableJointMotion.Locked;
    joint.angularXMotion = ConfigurableJointMotion.Locked;
    joint.yMotion = ConfigurableJointMotion.Locked;
    joint.angularYMotion = ConfigurableJointMotion.Locked;
    joint.zMotion = ConfigurableJointMotion.Locked;
    joint.angularZMotion = ConfigurableJointMotion.Locked;
  }

  StringBuilder DumpBaseJoint(Joint joint) {
    var msg = new StringBuilder();
    msg.Append("name: ").Append(joint.name).AppendLine();
    msg.Append("ownerBody: ")
        .Append(DebugEx.ObjectToString(joint.gameObject.GetComponent<Rigidbody>()))
        .AppendLine();
    msg.Append("connectedBody: ")
        .Append(DebugEx.ObjectToString(joint.connectedBody))
        .AppendLine();
    // Collider setup.
    msg.Append("enableCollision: ").Append(joint.enableCollision).AppendLine();
    // Optimization.
    msg.Append("enablePreprocessing: ").Append(joint.enablePreprocessing).AppendLine();
    // Break forces.
    msg.Append("breakForce: ").Append(joint.breakForce).AppendLine();
    msg.Append("breakTorque: ").Append(joint.breakTorque).AppendLine();
    // Geometry.
    msg.Append("anchor: ").Append(DbgFormatter.Vector(joint.anchor)).AppendLine();
    msg.Append("connectedAnchor: ")
        .Append(DbgFormatter.Vector(joint.connectedAnchor))
        .AppendLine();
    msg.Append("axis: ").Append(DbgFormatter.Vector(joint.axis)).AppendLine();
    return msg;
  }

  static string Dump(SoftJointLimitSpring limitSpring) {
    return string.Format("SoftJointLimitSpring(spring={0}, damper={1})",
                         limitSpring.spring, limitSpring.damper);
  }

  static string Dump(SoftJointLimit limit) {
    return string.Format("SoftJointLimit(limit={0}, bounciness={1}, contactDistance={2})",
                         limit.limit, limit.bounciness, limit.contactDistance);
  }

  static string Dump(JointDrive drive) {
    return string.Format(
        "JointDrive(spring={0}, damper={1}, maxForce={2})",
        drive.positionSpring, drive.positionDamper, drive.maximumForce);
  }
}
  
}  // namespace
