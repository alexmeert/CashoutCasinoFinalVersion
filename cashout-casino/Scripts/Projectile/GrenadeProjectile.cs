using Godot;
using System;

namespace CashoutCasino.Projectile
{
	public partial class GrenadeProjectile : Projectile
	{
		[Export] public float explosionRadius = 4f;
		[Export] public float explosionDamage = 50f;

		private float fuseTimer = 0f;

		public override void Launch(Vector3 dir, CashoutCasino.Character.Character projectileOwner)
		{
			base.Launch(dir, projectileOwner);
			fuseTimer = 0f;
		}

		public override void _PhysicsProcess(double delta)
		{
			base._PhysicsProcess(delta);
			fuseTimer += (float)delta;
		}

		public override void OnHit(Node3D hitTarget)
		{
			throw new NotImplementedException();
		}
	}
}
