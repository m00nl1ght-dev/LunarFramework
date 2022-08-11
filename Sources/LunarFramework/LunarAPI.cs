using System;
using LunarFramework.Bootstrap;
using Verse;

namespace LunarFramework;

public class LunarAPI
{
    public static Version FrameworkVersion => typeof(LunarAPI).Assembly.GetName().Version;
    
    public static LunarAPI InitializeForMod(ModContentPack mcp)
    {
        if (!Entrypoint.LunarMods.TryGetValue(mcp.PackageId, out var mod)) return null;
        if (mod.LoadingState != LoadingState.Loaded) return null;
        return new LunarAPI(mod);
    }

    private readonly LunarMod _mod;

    private LunarAPI(LunarMod mod)
    {
        _mod = mod;
    }
}