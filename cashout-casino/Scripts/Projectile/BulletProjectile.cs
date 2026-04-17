using Godot;

namespace CashoutCasino.Projectile
{
	public partial class BulletProjectile : Projectile
	{
		public override void _Ready()
		{
			lifetime = Mathf.Min(lifetime, 4f);
			Monitoring = true;
			Monitorable = false;

			// AreaShapeEntered gives us the exact shape index (0=head, 1=body) within the Hitbox area.
			AreaShapeEntered += OnAreaShapeEntered;

			// Keep BodyEntered as a fallback for non-character physics bodies (walls etc.)
			BodyEntered += OnBodyEntered;
		}

		private void OnAreaShapeEntered(Rid areaRid, Area3D area, long areaShapeIndex, long localShapeIndex)
		{
			Node parent = area.GetParent();
			if (parent is CashoutCasino.Character.Character c)
			{
				if (owner != null && c == owner) return;
				// areaShapeIndex 0 = HeadHitbox (first CollisionShape3D child of Hitbox)
				// areaShapeIndex 1 = BodyHitbox
				bool isHeadshot = areaShapeIndex == 0;
				HitCharacter(c, isHeadshot);
			}
		}

		private void OnBodyEntered(Node3D body)
		{
			// Ignore the shooter
			if (owner != null && body == owner) return;

			// If somehow a Character body is hit directly, handle it
			if (body is CashoutCasino.Character.Character c)
			{
				HitCharacter(c);
				return;
			}

			// Hit a wall or other static object — just despawn
			Despawn();
		}

		private void HitCharacter(CashoutCasino.Character.Character c, bool isHeadshot = false)
		{
			float damage = isHeadshot ? baseDamage * 2f : baseDamage;
			c.TakeDamage(damage, owner, isHeadshot);

			if (c.WorldHealthBar != null)
			{
				c.WorldHealthBar.SetLocalCamera(ShooterCamera);
				c.WorldHealthBar.ShowFor(c.GetHealth(), c.GetMaxHealth());
			}

			Despawn();
		}

		public override void OnHit(Node3D hitTarget)
		{
			if (hitTarget is CashoutCasino.Character.Character c)
				HitCharacter(c);
			else
				Despawn();
		}
	}
}
