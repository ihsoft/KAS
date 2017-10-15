// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using KSPDev.ProcessingUtils;
using System;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>
/// Module that offers normal KAS joint logic basing on the joint created by KSP. The joint is not
/// modified in any way, and its behavior is very similar to the behavior of a regular joint that
/// would normally connect two parts together.
/// </summary>
// FIXME deprecate the module
public class KASModuleStockJoint :
    // KAS parents.
    AbstractJointModule {
}

}  // namespace
