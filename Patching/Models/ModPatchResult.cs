namespace STS2ShowPlayerHandCards.Patching.Models
{
    /// <summary>
    ///     Represents the result of a patch application attempt
    /// </summary>
    public class ModPatchResult(
        ModPatchInfo modPatchInfo,
        bool success,
        string errorMessage = "",
        Exception? exception = null)
    {
        public ModPatchInfo ModPatchInfo { get; } = modPatchInfo;
        public bool Success { get; } = success;
        public string ErrorMessage { get; } = errorMessage;
        public Exception? Exception { get; } = exception;

        public static ModPatchResult CreateSuccess(ModPatchInfo modPatchInfo)
        {
            return new(modPatchInfo, true);
        }

        public static ModPatchResult CreateFailure(ModPatchInfo modPatchInfo, string errorMessage,
            Exception? exception = null)
        {
            return new(modPatchInfo, false, errorMessage, exception);
        }

        public override string ToString()
        {
            return Success
                ? $"✓ {ModPatchInfo.Id}"
                : $"✗ {ModPatchInfo.Id}: {ErrorMessage}";
        }
    }
}
