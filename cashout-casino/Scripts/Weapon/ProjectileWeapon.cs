using Godot;

namespace CashoutCasino.Weapon
{
	public abstract partial class ProjectileWeapon : Weapon
	{
		protected abstract PackedScene ProjectileScene { get; }
		protected abstract float ProjectileSpeed { get; }

		protected virtual Marker3D GetShotMuzzle() => Muzzle;

		protected virtual Vector3 GetProjectileDirection(
			Vector3 direction,
			CashoutCasino.Character.Character owner,
			Marker3D muzzle)
		{
			Vector3 origin = muzzle?.GlobalPosition ?? owner.GlobalPosition;
			Vector3 target = GetCameraHitPoint(owner);
			return (target - origin).Normalized();
		}

		public override void _Ready()
		{
			currentAmmo = maxAmmo;
		}

		public override bool Fire(Vector3 direction, CashoutCasino.Character.Character owner)
		{
			if (!TryStartFire(owner))
				return false;

			Marker3D muzzle = GetShotMuzzle();
			Vector3 fireDirection = GetProjectileDirection(direction, owner, muzzle);

			return TrySpawnBulletFromMuzzle(
				ProjectileScene,
				fireDirection,
				owner,
				damagePerHit,
				ProjectileSpeed,
				muzzle);
		}
	}
}
