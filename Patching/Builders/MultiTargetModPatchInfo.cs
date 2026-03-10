using STS2ShowPlayerHandCards.Patching.Models;

namespace STS2ShowPlayerHandCards.Patching.Builders
{
    /// <summary>
    ///     Builder for applying the same patch to multiple targets (classes/methods)
    ///     Useful when the same logic is duplicated across multiple types
    /// </summary>
    public class MultiTargetModPatchInfo(
        string id,
        ModPatchTarget[] targets,
        Type patchType,
        bool isCritical = true,
        string description = "")
    {
        public string Id { get; } = id;
        public ModPatchTarget[] Targets { get; } = targets;
        public Type PatchType { get; } = patchType;
        public bool IsCritical { get; } = isCritical;

        public string Description { get; } = string.IsNullOrEmpty(description)
            ? $"Multi-target patch: {id}"
            : description;

        /// <summary>
        ///     Create a multi-target patch for the same method name across multiple types
        ///     Example: Patch "ProcessData" method in ClassA, ClassB, ClassC
        /// </summary>
        public static MultiTargetModPatchInfo ForMethod(
            string id,
            string methodName,
            Type patchType,
            bool isCritical = true,
            string description = "",
            params ReadOnlySpan<Type> targetTypes)
        {
            var targets = new ModPatchTarget[targetTypes.Length];
            for (var i = 0; i < targetTypes.Length; i++)
                targets[i] = new(targetTypes[i], methodName);
            return new(id, targets, patchType, isCritical, description);
        }

        /// <summary>
        ///     Create a multi-target patch for multiple methods in the same type
        ///     Example: Patch "Method1", "Method2", "Method3" in SomeClass
        /// </summary>
        public static MultiTargetModPatchInfo ForType(
            string id,
            Type targetType,
            Type patchType,
            bool isCritical = true,
            string description = "",
            params ReadOnlySpan<string> methodNames)
        {
            var targets = new ModPatchTarget[methodNames.Length];
            for (var i = 0; i < methodNames.Length; i++)
                targets[i] = new(targetType, methodNames[i]);
            return new(id, targets, patchType, isCritical, description);
        }

        /// <summary>
        ///     Create a multi-target patch for all combinations of types and methods
        ///     Example: Patch ["Method1", "Method2"] in [ClassA, ClassB] = 4 patches
        /// </summary>
        public static MultiTargetModPatchInfo ForCombinations(
            string id,
            Type patchType,
            bool isCritical = true,
            string description = "",
            Type[]? targetTypes = null,
            string[]? methodNames = null)
        {
            targetTypes ??= [];
            methodNames ??= [];

            var targets = new ModPatchTarget[targetTypes.Length * methodNames.Length];
            var index = 0;
            foreach (var type in targetTypes)
            foreach (var method in methodNames)
            {
                targets[index] = new(type, method);
                index++;
            }

            return new(id, targets, patchType, isCritical, description);
        }

        /// <summary>
        ///     Convert to individual PatchInfo instances for registration
        /// </summary>
        public ModPatchInfo[] ToPatchInfos()
        {
            return Targets.Select((target, index) => new ModPatchInfo(
                $"{Id}_{target.TargetType.Name}_{target.MethodName}",
                target.TargetType,
                target.MethodName,
                PatchType,
                IsCritical,
                $"{Description} -> {target}",
                target.ParameterTypes
            )).ToArray();
        }

        public override string ToString()
        {
            return $"{Id}: {Targets.Length} targets with {PatchType.Name}";
        }
    }
}
