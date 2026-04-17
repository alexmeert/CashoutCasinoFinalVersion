using Godot;
using CashoutCasino.Economy;

namespace CashoutCasino.Weapon
{
	public partial class Shotgun : HitscanWeapon
	{
		[Export] public int pelletCount = 8;
		[Export] public float spreadAngle = 12f;

		private readonly RandomNumberGenerator rng = new RandomNumberGenerator();

		public override WeaponKind Kind => WeaponKind.Shotgun;
		public override CurrencyEconomy.CostType FireCostType => CurrencyEconomy.CostType.ShootShotgun;

		public override bool CanInterruptReload => true;

		public override void _Ready()
		{
			fireRate     = 1.5f;
			ammoCost     = 3;
			damagePerHit = 12f;
			maxAmmo      = 40;
			magSize      = 8;
			TrailColor   = new Color(1f, 0.2f, 0.1f, 1f);

			base._Ready();
			rng.Randomize();
		}

		public override bool FinishReloadStep()
		{
			currentMag = Mathf.Min(currentMag + 1, magSize);
			bool moreNeeded = currentMag < magSize;
			if (!moreNeeded) IsReloading = false;
			return moreNeeded;
		}

		public override bool Fire(Vector3 direction, CashoutCasino.Character.Character owner)
		{
			if (!TryStartFire(owner))
				return false;

			Vector3 rayOrigin = Muzzle != null
				? Muzzle.GlobalPosition
				: owner.GlobalPosition + Vector3.Up * 1.6f;

			float spreadRad = Mathf.DegToRad(spreadAngle);
			for (int i = 0; i < pelletCount; i++)
			{
				Vector3 pelletDir = direction
					.Rotated(Vector3.Up, rng.RandfRange(-spreadRad, spreadRad))
					.Rotated(Vector3.Right, rng.RandfRange(-spreadRad, spreadRad))
					.Normalized();

				PerformRaycastFrom(rayOrigin, pelletDir, owner);
			}

			return true;
		}
	}
}
