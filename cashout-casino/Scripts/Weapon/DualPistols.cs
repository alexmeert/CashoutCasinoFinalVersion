using Godot;
using CashoutCasino.Economy;

namespace CashoutCasino.Weapon
{
	public partial class DualPistols : HitscanWeapon
	{
		[Export] public Marker3D LeftMuzzle;
		[Export] public Marker3D RightMuzzle;

		private bool leftNext = true;

		public override WeaponKind Kind => WeaponKind.Pistol;
		public override string DisplayName => "Dual Pistols";
		public override CurrencyEconomy.CostType FireCostType => CurrencyEconomy.CostType.ShootPistol;

		public override void _Ready()
		{
			fireRate     = 0.18f;
			ammoCost     = 1;
			damagePerHit = 10f;
			maxAmmo      = 100;
			magSize      = 20;
			base._Ready();
		}

		public override bool Fire(Vector3 direction, CashoutCasino.Character.Character owner)
		{
			if (!TryStartFire(owner)) return false;

			Marker3D muzzle = leftNext ? LeftMuzzle : RightMuzzle;
			leftNext = !leftNext;

			// Hit detection from camera center (crosshair-accurate); trail starts at muzzle.
			PerformRaycast(direction, owner, muzzle?.GlobalPosition);
			return true;
		}
	}
}
