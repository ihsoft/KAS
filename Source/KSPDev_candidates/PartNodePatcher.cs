// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Collections.Generic;
using System.Linq;
using KSPDev.LogUtils;
using KSPDev.Types;
using KSPDev.ProcessingUtils;
using KSPDev.ConfigUtils;
using SaveUpgradePipeline;

namespace KSPDev.ConfigUtils {

/// <summary>A set of methods to deal with the part config compatibility.</summary>
/// <remarks>
/// These methods help patching the saved game in order to fix the incompatible changes to the
/// parts.
/// </remarks>
public static class PartNodePatcher {
  /// <summary>Extracts the part name from the game state.</summary>
  /// <param name="node">The part's config node to extract the value from.</param>
  /// <param name="loadContext">The current loading context.</param>
  /// <returns>The part's name.</returns>
  public static string GetPartNameFromUpgradeNode(ConfigNode node, LoadContext loadContext) {
    ArgumentGuard.NotNull(node, "node", context: node);
    ArgumentGuard.OneOf(loadContext, "loadContext", new[] {LoadContext.SFS, LoadContext.Craft},
                        context: node);
    if (loadContext == LoadContext.SFS) {
      return node.GetValue("name");
    }
    var craftPartName = node.GetValue("part");
    Preconditions.ConfValueExists(craftPartName, "node/part", context: node);
    var pair = craftPartName.Split(new[] {'_'}, 2);
    Preconditions.HasSize(pair, 2, message: "craftPartName", context: node);
    return pair[0];
  }

  /// <summary>Returns patch nodes for the tag.</summary>
  /// <remarks>
  /// This call can be very expensive. It's strongly encouraged to implement a lazy access approach
  /// and cache the retunred values.
  /// </remarks>
  /// <param name="modName">
  /// The mod to find the nodes for. If it's <c>null</c> or empty, then all the nodes will be
  /// returned.
  /// </param>
  /// <returns>The patch nodes for the mod.</returns>
  public static ConfigNodePatch[] GetPatches(string modName) {
    var patchConfigs = GameDatabase.Instance.GetConfigs("COMPATIBILITY");
    var patches = new List<ConfigNodePatch>();
    foreach (var patchConfig in patchConfigs) {
      try {
        var patch = ConfigNodePatch.MakeFromConfig(patchConfig);
        if (string.IsNullOrEmpty(modName) || patch.modName == modName) {
          patches.Add(patch);
        }
      } catch (Exception ex) {
        DebugEx.Error("Skipping bad patch node: {0}", ex.Message);
        continue;
      }
    }
    return patches.ToArray();
  }

  /// <summary>Tests if the patch can be applied to the part node.</summary>
  /// <param name="partNode">The part node to test against.</param>
  /// <param name="patch">The patch to test.</param>
  /// <param name="loadContext">The conext in whcih the part is being patched.</param>
  /// <param name="quietMode">Tells if anything should be reported to the logs.</param>
  /// <returns><c>true</c> if the TEST rules of the patch have matched.</returns>
  public static bool TestPatch(ConfigNode partNode, ConfigNodePatch patch, LoadContext loadContext,
                               bool quietMode = false) {
    ArgumentGuard.NotNull(patch, "patch", context: patch);
    ArgumentGuard.NotNull(partNode, "partNode", context: patch);
    ArgumentGuard.OneOf(loadContext, "loadContext", new[] {LoadContext.SFS, LoadContext.Craft},
                        context: patch);

    // Check if the part definition matches.
    var partName = GetPartNameFromUpgradeNode(partNode, loadContext);
    Preconditions.NotNullOrEmpty(partName, message: "part config node", context: partNode);
    var patchName = patch.testSection.partTests.GetValue("name");
    Preconditions.NotNullOrEmpty(patchName, message: "TEST/PART/name", context: patch);
    if (patchName != partName) {
      return false;  // Not for this part.
    }
    if (!CheckPatchValues(patch.testSection.partTests, partNode)) {
      if (patch.verboseLogging && !quietMode) {
        DebugEx.Warning(
            "PART test rules haven't matched: patch={0}\nTest node:\n{1}\nPartNode:\n{2}",
            patch, patch.testSection.partTests, partNode);
      }
      return false;
    }
    
    // Check if the part modules definition matches. This one is tricky.
    foreach (var moduleTests in patch.testSection.moduleTests) {
      var modulePattern = moduleTests.GetValue("name");
      Preconditions.NotNullOrEmpty(modulePattern, context: moduleTests);
      var targetModuleNode = LookupModule(partNode, modulePattern);
      if (targetModuleNode == null) {
        if (patch.verboseLogging && !quietMode) {
          DebugEx.Warning(
              "MODULE cannot be found: patch={0}, moduleName={1}\nPartNode:\n{2}",
              patch, modulePattern, partNode);
        }
        return false;
      }
      if (!CheckPatchValues(moduleTests, targetModuleNode)) {
        if (patch.verboseLogging && !quietMode) {
          DebugEx.Warning(
              "MODULE test rules haven't matched: patch={0}\nTest node:\n{1}\nModeleNode:\n{2}",
              patch, moduleTests, targetModuleNode);
        }
        return false;
      }
    }
    
    return true;
  }

