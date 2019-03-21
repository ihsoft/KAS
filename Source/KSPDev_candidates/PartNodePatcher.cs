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
  /// <returns><c>true</c> if the TEST rules of the patch have matched.</returns>
  public static bool TestPatch(
      ConfigNode partNode, ConfigNodePatch patch, LoadContext loadContext) {
    ArgumentGuard.NotNull(patch, "patch", context: patch);
    ArgumentGuard.NotNull(partNode, "partNode", context: patch);
    ArgumentGuard.OneOf(loadContext, "loadContext", new[] {LoadContext.SFS, LoadContext.Craft},
                        context: patch);

    // Check if the part definition matches.
    var partName = GetPartNameFromUpgradeNode(partNode, loadContext);
    Preconditions.NotNullOrEmpty(partName, message: "TEST/PART/name", context: patch);
    if (patch.testSection.partTests.name != partName
        || !CheckPatchValues(patch.testSection.partTests, partNode)) {
      return false;
    }
    
    // Check if the part modules definition matches. This one is tricky.
    foreach (var moduleTests in patch.testSection.moduleTests) {
      ConfigNode targetModuleNode;
      try {
        targetModuleNode = LookupModule(partNode, moduleTests.name, "TEST", patch);
      } catch (InvalidOperationException ex) {
        DebugEx.Error(ex.Message);
        return false;
      }
      if (!CheckPatchValues(moduleTests, targetModuleNode)) {
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
          targetModuleNode = LookupModule(partNode, moduleRules.name, "UPGRADE", patch);
        }
        ApplyPatchToNode(targetModuleNode, moduleRules, context);
      }
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
          DebugEx.Warning(
              "[UpgradePipeline][{0}] Action: SET {1}={2}", context, fieldName, rulePair[1]);
          target.SetValue(fieldName, rulePair[1], createIfNotFound: true);
        } else {
          Preconditions.MinElements(rulePair, 2, message: "Need add value", context: context);
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
  /// <param name="patchSection">
  /// The name of the patch section for which module is being looked up. E.g. "UPGRADE".
  /// </param>
  /// <param name="patchContext">The patch for which the lookup is done.</param>
  /// <returns>The config node of the found part module. It's never <c>null</c>.</returns>
  /// <exception cref="InvalidOperationException">If the module cannot be found.</exception>
  /// <seealso cref="ConfigNodePatch"/>
  static ConfigNode LookupModule(
      ConfigNode partNode, string namePattern, string patchSection, object patchContext) {
    Preconditions.NotNullOrEmpty(namePattern, patchSection + "/MODULE/name", context: patchContext);
    var namePair = namePattern.Split(',');
    ConfigNode moduleNode;
    if (namePair.Length == 1) {
      moduleNode = partNode.GetNodes("MODULE")
          .FirstOrDefault(x => x.GetValue("name") == namePair[0]);
      Preconditions.ConfValueExists(
          moduleNode, patchSection + "/MODULE/" + namePair[0], context: partNode);
    } else {
      // Fetch a module at the index. 
      var suffix = namePair[1];
      Preconditions.NotNullOrEmpty(
          suffix, message: "Bad name suffix in: " + namePair[1], context: patchContext);
      if (suffix[0] == '+') {
        // Relative index for the repeated modules.
        var skipNodes = int.Parse(suffix.Substring(1));
        var nodes = partNode.GetNodes("MODULE")
            .Where(x => x.GetValue("name") == namePair[0])
            .ToArray();
        Preconditions.MinElements(
            nodes, skipNodes + 1, message: patchSection + "/MODULE/" + namePair[0], context: partNode);
        moduleNode = nodes[skipNodes];
      } else {
        // Absolute index.
        var nodeIndex = int.Parse(suffix);
        var nodes = partNode.GetNodes("MODULE");
        Preconditions.MinElements(nodes, nodeIndex + 1,
                                  message: patchSection + "/MODULE", context: partNode);
        moduleNode = nodes[nodeIndex];
        if (moduleNode.GetValue("name") != namePair[0]) {
          var message = string.Format(
              "Module at index {0} is not {1}:\n{2}", nodeIndex, namePair[0], moduleNode);
          throw new InvalidOperationException(Preconditions.MakeContextError(partNode, message));
        }
      }
    }
    return moduleNode;
  }
}
  
}  // namespace
