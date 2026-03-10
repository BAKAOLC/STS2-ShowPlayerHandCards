using System.Reflection;
using STS2ShowPlayerHandCards.Patching.Models;

namespace STS2ShowPlayerHandCards.Patching.Rules
{
    /// <summary>
    ///     Rule-based patch generator for dynamic patch discovery and application
    ///     Useful for applying patches based on type/method characteristics
    /// </summary>
    public class ModPatchRule
    {
        public string Id { get; init; } = "";
        public Func<Type, bool> TypeSelector { get; init; } = _ => false;
        public Func<MethodInfo, bool> MethodSelector { get; init; } = _ => false;
        public Type? PatchType { get; init; }
        public bool IsCritical { get; init; } = true;
        public string Description { get; init; } = "";

        /// <summary>
        ///     Generate patches based on the rule by scanning an assembly
        /// </summary>
        public ModPatchInfo[] GeneratePatches(Assembly assembly)
        {
            if (PatchType == null)
                throw new InvalidOperationException("PatchType must be set before generating patches");

            var types = assembly.GetTypes().Where(TypeSelector);

            return (from type in types
                let methods =
                    type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                    BindingFlags.NonPublic).Where(MethodSelector)
                from method in methods
                select new ModPatchInfo($"{Id}_{type.Name}_{method.Name}", type, method.Name, PatchType, IsCritical,
                    $"{Description} -> {type.Name}.{method.Name}")).ToArray();
        }

        /// <summary>
        ///     Generate patches by scanning multiple assemblies
        /// </summary>
        public ModPatchInfo[] GeneratePatches(params ReadOnlySpan<Assembly> assemblies)
        {
            var result = new List<ModPatchInfo>();
            foreach (var assembly in assemblies)
                result.AddRange(GeneratePatches(assembly));
            return [..result];
        }

        public override string ToString()
        {
            return $"Rule: {Id} - {Description}";
        }
    }

    /// <summary>
    ///     Fluent builder for PatchRule
    /// </summary>
    public class PatchRuleBuilder
    {
        private string _description = "";
        private string _id = "";
        private bool _isCritical = true;
        private Func<MethodInfo, bool> _methodSelector = _ => false;
        private Type? _patchType;
        private Func<Type, bool> _typeSelector = _ => false;

        public static PatchRuleBuilder Create(string id)
        {
            return new() { _id = id };
        }

        public PatchRuleBuilder ForTypes(Func<Type, bool> selector)
        {
            _typeSelector = selector;
            return this;
        }

        public PatchRuleBuilder ForMethods(Func<MethodInfo, bool> selector)
        {
            _methodSelector = selector;
            return this;
        }

        public PatchRuleBuilder WithPatch(Type patchType)
        {
            _patchType = patchType;
            return this;
        }

        public PatchRuleBuilder Critical(bool isCritical = true)
        {
            _isCritical = isCritical;
            return this;
        }

        public PatchRuleBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        public ModPatchRule Build()
        {
            return new()
            {
                Id = _id,
                TypeSelector = _typeSelector,
                MethodSelector = _methodSelector,
                PatchType = _patchType,
                IsCritical = _isCritical,
                Description = _description,
            };
        }
    }
}
