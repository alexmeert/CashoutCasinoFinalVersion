using Godot;
using System;

namespace CashoutCasino.Weapon
{
	/// <summary>
	/// Always-available melee arm. Zero cost, used for special interactions (slot machine, melee hits).
	/// </summary>
	public partial class SlotMachineArm : MeleeWeapon
	{
		public override bool Fire(Vector3 direction, CashoutCasino.Character.Character owner)
		{
			throw new NotImplementedException();
		}
	}
}
