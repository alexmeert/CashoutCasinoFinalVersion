using Godot;
using CashoutCasino.Economy;

namespace CashoutCasino.Weapon
{
	public enum WeaponKind { Rifle, Shotgun, Pistol, Other }

	public abstract partial class Weapon : Node3D
	{
		public virtual WeaponKind Kind => WeaponKind.Other;
		public virtual string DisplayName => Name;
		public virtual bool CanInterruptReload => false;

		public Camera3D FireCamera;

		[Export] public Marker3D Muzzle;
		[Export] public float fireRate = 0.1f;
		[Export] public int ammoCost = 1;
		[Export] public int maxAmmo = 100;
		[Export] public float damagePerHit = 10f;

		public int currentAmmo;
		public int magSize  = 30;
		public int currentMag;
		public bool IsReloading { get; protected set; }

		protected ulong lastFireTime;
		protected CashoutCasino.Character.Character owner;

		public virtual CurrencyEconomy.CostType FireCostType => CurrencyEconomy.CostType.Other;
		public virtual bool HoldToFire => false;

		public abstract bool Fire(Vector3 direction, CashoutCasino.Character.Character owner);

		public virtual void Reload() { }
		public virtual void Equip(CashoutCasino.Character.Character newOwner) => owner = newOwner;
		public virtual void Unequip() => owner = null;
		public virtual void SyncAmmoDisplay(int amount) => currentAmmo = amount;
		public int GetAmmoCount() => currentAmmo;

		public bool CanFire()
		{
			ulong now = Time.GetTicksMsec();
			return now - lastFireTime >= (ulong)(fireRate * 1000.0);
		}

		protected bool TryStartFire(CashoutCasino.Character.Character weaponOwner)
		{
			if (!CanFire() || IsReloading || currentMag <= 0)
				return false;

			owner = weaponOwner;
			lastFireTime = Time.GetTicksMsec();
			currentMag--;
			return true;
		}

		// Returns true if more reload steps remain (shell-by-shell weapons).
		public virtual bool FinishReloadStep()
		{
			currentMag  = magSize;
			IsReloading = false;
			return false;
		}

		public virtual bool StartReload()
		{
			if (IsReloading || currentMag >= magSize) return false;
			IsReloading = true;
			return true;
		}

		public virtual void CancelReload()
		{
			IsReloading = false;
		}

		protected bool TrySpawnBulletFromMuzzle(
			PackedScene bulletScene,
			Vector3 worldDirection,
			CashoutCasino.Character.Character shooter,
			float damage,
			float projectileSpeed,
			Marker3D muzzleOverride = null)
		{
			worldDirection = worldDirection.Normalized();

			if (bulletScene == null || shooter == null)
				return false;

			var sceneRoot = shooter.GetTree()?.CurrentScene;
			if (sceneRoot == null)
				return false;

			Marker3D muzzle = muzzleOverride ?? Muzzle;

			Vector3 origin = muzzle != null
				? muzzle.GlobalPosition
				: shooter.GlobalPosition + Vector3.Up * 1.6f;

			var instantiated = bulletScene.Instantiate();
			if (instantiated is not Projectile.Projectile proj)
			{
				instantiated.QueueFree();
				GD.PushError("Bullet scene root must extend Projectile.");
				return false;
			}

			proj.baseDamage = damage;
			proj.speed = projectileSpeed;

			// Pass the camera so bullet hits can show the health bar
			proj.ShooterCamera = FireCamera;

			sceneRoot.AddChild(proj);

			Vector3 up = Mathf.Abs(worldDirection.Dot(Vector3.Up)) > 0.99f
				? Vector3.Right
				: Vector3.Up;

			proj.GlobalTransform = new Transform3D(
				Basis.LookingAt(worldDirection, up),
				origin);

			proj.Launch(worldDirection, shooter);
			return true;
		}

		protected Vector3 GetCameraHitPoint(CashoutCasino.Character.Character owner)
		{
			if (FireCamera == null)
				return owner.GlobalPosition + owner.GlobalTransform.Basis.Z * -10f;

			var space = owner.GetWorld3D().DirectSpaceState;
			Vector3 origin = FireCamera.GlobalPosition;
			Vector3 dir = -FireCamera.GlobalTransform.Basis.Z;

			var query = PhysicsRayQueryParameters3D.Create(origin, origin + dir * 1000f);
			query.Exclude = new Godot.Collections.Array<Rid> { owner.GetRid() };

			var result = space.IntersectRay(query);
			return result.Count > 0
				? (Vector3)result["position"]
				: origin + dir * 1000f;
		}
	}
}