  /// <summary>Applies the patch to the part node.</summary>
  /// <param name="partNode">The part node to patch.</param>
  /// <param name="patch">The patch to apply.</param>
  /// <param name="loadContext">The conext in whcih the part is being patched.</param>
  public static void PatchNode(
      ConfigNode partNode, ConfigNodePatch patch, LoadContext loadContext) {
    ArgumentGuard.NotNull(partNode, "partNode", context: patch);
    ArgumentGuard.NotNull(patch, "patchNode", context: patch);
    ArgumentGuard.OneOf(loadContext, "loadContext", new[] {LoadContext.SFS, LoadContext.Craft},
                        context: patch);
    var partName = GetPartNameFromUpgradeNode(partNode, loadContext);
    Preconditions.OneOf(patch.upgradeSection.partRules.action,
                        new[] {ConfigNodePatch.PatchAction.Drop, ConfigNodePatch.PatchAction.Fix},
                        context: patch);
    var oldPartNode = partNode.CreateCopy();
    ApplyPatchToNode(partNode, patch.upgradeSection.partRules, "Part#" + partName);
    if (patch.upgradeSection.partRules.action == ConfigNodePatch.PatchAction.Fix) {
      foreach (var moduleRules in patch.upgradeSection.moduleRules) {
        ConfigNode targetModuleNode;
        var context = "Part#" + partName + "#" + moduleRules.name;
        if (moduleRules.action == ConfigNodePatch.PatchAction.Add) {
          DebugEx.Warning("[UpgradePipeline][{0}] Action: ADD NODE", context);
          targetModuleNode = partNode.AddNode("MODULE");
          targetModuleNode.SetValue("name", moduleRules.name, "*** added by comaptibility patch",
                                    createIfNotFound: true);
        } else {
          targetModuleNode = LookupModule(partNode, moduleRules.name);
          Preconditions.NotNull(
              targetModuleNode, message: "Cannot find module for UPGRADE", context: patch);
        }
        ApplyPatchToNode(targetModuleNode, moduleRules, context);
      }
    }
    if (patch.verboseLogging) {
      DebugEx.Warning("Part node has been patched:\nOriginal node:\n{0}\nNew node:\n{1}",
                      oldPartNode, partNode);
    }
  }

  /// <summary>Checks if all pattern fields are set in the target.</summary>
  /// <param name="pattern">The pattern node.</param>
  /// <param name="target">The target node.</param>
  /// <returns><c>true</c> if patch test rules match the target.</returns>
  static bool CheckPatchValues(ConfigNode pattern, ConfigNode target) {
    for (var i = 0; i < pattern.values.Count; i++) {
      var testValue = pattern.values[i];
      if (testValue.name == "name") {
        continue;  // The name field pattern has own syntax.
      }
      if (target.GetValue(testValue.name) != testValue.value) {
        return false;
      }
    }
    return true;
  }

