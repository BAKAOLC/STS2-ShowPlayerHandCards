using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using STS2ShowPlayerHandCards.Patching.Models;

namespace STS2ShowPlayerHandCards.Patching.Core
{
    /// <summary>
    ///     Manages Harmony patch application and lifecycle
    /// </summary>
    public class ModPatcher(string patcherId, Logger logger, string patcherName = "")
    {
        private readonly Harmony _harmony = new(patcherId);

        private readonly string _logPrefix =
            string.IsNullOrEmpty(patcherName) ? "[Patcher] " : $"[Patcher - {patcherName}] ";

        private readonly Dictionary<string, bool> _patchedStatus = [];
        private readonly List<ModPatchInfo> _registeredPatches = [];

        public string PatcherId => patcherId;
        public string PatcherName => patcherName;
        public int RegisteredPatchCount => _registeredPatches.Count;
        public int AppliedPatchCount => _patchedStatus.Count(kvp => kvp.Value);
        public bool IsApplied { get; private set; }

        public void RegisterPatch(ModPatchInfo modPatchInfo)
        {
            if (IsApplied)
            {
                logger.Error(
                    $"{_logPrefix}Cannot register patch '{modPatchInfo.Id}': Patches have already been applied");
                throw new InvalidOperationException("Cannot register patches after they have been applied");
            }

            if (_registeredPatches.Any(p => p.Id == modPatchInfo.Id))
            {
                logger.Warn($"{_logPrefix}Patch '{modPatchInfo.Id}' already registered, skipping duplicate");
                return;
            }

            ValidatePatchType(modPatchInfo);

            _registeredPatches.Add(modPatchInfo);
            logger.Debug($"{_logPrefix}Registered patch: {modPatchInfo.Id} - {modPatchInfo.Description}");
        }

        public void RegisterPatches(params ReadOnlySpan<ModPatchInfo> patches)
        {
            foreach (var patch in patches) RegisterPatch(patch);
        }

        public bool PatchAll()
        {
            if (IsApplied)
            {
                logger.Warn($"{_logPrefix}Patches have already been applied, skipping");
                return true;
            }

            logger.Info($"{_logPrefix}Applying {_registeredPatches.Count} patches...");
            var results = new ModPatchResult[_registeredPatches.Count];
            for (var i = 0; i < _registeredPatches.Count; i++)
                results[i] = ApplyPatch(_registeredPatches[i]);
            var success = ProcessPatchResults(results);

            if (success)
            {
                IsApplied = true;
                logger.Info($"{_logPrefix}All patches applied successfully");
            }
            else
            {
                logger.Error($"{_logPrefix}Critical patch(es) failed, rolling back all patches...");
                UnpatchAll();
                IsApplied = false;
            }

            return success;
        }

        public void UnpatchAll()
        {
            if (_registeredPatches.Count == 0)
            {
                logger.Debug($"{_logPrefix}No patches registered, skipping unpatch");
                return;
            }

            var appliedCount =
                _registeredPatches.Count(patchInfo => _patchedStatus.GetValueOrDefault(patchInfo.Id, false));

            if (appliedCount == 0)
            {
                logger.Debug($"{_logPrefix}No patches applied, skipping unpatch");
                IsApplied = false;
                return;
            }

            logger.Info($"{_logPrefix}Removing {appliedCount} applied patches...");

            foreach (var patchInfo in _registeredPatches.Where(patchInfo =>
                         _patchedStatus.GetValueOrDefault(patchInfo.Id, false)))
                try
                {
                    var originalMethod = GetOriginalMethod(patchInfo);
                    if (originalMethod == null) continue;
                    _harmony.Unpatch(originalMethod, HarmonyPatchType.All, _harmony.Id);
                    _patchedStatus[patchInfo.Id] = false;
                    logger.Info($"{_logPrefix}✓ Removed patch: {patchInfo.Id}");
                }
                catch (Exception ex)
                {
                    logger.Error($"{_logPrefix}✗ Failed to remove patch: {patchInfo.Id} - {ex.Message}");
                }

            IsApplied = false;
            logger.Info($"{_logPrefix}All patches removed");
        }

