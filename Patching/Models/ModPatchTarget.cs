namespace STS2ShowPlayerHandCards.Patching.Models
{
    /// <summary>
    ///     Represents a target for patching (Type + Method combination)
    /// </summary>
    public record ModPatchTarget(Type TargetType, string MethodName, Type[]? ParameterTypes = null)
    {
        public override string ToString()
        {
            if (ParameterTypes == null) return $"{TargetType.Name}.{MethodName}";

            var paramNames = ParameterTypes.Length == 0
                ? "no parameters"
                : string.Join(", ", ParameterTypes.Select(p => p.Name));
            return $"{TargetType.Name}.{MethodName}({paramNames})";
        }
    }
}
