using System.Text.Json;
using Godot;
using FileAccess = Godot.FileAccess;

namespace STS2ShowPlayerHandCards.Utils
{
    /// <summary>
    ///     Unified file operations wrapper for Godot's FileAccess with consistent error handling and logging.
    /// </summary>
    public static class FileOperations
    {
        /// <summary>
        ///     Reads text content from a file with detailed error handling.
        /// </summary>
        public static ReadResult ReadText(string filePath, string? logContext = null)
        {
            var context = logContext ?? "FileOperations";

            try
            {
                if (!FileAccess.FileExists(filePath))
                {
                    Main.Logger.Debug($"[{context}] File not found at '{filePath}'");
                    return new()
                    {
                        Success = false,
                        ErrorMessage = "File not found",
                    };
                }

                using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    var error = FileAccess.GetOpenError();
                    Main.Logger.Error($"[{context}] Failed to open file '{filePath}' (Error: {error})");
                    return new()
                    {
                        Success = false,
                        ErrorCode = error,
                        ErrorMessage = $"Failed to open file (Error: {error})",
                    };
                }

                var content = file.GetAsText();

                if (string.IsNullOrWhiteSpace(content))
                {
                    Main.Logger.Warn($"[{context}] File '{filePath}' is empty");
                    return new()
                    {
                        Success = false,
                        Content = content,
                        ErrorMessage = "File is empty",
                    };
                }

                Main.Logger.Debug($"[{context}] Successfully read file '{filePath}' ({content.Length} characters)");
                return new()
                {
                    Success = true,
                    Content = content,
                };
            }
            catch (Exception ex)
            {
                Main.Logger.Error($"[{context}] Unexpected error reading file '{filePath}': {ex.Message}");
                return new()
                {
                    Success = false,
                    ErrorMessage = $"Unexpected error: {ex.Message}",
                };
            }
        }

        /// <summary>
        ///     Writes text content to a file with detailed error handling.
        /// </summary>
        public static WriteResult WriteText(string filePath, string content, string? logContext = null)
        {
            var context = logContext ?? "FileOperations";

            try
            {
                EnsureDirectoryExists(filePath);

                using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
                if (file == null)
                {
                    var error = FileAccess.GetOpenError();
                    Main.Logger.Error($"[{context}] Failed to open file '{filePath}' for writing (Error: {error})");
                    return new()
                    {
                        Success = false,
                        ErrorCode = error,
                        ErrorMessage = $"Failed to open file for writing (Error: {error})",
                    };
                }

                file.StoreString(content);
                Main.Logger.Debug($"[{context}] Successfully wrote to file '{filePath}' ({content.Length} characters)");
                return new()
                {
                    Success = true,
                };
            }
            catch (Exception ex)
            {
                Main.Logger.Error($"[{context}] Unexpected error writing to file '{filePath}': {ex.Message}");
                return new()
                {
                    Success = false,
                    ErrorMessage = $"Unexpected error: {ex.Message}",
                };
            }
        }

        /// <summary>
        ///     Ensures the directory for a file path exists.
        /// </summary>
        private static void EnsureDirectoryExists(string filePath)
        {
            var lastSlash = filePath.LastIndexOf('/');
            if (lastSlash <= 0) return;

            var directory = filePath[..lastSlash];
            if (string.IsNullOrEmpty(directory)) return;
            if (DirAccess.DirExistsAbsolute(directory)) return;

            var error = DirAccess.MakeDirRecursiveAbsolute(directory);
            if (error != Error.Ok)
                Main.Logger.Warn($"Failed to create directory '{directory}' (Error: {error})");
        }

