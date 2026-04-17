using Godot;
using CashoutCasino.Economy;

namespace CashoutCasino.Weapon
{
	public partial class AssaultRifle : HitscanWeapon
	{
		public override WeaponKind Kind => WeaponKind.Rifle;
		public override string DisplayName => "Assault Rifle";
		public override CurrencyEconomy.CostType FireCostType => CurrencyEconomy.CostType.ShootAR;
		public override bool HoldToFire => true;

		public override void _Ready()
		{
			fireRate     = 0.09f;
			ammoCost     = 1;
			damagePerHit = 15f;
			maxAmmo      = 100;
			magSize      = 30;
			base._Ready();
		}

		public override bool Fire(Vector3 direction, CashoutCasino.Character.Character owner)
		{
			if (!TryStartFire(owner)) return false;
			// Trail starts at muzzle; hit detection uses camera-center raycast.
			PerformRaycast(direction, owner, Muzzle?.GlobalPosition);
			return true;
		}
	}
}
