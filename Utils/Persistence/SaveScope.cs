namespace STS2ShowPlayerHandCards.Utils.Persistence
{
    /// <summary>
    ///     Defines the scope of save data storage
    /// </summary>
    public enum SaveScope
    {
        /// <summary>
        ///     Global scope - data is shared across all profiles
        /// </summary>
        Global,

        /// <summary>
        ///     Profile scope - data is specific to the current profile
        /// </summary>
        Profile,
    }
}
