using Godot;
using System;

namespace CashoutCasino.Projectile
{
    /// <summary>
    /// Represents one pellet/fragment for a shotgun or dice projectile effect.
    /// </summary>
    public partial class DiceProjectile : Projectile
    {
        public override void OnHit(Node3D hitTarget)
        {
            if (hitTarget is CashoutCasino.Character.Character c)
            {
                ApplyDamage(c);
            }
            Despawn();
        }
    }
}
