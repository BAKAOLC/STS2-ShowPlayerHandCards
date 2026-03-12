using System.Text.Json.Nodes;

namespace STS2ShowPlayerHandCards.Utils.Persistence.Migration
{
    /// <summary>
    ///     Interface for data migrations
    /// </summary>
    public interface IMigration
    {
        /// <summary>
        ///     The version this migration upgrades FROM
        /// </summary>
        int FromVersion { get; }

        /// <summary>
        ///     The version this migration upgrades TO
        /// </summary>
        int ToVersion { get; }

        /// <summary>
        ///     Perform the migration on the JSON data
        /// </summary>
        /// <param name="data">The JSON data to migrate</param>
        /// <returns>True if migration succeeded, false otherwise</returns>
        bool Migrate(JsonObject data);
    }
}
