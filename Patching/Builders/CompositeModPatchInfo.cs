using STS2ShowPlayerHandCards.Patching.Models;

namespace STS2ShowPlayerHandCards.Patching.Builders
{
    /// <summary>
    ///     Builder for applying multiple different patches to the same target
    ///     Useful when you need to combine multiple aspects (logging, performance, security, etc.)
    /// </summary>
    public class CompositeModPatchInfo(
        string id,
        Type targetType,
        string methodName,
        Type[] patchTypes,
        bool isCritical = true,
        string description = "")
    {
        public string Id { get; } = id;
        public Type TargetType { get; } = targetType;
        public string MethodName { get; } = methodName;
        public Type[] PatchTypes { get; } = patchTypes;
        public bool IsCritical { get; } = isCritical;

        public string Description { get; } = string.IsNullOrEmpty(description)
            ? $"Composite patch for {targetType.Name}.{methodName}"
            : description;

        /// <summary>
        ///     Create a composite patch with fluent builder pattern
        /// </summary>
        public static CompositePatchBuilder For(Type targetType, string methodName)
        {
            return new(targetType, methodName);
        }

        /// <summary>
        ///     Convert to individual PatchInfo instances for registration
        /// </summary>
        public ModPatchInfo[] ToPatchInfos()
        {
            return PatchTypes.Select((patchType, index) => new ModPatchInfo(
                $"{Id}_part{index + 1}_{patchType.Name}",
                TargetType,
                MethodName,
                patchType,
                IsCritical,
                $"{Description} (Part {index + 1}: {patchType.Name})"
            )).ToArray();
        }

        public override string ToString()
        {
            return $"{Id}: {TargetType.Name}.{MethodName} <- [{string.Join(", ", PatchTypes.Select(p => p.Name))}]";
        }
    }

    /// <summary>
    ///     Fluent builder for CompositePatchInfo
    /// </summary>
    public class CompositePatchBuilder(Type targetType, string methodName)
    {
        private readonly List<Type> _patchTypes = [];
        private string _description = "";
        private string _id = "";
        private bool _isCritical = true;

        public CompositePatchBuilder WithId(string id)
        {
            _id = id;
            return this;
        }

        public CompositePatchBuilder WithPatch(Type patchType)
        {
            _patchTypes.Add(patchType);
            return this;
        }

        public CompositePatchBuilder WithPatches(params ReadOnlySpan<Type> patchTypes)
        {
            foreach (var patchType in patchTypes)
                _patchTypes.Add(patchType);
            return this;
        }

        public CompositePatchBuilder Critical(bool isCritical = true)
        {
            _isCritical = isCritical;
            return this;
        }

        public CompositePatchBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        public CompositeModPatchInfo Build()
        {
            if (string.IsNullOrEmpty(_id))
                _id = $"composite_{targetType.Name}_{methodName}";

            return new(_id, targetType, methodName, _patchTypes.ToArray(), _isCritical, _description);
        }
    }
}
