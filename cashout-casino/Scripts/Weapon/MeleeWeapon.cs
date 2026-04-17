using Godot;
using System;

namespace CashoutCasino.Weapon
{
	public abstract partial class MeleeWeapon : Weapon
	{
		[Export] public float reach = 2.0f;
		[Export] public float swingTime = 0.3f;

		public override bool Fire(Vector3 direction, CashoutCasino.Character.Character owner)
		{
			throw new NotImplementedException();
		}
	}
}
