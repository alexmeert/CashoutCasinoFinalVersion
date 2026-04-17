using Godot;
using System.Collections.Generic;

namespace CashoutCasino.Weapon
{
	public abstract partial class HitscanWeapon : Weapon
	{
		[Export] public float range = 100f;

		public Color TrailColor = new Color(1f, 0.95f, 0.6f, 1f);

		// Accumulates per-target damage within a single Fire() call so multi-pellet
		// weapons (shotgun) produce one popup per target instead of one per pellet.
		private readonly Dictionary<ulong, (CashoutCasino.Character.Character target, float damage, bool isHeadshot, Vector3 pos)> _pendingPopups = new();
		private bool _popupFlushQueued;

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

				// Occlusion check: verify no wall sits between shooter and the hitbox.
				var wallCheck = PhysicsRayQueryParameters3D.Create(origin, hitPoint);
				wallCheck.CollideWithAreas = false;
				wallCheck.CollideWithBodies = true;
				wallCheck.CollisionMask = 1; // layer 1 — walls/environment only
				wallCheck.Exclude = new Godot.Collections.Array<Rid> { owner.GetRid(), hc.GetRid() };
				var wallHit = spaceState.IntersectRay(wallCheck);
				if (wallHit.Count > 0)
				{
					// Wall blocks line-of-sight — cancel the hit and end the trail at the wall.
					hitPoint = (Vector3)wallHit["position"];
					hitCharacter = null;
					isHeadshot = false;
				}
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

				if (!hitCharacter.IsDead)
				{
					ulong id = hitCharacter.GetInstanceId();
					Vector3 popupPos = hitCharacter.GlobalPosition + Vector3.Up * 2.8f;
					if (_pendingPopups.TryGetValue(id, out var existing))
						_pendingPopups[id] = (hitCharacter, existing.damage + damage, existing.isHeadshot || isHeadshot, existing.pos);
					else
						_pendingPopups[id] = (hitCharacter, damage, isHeadshot, popupPos);

					if (!_popupFlushQueued)
					{
						_popupFlushQueued = true;
						Callable.From(() => FlushDamagePopups(owner)).CallDeferred();
					}
				}
			}

			// Trail starts at the muzzle if provided, otherwise just ahead of the camera.
			Vector3 trailStart = trailFrom ?? (origin + direction * 0.5f);
			SpawnTrail(trailStart, hitPoint, owner);
		}

		private void FlushDamagePopups(CashoutCasino.Character.Character owner)
		{
			foreach (var entry in _pendingPopups.Values)
			{
				if (IsInstanceValid(entry.target) && !entry.target.IsDead)
					SpawnDamagePopup(entry.pos, entry.damage, entry.isHeadshot, owner);
			}
			_pendingPopups.Clear();
			_popupFlushQueued = false;
		}

		// Spawns a floating damage number above the hit player — local client only.
		private static void SpawnDamagePopup(Vector3 worldPos, float damage, bool isHeadshot, CashoutCasino.Character.Character owner)
		{
			var node = new Node3D();
			var label = new Label3D
			{
				Text        = isHeadshot ? $"{(int)damage}!" : $"{(int)damage}",
				FontSize    = 72,
				Billboard   = BaseMaterial3D.BillboardModeEnum.Enabled,
				NoDepthTest = true,
				Modulate    = isHeadshot ? new Color(1f, 0.85f, 0f) : new Color(1f, 1f, 1f),
				Shaded      = false,
			};
			node.AddChild(label);
			// Must be in the tree before GlobalPosition can be set.
			(owner.GetParent() ?? owner.GetTree().CurrentScene).AddChild(node);
			node.GlobalPosition = worldPos;

			var tween = node.CreateTween();
			// Float upward
			tween.TweenProperty(node, "position", node.Position + Vector3.Up * 1.5f, 1.0f)
				 .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			// Fade out in parallel, delayed slightly
			tween.Parallel().TweenProperty(label, "modulate", label.Modulate with { A = 0f }, 0.7f)
				 .SetDelay(0.3f);
			tween.TweenCallback(Callable.From(node.QueueFree));
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
