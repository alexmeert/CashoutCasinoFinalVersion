using Godot;

namespace CashoutCasino.Character
{
	public partial class AIEnemy : Character
	{
		[Export] public float detectionRadius = 15f;
		[Export] public float fireRate = 1.2f;
		[Export] public float damagePerShot = 10f;
		[Export] public float gravity = 20f;
		[Export] public float respawnTime = 15f;

		private float verticalVelocity = 0f;
		private float fireTimer = 0f;
		private Character target;
		private Vector3 spawnPosition;

		public override void _Ready()
		{
			base._Ready();
			spawnPosition = GlobalPosition;

			var whb = GetNodeOrNull<UI.WorldHealthBar>("WorldHealthBar");
			if (whb != null)
				WorldHealthBar = whb;
		}

		public override void _PhysicsProcess(double delta)
		{
			// Only the server runs AI logic
			if (!Multiplayer.IsServer()) return;
			if (isDead) return;

			float dt = (float)delta;

			if (IsOnFloor())
				verticalVelocity = -0.5f;
			else
				verticalVelocity -= gravity * dt;

			Velocity = new Vector3(0f, verticalVelocity, 0f);
			MoveAndSlide();

			target = FindNearestPlayer();
			if (target == null) return;

			Vector3 dir = target.GlobalPosition - GlobalPosition;
			dir.Y = 0f;
			if (dir.LengthSquared() > 0.01f)
				LookAt(GlobalPosition + dir, Vector3.Up);

			fireTimer -= dt;
			if (fireTimer <= 0f)
			{
				FireAtTarget();
				fireTimer = fireRate;
			}
		}

		public override void TakeDamage(float damage, Character attacker = null, bool isHeadshot = false)
		{
			// Route to server — if already on server apply directly, otherwise RPC.
			if (Multiplayer.IsServer())
			{
				base.TakeDamage(damage, attacker);
				Rpc(MethodName.SyncHealth, currentHealth);
			}
			else
			{
				// Pass attacker peer ID so server can credit the kill.
				long attackerPeerId = attacker != null ? attacker.GetMultiplayerAuthority() : 0;
				RpcId(1, MethodName.ServerApplyDamage, damage, attackerPeerId);
			}
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void ServerApplyDamage(float damage, long attackerPeerId)
		{
			if (!Multiplayer.IsServer() || isDead) return;
			// Resolve attacker node from peer ID so OnDeath can credit the kill.
			Character attacker = null;
			if (attackerPeerId > 0)
			{
				foreach (var node in GetTree().GetNodesInGroup("Player"))
					if (node is Character c && c.GetMultiplayerAuthority() == attackerPeerId)
					{ attacker = c; break; }
			}
			base.TakeDamage(damage, attacker);
			Rpc(MethodName.SyncHealth, currentHealth);
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void SyncHealth(float health)
		{
			currentHealth = health;
			OnHealthChangedInternal(currentHealth, maxHealth);
			if (WorldHealthBar != null)
				WorldHealthBar.ShowFor(currentHealth, maxHealth);
		}

		public override void OnDeath(Character killer)
		{
			base.OnDeath(killer);

			if (Multiplayer.IsServer())
			{
				if (killer != null)
					Economy.CurrencyEconomy.ApplyCurrencyGain(killer, Economy.CurrencyEconomy.ElimType.Body);
				// Tell all clients to hide and disable
				Rpc(MethodName.SyncDeath);
				GetTree().CreateTimer(respawnTime).Timeout += () => {
					if (IsInstanceValid(this)) Rpc(MethodName.SyncRespawn);
				};
			}
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void SyncDeath()
		{
			isDead = true;
			Visible = false;
			SetPhysicsProcess(false);
			SetProcess(false);
			if (WorldHealthBar != null)
				WorldHealthBar.Visible = false;
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void SyncRespawn()
		{
			isDead = false;
			currentHealth = maxHealth;
			verticalVelocity = 0f;
			fireTimer = 0f;
			GlobalPosition = spawnPosition;
			Visible = true;
			SetPhysicsProcess(true);
			SetProcess(true);
			if (WorldHealthBar != null)
			{
				WorldHealthBar.Visible = true;
				WorldHealthBar.Reset();
			}
		}

		private Character FindNearestPlayer()
		{
			float closest = detectionRadius * detectionRadius;
			Character nearest = null;

			foreach (Node node in GetTree().GetNodesInGroup("Player"))
			{
				if (node is Character c && !c.IsDead)
				{
					float distSq = GlobalPosition.DistanceSquaredTo(c.GlobalPosition);
					if (distSq < closest)
					{
						closest = distSq;
						nearest = c;
					}
				}
			}

			return nearest;
		}

		private void FireAtTarget()
		{
			if (target == null) return;

			Vector3 origin = GlobalPosition + Vector3.Up * 1.4f;
			Vector3 aimPoint = target.GlobalPosition + Vector3.Up * 0.9f;
			Vector3 direction = (aimPoint - origin).Normalized();
			Vector3 targetPos = origin + direction * 100f;

			var spaceState = GetWorld3D().DirectSpaceState;
			var query = PhysicsRayQueryParameters3D.Create(origin, targetPos);
			query.CollisionMask = 0xFFFFFFFF;
			query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

			var result = spaceState.IntersectRay(query);
			Vector3 hitPoint = result.Count > 0 ? (Vector3)result["position"] : targetPos;

			if (result.Count > 0)
			{
				Node node = result["collider"].As<Node>();
				while (node != null)
				{
					if (node is Character hit && hit != this)
					{
						// Server calls TakeDamage directly (already server-side)
						hit.TakeDamage(damagePerShot, this);
						break;
					}
					node = node.GetParent();
				}
			}

			SpawnTrail(origin, hitPoint);
		}

		private void SpawnTrail(Vector3 from, Vector3 to)
		{
			if (from.DistanceTo(to) < 0.5f) return;
			// Broadcast trail to all clients so everyone sees the bullet.
			Rpc(MethodName.SpawnTrailOnAllPeers, from, to);
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
			TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
		private void SpawnTrailOnAllPeers(Vector3 from, Vector3 to)
		{
			var trail = new Weapon.BulletTrail();
			trail.TrailColor = new Color(1f, 0.3f, 0.0f, 1f);
			trail.Init(from, to);
			(GetParent() ?? GetTree().CurrentScene).AddChild(trail);
		}

		public override void RequestAIDecision() { }
		public override void OnInputAction(string action) { }
	}
}
