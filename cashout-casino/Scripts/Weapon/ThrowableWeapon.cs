using Godot;
using System;
using CashoutCasino.Projectile;

namespace CashoutCasino.Weapon
{
	public abstract partial class ThrowableWeapon : Weapon
	{
		[Export] public PackedScene projectileScene;
		[Export] public float throwForce = 10f;

		public override bool Fire(Vector3 direction, CashoutCasino.Character.Character owner)
		{
			if (!TryStartFire(owner)) return false;
			currentAmmo -= ammoCost;
			throw new NotImplementedException();
		}
	}
}
