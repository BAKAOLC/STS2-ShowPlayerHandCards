namespace STS2ShowPlayerHandCards.Patching.Models
{
    /// <summary>
    ///     Contains information about a single patch to be applied
    /// </summary>
    public class ModPatchInfo(
        string id,
        Type targetType,
        string methodName,
        Type patchType,
        bool isCritical = true,
        string description = "",
        Type[]? parameterTypes = null)
    {
        public string Id { get; } = id;
        public Type TargetType { get; } = targetType;
        public string MethodName { get; } = methodName;
        public Type PatchType { get; } = patchType;
        public bool IsCritical { get; } = isCritical;
        public Type[]? ParameterTypes { get; } = parameterTypes;

        public string Description { get; } =
            string.IsNullOrEmpty(description) ? $"Patch {targetType.Name}.{methodName}" : description;

        public override string ToString()
        {
            if (ParameterTypes == null) return $"{Id}: {TargetType.Name}.{MethodName} <- {PatchType.Name}";

            var paramNames = ParameterTypes.Length == 0
                ? "no parameters"
                : string.Join(", ", ParameterTypes.Select(p => p.Name));
            return $"{Id}: {TargetType.Name}.{MethodName}({paramNames}) <- {PatchType.Name}";
        }
    }
}
