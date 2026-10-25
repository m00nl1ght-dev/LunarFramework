#if RW_1_6_OR_GREATER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LunarFramework.Bootstrap;
using LunarFramework.Internal;

namespace LunarFramework.Patching;

internal static class HarmonyInliningFixer
{
    public static void Apply()
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var refreshTargets = new HashSet<MethodBase>();

        #if RW_1_7_OR_GREATER

        var monoModPlatform = AccessTools.PropertyGetter("MonoMod.Core.Platforms.PlatformTriple:Current")?.Invoke(null, null);
        if (monoModPlatform == null) throw new Exception("Failed to obtain MonoMod platform instance");

        var tryDisableInliningMethod = AccessTools.Method("MonoMod.Core.Platforms.PlatformTriple:TryDisableInlining");
        if (tryDisableInliningMethod == null) throw new Exception("Failed to find TryDisableInlining method in MonoMod");

        var getOriginalMethod = AccessTools.Method("HarmonyLib.PatchTools:GetOriginalMethod");
        if (getOriginalMethod == null) throw new Exception("Failed to find GetOriginalMethod in Harmony");

        foreach (var component in Entrypoint.LunarComponents.Values)
        {
            foreach (var type in AccessTools.GetTypesFromAssembly(component.LoadedAssembly))
            {
                if (type.IsDefined(typeof(HarmonyPatch)))
                {
                    var attrOnType = type.GetCustomAttribute<HarmonyPatch>();

                    foreach (var method in AccessTools.GetDeclaredMethods(type))
                    {
                        if (method.IsDefined(typeof(HarmonyPatch)) && method.IsDefined(typeof(PatchTargetPotentiallyInlined)))
                        {
                            var attrOnMethod = method.GetCustomAttribute<HarmonyPatch>();
                            var mergedInfo = attrOnType.info.Merge(attrOnMethod.info);

                            var originalMethod = getOriginalMethod.Invoke(null, [mergedInfo]) as MethodBase;
                            if (originalMethod != null && refreshTargets.Add(originalMethod))
                            {
                                var result = (bool) tryDisableInliningMethod.Invoke(monoModPlatform, [originalMethod]);
                                if (result)
                                {
                                    LunarRoot.Logger.Debug($"Successfully disabled inlining for method {originalMethod}");
                                }
                                else
                                {
                                    LunarRoot.Logger.Warn($"Failed to disable inlining for method {originalMethod}");
                                }
                            }
                        }
                    }
                }
            }
        }

        #else

        var groupNames = new HashSet<string>(PatchGroup.AllGroups.Select(g => g.Name));

        bool HasAttribute(Patch patch)
        {
            return groupNames.Contains(patch.owner) && patch.PatchMethod?.GetCustomAttribute<PatchTargetPotentiallyInlined>() != null;
        }

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

        #endif

        if (refreshTargets.Count == 0) return;

        var count = 0;

        var patchedMethods = Harmony.GetAllPatchedMethods();

        var harmony = new Harmony("LF_inlining_fixer");

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
