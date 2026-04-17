using Godot;

namespace CashoutCasino.Weapon
{
	public abstract partial class HitscanWeapon : Weapon
	{
		[Export] public float range = 100f;

		public Color TrailColor = new Color(1f, 0.95f, 0.6f, 1f);

		protected void PerformRaycast(Vector3 direction, CashoutCasino.Character.Character owner, Vector3? trailFrom = null)
		{
			Vector3 rayOrigin = FireCamera != null
				? FireCamera.GlobalPosition
				: owner.GlobalPosition + Vector3.Up * 1.6f;

			PerformRaycastFrom(rayOrigin, direction.Normalized(), owner, trailFrom);
		}

		protected void PerformRaycastFrom(Vector3 origin, Vector3 direction, CashoutCasino.Character.Character owner, Vector3? trailFrom = null)
		{
			var spaceState = owner.GetWorld3D().DirectSpaceState;
			Vector3 target = origin + direction * range;

			CashoutCasino.Character.Character hitCharacter = null;
			bool isHeadshot = false;
			Vector3 hitPoint = target;

			// Pass 1: check dedicated hitbox areas (layer 2) for headshot detection
			var hitboxQuery = PhysicsRayQueryParameters3D.Create(origin, target);
			hitboxQuery.CollideWithAreas = true;
			hitboxQuery.CollideWithBodies = false;
			hitboxQuery.CollisionMask = 2;
			var hitboxResult = spaceState.IntersectRay(hitboxQuery);

			if (hitboxResult.Count > 0
				&& hitboxResult["collider"].As<Node>() is Area3D area
				&& area.Name == "Hitbox"
				&& area.GetParent() is CashoutCasino.Character.Character hc
				&& hc != owner)
			{
				hitCharacter = hc;
				isHeadshot = (int)hitboxResult["shape"] == 0; // HeadHitbox is first child
				hitPoint = (Vector3)hitboxResult["position"];
			}

			// Pass 2: fall back to body collision (layer 1) if hitbox missed
			if (hitCharacter == null)
			{
				var bodyQuery = PhysicsRayQueryParameters3D.Create(origin, target);
				bodyQuery.Exclude = new Godot.Collections.Array<Rid> { owner.GetRid() };
				var bodyResult = spaceState.IntersectRay(bodyQuery);

				if (bodyResult.Count > 0)
				{
					hitPoint = (Vector3)bodyResult["position"];
					if (bodyResult["collider"].As<Node>() is CashoutCasino.Character.Character hit && hit != owner)
						hitCharacter = hit;
				}
			}

			if (hitCharacter != null)
			{
				float damage = isHeadshot ? damagePerHit * 2f : damagePerHit;
				hitCharacter.TakeDamage(damage, owner, isHeadshot);

				if (hitCharacter.WorldHealthBar != null)
				{
					hitCharacter.WorldHealthBar.SetLocalCamera(FireCamera);
					hitCharacter.WorldHealthBar.ShowFor(hitCharacter.GetHealth(), hitCharacter.GetMaxHealth());
				}
			}

			// Trail starts at the muzzle if provided, otherwise just ahead of the camera.
			Vector3 trailStart = trailFrom ?? (origin + direction * 0.5f);
			SpawnTrail(trailStart, hitPoint, owner);
		}

		public void SpawnTrail(Vector3 from, Vector3 to, CashoutCasino.Character.Character owner)
		{
			if (from.DistanceTo(to) < 0.6f) return;

			var trail = new BulletTrail();
			trail.TrailColor = TrailColor;
			trail.Init(from, to);
			// Add to owner's parent so it lives in the same 3D space as the player.
			(owner.GetParent() ?? owner.GetTree().CurrentScene).AddChild(trail);
		}

		public override void _Ready()
		{
			currentAmmo = maxAmmo;
			currentMag  = magSize;
		}
	}
}
