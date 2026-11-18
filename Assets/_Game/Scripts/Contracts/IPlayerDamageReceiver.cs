namespace Game4026.Contracts
{
    /// <summary>Implemented by A's player health receiver; called by B's enemy attacks.</summary>
    public interface IPlayerDamageReceiver
    {
        /// <summary>
        /// Receives finite, non-negative damage for one confirmed attack.
        /// B suppresses duplicate contacts; A owns player health and invulnerability.
        /// </summary>
        void ApplyPlayerDamage(float damage);
    }
}