  /// <summary>Applies patch rules on the target.</summary>
  /// <param name="target">The target node.</param>
  /// <param name="rules">The patching rules to apply.</param>
  /// <param name="context">The logging context.</param>
  static void ApplyPatchToNode(
      ConfigNode target, ConfigNodePatch.UpgradeSection.Rules rules, string context) {
    if (rules.action == ConfigNodePatch.PatchAction.Drop) {
      DebugEx.Warning("[UpgradePipeline][{0}] Action: DROP NODE", context);
      target.ClearData();
      target.name = "$DELETED";
    } else {
      foreach (var fieldRule in rules.fieldRules) {
        var rulePair = fieldRule.Split(new[] {'='}, 2);
        rulePair[0] = rulePair[0].Trim();
        Preconditions.NotNullOrEmpty(
            rulePair[0], message: "Field name must not be empty", context: context);
        var actionPrefix = rulePair[0].Substring(0, 1);
        if (actionPrefix != "-" && actionPrefix != "%") {
          actionPrefix = "";
        }
        var fieldName = actionPrefix != "" ? rulePair[0].Substring(1) : rulePair[0];
        if (actionPrefix == "-") {
          while (target.HasNode(fieldName)) {
            DebugEx.Warning("[UpgradePipeline][{0}] Action: DROP node={1}", context, fieldName);
            target.RemoveNode(fieldName);
          }
          while (target.HasValue(fieldName)) {
            DebugEx.Warning("[UpgradePipeline][{0}] Action: DROP value={1}", context, fieldName);
            target.RemoveValue(fieldName);
          }
        } else if (actionPrefix == "%") {
          Preconditions.MinElements(rulePair, 2, message: "Need add/edit value", context: context);
          rulePair[1] = rulePair[1].Trim();
          DebugEx.Warning(
              "[UpgradePipeline][{0}] Action: SET {1}={2}", context, fieldName, rulePair[1]);
          target.SetValue(fieldName, rulePair[1].Trim(), createIfNotFound: true);
        } else {
          Preconditions.MinElements(rulePair, 2, message: "Need add value", context: context);
          rulePair[1] = rulePair[1].Trim();
          DebugEx.Warning(
              "[UpgradePipeline][{0}] Action: ADD {1}={2}", context, fieldName, rulePair[1]);
          target.AddValue(fieldName, rulePair[1]);
        }
      }
    }
  }

  /// <summary>Helper method to find a patch module by the name pattern.</summary>
  /// <param name="partNode">The part config node to search the node in.</param>
  /// <param name="namePattern">The lookup module name pattern.</param>
  /// <returns>The config node of the found part module or <c>null</c>.</returns>
  /// <seealso cref="ConfigNodePatch"/>
  static ConfigNode LookupModule(ConfigNode partNode, string namePattern) {
    var namePair = namePattern.Split(new[] {','}, 2);
    var moduleName = namePair[0];
    var moduleSuffix = namePair.Length == 1 ? "" : namePair[1];
    ConfigNode moduleNode = null;
    if (moduleSuffix == "") {
      moduleNode = partNode.GetNodes("MODULE")
          .FirstOrDefault(x => x.GetValue("name") == moduleName);
    } else if (moduleSuffix[0] == '+') {
      // Relative index for the repeated modules.
      var skipNodes = int.Parse(moduleSuffix.Substring(1));
      var nodes = partNode.GetNodes("MODULE")
          .Where(x => x.GetValue("name") == moduleName)
          .ToArray();
      if (nodes.Length > skipNodes) {
        moduleNode = nodes[skipNodes];
      }
    } else {
      // Absolute index.
      var nodeIndex = int.Parse(moduleSuffix);
      var nodes = partNode.GetNodes("MODULE");
      if (nodes.Length > nodeIndex && nodes[nodeIndex].GetValue("name") != moduleName) {
        moduleNode = nodes[nodeIndex];
      }
    }
    return moduleNode;
  }
}
  
}  // namespace
