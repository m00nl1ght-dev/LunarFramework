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

        bool HasAttribute(Patch patch)
        {
            return patch.PatchMethod?.GetCustomAttribute<PatchTargetPotentiallyInlined>() != null;
        }

        var patchedMethods = Harmony.GetAllPatchedMethods();
        var refreshTargets = new HashSet<MethodBase>();

        foreach (var method in patchedMethods)
        {
            var patchInfo = PatchProcessor.GetPatchInfo(method);

            if (patchInfo.Prefixes.Any(HasAttribute) ||
                patchInfo.Postfixes.Any(HasAttribute) ||
                patchInfo.Transpilers.Any(HasAttribute) ||
                patchInfo.Finalizers.Any(HasAttribute))
            {
                refreshTargets.Add(method.GetDeclaredMember());
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

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class PatchTargetPotentiallyInlined : Attribute;

#endif
