using System.Reflection;
using STS2ShowPlayerHandCards.Patching.Builders;
using STS2ShowPlayerHandCards.Patching.Models;
using STS2ShowPlayerHandCards.Patching.Rules;

namespace STS2ShowPlayerHandCards.Patching.Core
{
    /// <summary>
    ///     Extension methods for HarmonyPatcher to support advanced patch builders
    /// </summary>
    public static class ModPatcherExtensions
    {
        extension(ModPatcher patcher)
        {
            /// <summary>
            ///     Register a multi-target patch (same patch for multiple targets)
            /// </summary>
            public void RegisterMultiTarget(MultiTargetModPatchInfo multiModPatch)
            {
                patcher.RegisterPatches(multiModPatch.ToPatchInfos());
            }

            /// <summary>
            ///     Register a composite patch (multiple patches for same target)
            /// </summary>
            public void RegisterComposite(CompositeModPatchInfo compositeModPatch)
            {
                patcher.RegisterPatches(compositeModPatch.ToPatchInfos());
            }

            /// <summary>
            ///     Register patches generated from a rule
            /// </summary>
            public void RegisterFromRule(ModPatchRule rule,
                params ReadOnlySpan<Assembly> assemblies)
            {
                var patches = rule.GeneratePatches(assemblies);
                patcher.RegisterPatches(patches);
            }

            /// <summary>
            ///     Register patches from an IModPatchProvider type
            /// </summary>
            public void RegisterPatches<T>() where T : IModPatches
            {
                T.AddTo(patcher);
            }

            /// <summary>
            ///     Register patches from an IPatchMethod type (single patch)
            ///     or a class containing multiple IPatchMethod nested classes
            /// </summary>
            public void RegisterPatch<TPatch>() where TPatch : IPatchMethod
            {
                var patchInfos = IPatchMethod.CreatePatchInfos<TPatch>();
                patcher.RegisterPatches(patchInfos);
            }
        }
    }
}
