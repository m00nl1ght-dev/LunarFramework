#if RW_1_6_OR_GREATER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LunarFramework.Internal;

namespace LunarFramework.Patching;

internal static class HarmonyInliningFixer
{
    public static void Apply()
    {
        var harmony = new Harmony("LF_inlining_fixer");

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var groupNames = new HashSet<string>(PatchGroup.AllGroups.Select(g => g.Name));

        bool HasAttribute(Patch patch)
        {
            return groupNames.Contains(patch.owner) && patch.PatchMethod?.GetCustomAttribute<PatchTargetPotentiallyInlined>() != null;
        }

        var patchedMethods = Harmony.GetAllPatchedMethods();
        var refreshTargets = new HashSet<MethodBase>();

        // The attribute is only used on patches applied by Lunar patch groups, so only the methods
        // patched by those need to be checked, and resolving PatchMethod is only needed for their own patches
        foreach (var method in PatchGroup.AllGroups.SelectMany(g => g.PatchedMethods).Distinct())
        {
            try
            {
                var patchInfo = PatchProcessor.GetPatchInfo(method);
                if (patchInfo == null) continue;

                if (patchInfo.Prefixes.Any(HasAttribute) ||
                    patchInfo.Postfixes.Any(HasAttribute) ||
                    patchInfo.Transpilers.Any(HasAttribute) ||
                    patchInfo.Finalizers.Any(HasAttribute))
                {
                    refreshTargets.Add(method.GetDeclaredMember());
                }
            }
            catch (Exception e)
            {
                LunarRoot.Logger.Debug($"Exception while checking Harmony patches for {method.FullDescription()}", e);
            }
        }

        if (refreshTargets.Count == 0) return;

        var count = 0;

        foreach (var method in patchedMethods)
        {
            var methodBody = PatchProcessor.ReadMethodBody(method);

            if (methodBody.Any(e => e.Value is MethodBase m && refreshTargets.Contains(m)))
            {
                try
                {
                    LunarRoot.Logger.Debug($"Refreshing Harmony wrapper for {method.FullDescription()}");
                    new PatchProcessor(harmony, method.GetDeclaredMember()).Patch();
                    count++;
                }
                catch (Exception e)
                {
                    LunarRoot.Logger.Error($"Failed to refresh Harmony wrapper for {method.FullDescription()}", e);
                }
            }
        }

        LunarRoot.Logger.Log($"Processed {count} Harmony patches in {stopwatch.ElapsedMilliseconds} ms.");
    }
}

/// <summary>
/// Marks a patch whose target may be inlined into other methods, so that HarmonyInliningFixer re-patches the patched methods that reference it.
/// HarmonyInliningFixer runs once, after the Lunar components have initialized, and only checks patches applied through a PatchGroup by then.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class PatchTargetPotentiallyInlined : Attribute;

#endif
