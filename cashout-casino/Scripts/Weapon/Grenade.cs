using Godot;
using System;

namespace CashoutCasino.Weapon
{
	public partial class Grenade : ThrowableWeapon
	{
		[Export] public float explosionRadius = 4f;
		[Export] public float fuseTime = 2f;
		[Export] public float explosionDamage = 50f;

		public override bool Fire(Vector3 direction, CashoutCasino.Character.Character owner)
		{
			base.Fire(direction, owner);
			throw new NotImplementedException();
		}
	}
}
