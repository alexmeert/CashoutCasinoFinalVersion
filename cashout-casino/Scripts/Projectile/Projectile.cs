using Godot;
using System;

namespace CashoutCasino.Projectile
{
	public abstract partial class Projectile : Area3D
	{
		[Export] public float speed = 30f;
		[Export] public float lifetime = 10f;
		[Export] public float baseDamage = 10f;

		protected Vector3 direction = Vector3.Zero;
		protected CashoutCasino.Character.Character owner;
		protected ulong spawnTime = 0;

		// Set by the weapon when spawning so bullet hits can show the health bar
		public Camera3D ShooterCamera;

		public virtual void Launch(Vector3 dir, CashoutCasino.Character.Character projectileOwner)
		{
			direction = dir.Normalized();
			owner = projectileOwner;
			spawnTime = Time.GetTicksMsec();
		}

		public abstract void OnHit(Node3D hitTarget);

		public virtual float ApplyDamage(CashoutCasino.Character.Character target, bool isHeadshot = false)
		{
			target.TakeDamage(baseDamage, owner, isHeadshot);
			return baseDamage;
		}

		public virtual void Despawn()
		{
			QueueFree();
		}

		public override void _PhysicsProcess(double delta)
		{
			if (direction != Vector3.Zero)
				GlobalPosition += direction * speed * (float)delta;
			if (Time.GetTicksMsec() - spawnTime > (ulong)(lifetime * 1000f)) Despawn();
		}
	}
}