        private ModPatchResult ApplyPatch(ModPatchInfo modPatchInfo)
        {
            try
            {
                var originalMethod = GetOriginalMethod(modPatchInfo);
                if (originalMethod == null)
                {
                    _patchedStatus[modPatchInfo.Id] = false;
                    return ModPatchResult.CreateFailure(
                        modPatchInfo,
                        $"Target method not found: {modPatchInfo.TargetType.Name}.{modPatchInfo.MethodName}"
                    );
                }

                var prefix = GetPatchMethod(modPatchInfo.PatchType, "Prefix");
                var postfix = GetPatchMethod(modPatchInfo.PatchType, "Postfix");
                var transpiler = GetPatchMethod(modPatchInfo.PatchType, "Transpiler");
                var finalizer = GetPatchMethod(modPatchInfo.PatchType, "Finalizer");

                if (prefix == null && postfix == null && transpiler == null && finalizer == null)
                {
                    _patchedStatus[modPatchInfo.Id] = false;
                    return ModPatchResult.CreateFailure(
                        modPatchInfo,
                        $"No valid patch methods found in {modPatchInfo.PatchType.Name}"
                    );
                }

                _harmony.Patch(
                    originalMethod,
                    prefix != null ? new HarmonyMethod(prefix) : null,
                    postfix != null ? new HarmonyMethod(postfix) : null,
                    transpiler != null ? new HarmonyMethod(transpiler) : null,
                    finalizer != null ? new HarmonyMethod(finalizer) : null
                );

                _patchedStatus[modPatchInfo.Id] = true;
                return ModPatchResult.CreateSuccess(modPatchInfo);
            }
            catch (Exception ex)
            {
                _patchedStatus[modPatchInfo.Id] = false;
                return ModPatchResult.CreateFailure(modPatchInfo, ex.Message, ex);
            }
        }

        private bool ProcessPatchResults(ReadOnlySpan<ModPatchResult> results)
        {
            var successCount = 0;
            var failureCount = 0;
            var criticalFailureCount = 0;

            var sortedResults = results.ToArray()
                .OrderBy(r => r.Success)
                .ThenByDescending(r => r.ModPatchInfo.IsCritical)
                .ThenBy(r => r.ModPatchInfo.Id);

            foreach (var result in sortedResults)
            {
                var importance = result.ModPatchInfo.IsCritical ? "Critical" : "Optional";

                if (result.Success)
                {
                    successCount++;
                    logger.Info($"{_logPrefix}[{importance}] {result.ModPatchInfo.Id} - Success ✓");
                }
                else
                {
                    failureCount++;
                    if (result.ModPatchInfo.IsCritical)
                        criticalFailureCount++;

                    logger.Error($"{_logPrefix}[{importance}] {result.ModPatchInfo.Id} - Failed ✗");
                    logger.Error($"{_logPrefix}  Description: {result.ModPatchInfo.Description}");
                    logger.Error($"{_logPrefix}  Error: {result.ErrorMessage}");

                    if (result.Exception != null) logger.Error($"{_logPrefix}  Exception: {result.Exception}");
                }
            }

            logger.Info($"{_logPrefix}Patch application complete: {successCount}/{results.Length} succeeded");

            if (failureCount > 0) logger.Warn($"{_logPrefix}{failureCount} patch(es) failed");

            if (criticalFailureCount == 0) return true;
            logger.Error($"{_logPrefix}{criticalFailureCount} critical patch(es) failed, mod loading blocked");
            return false;
        }

        private static MethodInfo? GetOriginalMethod(ModPatchInfo modPatchInfo)
        {
            if (modPatchInfo.ParameterTypes != null)
                return modPatchInfo.TargetType.GetMethod(
                    modPatchInfo.MethodName,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    modPatchInfo.ParameterTypes,
                    null
                );

            return modPatchInfo.TargetType.GetMethod(
                modPatchInfo.MethodName,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );
        }

        private static MethodInfo? GetPatchMethod(Type patchType, string methodName)
        {
            return patchType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );
        }

        /// <summary>
        ///     Validate that patch type implements IPatchMethod interface (optional but recommended)
        /// </summary>
        private void ValidatePatchType(ModPatchInfo modPatchInfo)
        {
            var patchType = modPatchInfo.PatchType;
            var implementsIPatchMethod = patchType.GetInterfaces()
                .Any(i => i.Name == nameof(IPatchMethod) ||
                          (i.IsGenericType && i.GetGenericTypeDefinition().GetInterfaces()
                              .Any(gi => gi.Name == nameof(IPatchMethod))));

            if (!implementsIPatchMethod)
                logger.Warn(
                    $"{_logPrefix}Patch type '{patchType.Name}' does not implement IPatchMethod interface. " +
                    "Consider implementing IPatchMethod interfaces for better type safety and IDE support.");
        }
    }
}
