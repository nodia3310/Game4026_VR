using System.Collections.Generic;
using Game4026.Interaction.Melee;
using Game4026.Interaction.Ranged;
using UnityEngine;

namespace Game4026.Bridges
{
    /// <summary>
    /// Drop one into any scene to connect A's weapons to enemy damage without editing A's prefabs or scenes:
    /// adds <see cref="MeleeDamageBridge"/> to every <see cref="MeleeWeapon"/> at start, and
    /// <see cref="ArrowDamageBridge"/> to each arrow right after a <see cref="BowDrawAim"/> shoots it.
    /// Scene searches run only at start and once per shot, never per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageBridgeInstaller : MonoBehaviour
    {
        [SerializeField] private ImpactDamageCurve meleeDamage = ImpactDamageCurve.MeleeDefault;
        [SerializeField] private ImpactDamageCurve arrowDamage = ImpactDamageCurve.ArrowDefault;
        [SerializeField] private bool logInstalls = true;

        private BowDrawAim[] bows = new BowDrawAim[0];

        public ImpactDamageCurve MeleeDamage => meleeDamage;
        public ImpactDamageCurve ArrowDamage => arrowDamage;

        private void Awake()
        {
            // Forgiving reflects for flying weapons (held weapon in the way, no swing needed).
            if (GetComponent<HeldWeaponParryProbe>() == null)
                gameObject.AddComponent<HeldWeaponParryProbe>();
        }

        private void Start() => Refresh();

        private void OnDestroy() => UnsubscribeBows();

        /// <summary>Rescans the scene. Call again if weapons or bows are spawned after start.</summary>
        public void Refresh()
        {
            var weapons = FindObjectsByType<MeleeWeapon>(FindObjectsInactive.Include);
            int melee = AttachMelee(weapons, meleeDamage);
            int arrows = AttachArrows(FindObjectsByType<ArrowProjectile>(FindObjectsInactive.Include), arrowDamage);

            UnsubscribeBows();
            bows = FindObjectsByType<BowDrawAim>(FindObjectsInactive.Include);
            for (int i = 0; i < bows.Length; i++)
                bows[i].Shot += HandleShot;

            if (logInstalls)
                Debug.Log($"[{nameof(DamageBridgeInstaller)}] melee bridges added: {melee}/{weapons.Length}, " +
                          $"existing arrows bridged: {arrows}, bows watched: {bows.Length}.", this);
        }

        private void HandleShot(BowShot _)
        {
            // BowDrawAim instantiates and launches the arrow before raising Shot, in the same frame.
            AttachArrows(FindObjectsByType<ArrowProjectile>(FindObjectsInactive.Exclude), arrowDamage);
        }

        private void UnsubscribeBows()
        {
            for (int i = 0; i < bows.Length; i++)
                if (bows[i] != null)
                    bows[i].Shot -= HandleShot;
        }

        /// <summary>Adds a melee bridge to each weapon that has none. Returns how many were added.</summary>
        public static int AttachMelee(IReadOnlyList<MeleeWeapon> weapons, ImpactDamageCurve curve)
        {
            int added = 0;
            for (int i = 0; i < weapons.Count; i++)
            {
                MeleeWeapon weapon = weapons[i];
                if (weapon == null || weapon.TryGetComponent(out MeleeDamageBridge _))
                    continue;
                weapon.gameObject.AddComponent<MeleeDamageBridge>().Damage = curve;
                added++;
            }
            return added;
        }

        /// <summary>Adds an arrow bridge to each arrow that has none. Returns how many were added.</summary>
        public static int AttachArrows(IReadOnlyList<ArrowProjectile> arrows, ImpactDamageCurve curve)
        {
            int added = 0;
            for (int i = 0; i < arrows.Count; i++)
            {
                ArrowProjectile arrow = arrows[i];
                if (arrow == null || arrow.TryGetComponent(out ArrowDamageBridge _))
                    continue;
                arrow.gameObject.AddComponent<ArrowDamageBridge>().Damage = curve;
                added++;
            }
            return added;
        }
    }
}