        /// <summary>
        ///     Reads and deserializes JSON content from a file.
        /// </summary>
        public static JsonResult<T> ReadJson<T>(string filePath, JsonSerializerOptions? options = null,
            string? logContext = null)
        {
            var context = logContext ?? "FileOperations";
            var readResult = ReadText(filePath, context);

            if (!readResult.Success || readResult.Content == null)
                return new()
                {
                    Success = false,
                    ErrorMessage = readResult.ErrorMessage,
                };

            try
            {
                var data = JsonSerializer.Deserialize<T>(readResult.Content, options);

                if (data == null)
                {
                    Main.Logger.Error($"[{context}] Deserialization resulted in null object for file '{filePath}'");
                    return new()
                    {
                        Success = false,
                        ErrorMessage = "Deserialization resulted in null object",
                    };
                }

                Main.Logger.Debug($"[{context}] Successfully deserialized JSON from '{filePath}'");
                return new()
                {
                    Success = true,
                    Data = data,
                };
            }
            catch (JsonException ex)
            {
                Main.Logger.Error($"[{context}] JSON parsing error in file '{filePath}': {ex.Message}");
                return new()
                {
                    Success = false,
                    ErrorMessage = $"JSON parsing error: {ex.Message}",
                };
            }
            catch (Exception ex)
            {
                Main.Logger.Error($"[{context}] Unexpected error deserializing file '{filePath}': {ex.Message}");
                return new()
                {
                    Success = false,
                    ErrorMessage = $"Unexpected error: {ex.Message}",
                };
            }
        }

        /// <summary>
        ///     Serializes and writes JSON content to a file.
        /// </summary>
        public static WriteResult WriteJson<T>(string filePath, T data, JsonSerializerOptions? options = null,
            string? logContext = null)
        {
            var context = logContext ?? "FileOperations";

            try
            {
                var jsonContent = JsonSerializer.Serialize(data, options);
                return WriteText(filePath, jsonContent, context);
            }
            catch (JsonException ex)
            {
                Main.Logger.Error($"[{context}] JSON serialization error: {ex.Message}");
                return new()
                {
                    Success = false,
                    ErrorMessage = $"JSON serialization error: {ex.Message}",
                };
            }
            catch (Exception ex)
            {
                Main.Logger.Error($"[{context}] Unexpected error serializing data: {ex.Message}");
                return new()
                {
                    Success = false,
                    ErrorMessage = $"Unexpected error: {ex.Message}",
                };
            }
        }

        /// <summary>
        ///     Checks if a file exists.
        /// </summary>
        public static bool FileExists(string filePath)
        {
            return FileAccess.FileExists(filePath);
        }

        /// <summary>
        ///     Deletes a file with detailed error handling.
        /// </summary>
        public static WriteResult DeleteFile(string filePath, string? logContext = null)
        {
            var context = logContext ?? "FileOperations";

            try
            {
                if (!FileAccess.FileExists(filePath))
                {
                    Main.Logger.Debug($"[{context}] File '{filePath}' does not exist, nothing to delete");
                    return new() { Success = true };
                }

                var pathParts = filePath.Split('/');
                var directory = pathParts.Length > 1 ? string.Join("/", pathParts[..^1]) : "user://";

                var dirAccess = DirAccess.Open(directory);
                if (dirAccess == null)
                {
                    Main.Logger.Error($"[{context}] Failed to access directory '{directory}' for file deletion");
                    return new()
                    {
                        Success = false,
                        ErrorMessage = $"Failed to access directory '{directory}'",
                    };
                }

                var error = dirAccess.Remove(filePath);
                if (error != Error.Ok)
                {
                    Main.Logger.Error($"[{context}] Failed to delete file '{filePath}' (Error: {error})");
                    return new()
                    {
                        Success = false,
                        ErrorCode = error,
                        ErrorMessage = $"Failed to delete file (Error: {error})",
                    };
                }

                Main.Logger.Info($"[{context}] Successfully deleted file '{filePath}'");
                return new() { Success = true };
            }
            catch (Exception ex)
            {
                Main.Logger.Error($"[{context}] Unexpected error deleting file '{filePath}': {ex.Message}");
                return new()
                {
                    Success = false,
                    ErrorMessage = $"Unexpected error: {ex.Message}",
                };
            }
        }

        /// <summary>
        ///     Result of a file read operation.
        /// </summary>
        public class ReadResult
        {
            public bool Success { get; init; }
            public string? Content { get; init; }
            public Error? ErrorCode { get; init; }
            public string? ErrorMessage { get; init; }
        }

        /// <summary>
        ///     Result of a file write operation.
        /// </summary>
        public class WriteResult
        {
            public bool Success { get; init; }
            public Error? ErrorCode { get; init; }
            public string? ErrorMessage { get; init; }
        }

        /// <summary>
        ///     Result of a JSON deserialization operation.
        /// </summary>
        public class JsonResult<T>
        {
            public bool Success { get; init; }
            public T? Data { get; init; }
            public string? ErrorMessage { get; init; }
        }
    }
}
