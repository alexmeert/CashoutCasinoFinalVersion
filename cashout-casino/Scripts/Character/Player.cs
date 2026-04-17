using Godot;
using CashoutCasino.UI;

namespace CashoutCasino.Character
{
	public partial class Player : Character
	{
		[Export] public float mouseSensitivity = 0.15f;
		[Export] public NodePath cameraPath = "CameraHolder/Camera3D";
		[Export] public NodePath cameraHolderPath = "CameraHolder";
		[Export] public NodePath collisionShapePath = "CollisionShape3D";
		[Export] public NodePath weaponManagerPath = "CameraHolder/Camera3D/WeaponManager";
		[Export] public NodePath hudPath = "PlayerHud";
		[Export] public NodePath respawnScreenPath = "RespawnScreen";
		[Export] public float respawnTime = 5f;

		[Export] public float jumpForce = 5f;
		[Export] public float gravity = 20f;
		[Export] public float standHeight = 1.8f;
		[Export] public float crouchHeight = 1.0f;

		// Regen settings
		[Export] public float regenDelay = 15f;    // seconds of no damage before regen starts
		[Export] public float regenRate  = 10f;    // HP per second once regen kicks in

		private Camera3D camera;
		private Node3D cameraHolder;
		private CollisionShape3D collisionShape;
		private Weapon.WeaponManager wm;
		private UI.PlayerHud hud;
		private UI.RespawnScreen respawnScreen;
		// PlayerCharacter handles the animated model (replaces capsule MeshInstance3D)
		private PlayerCharacter playerCharacter;
		[Export] public AnimationPlayer FpAnimPlayer;
		[Export] public Weapon.WeaponManager FpWeaponManager;
		[Export] public AudioStreamPlayer3D ShootAudioPlayer;

		[Export] public AudioStream RifleShootSound;
		[Export] public AudioStream RifleReloadSound;
		[Export] public AudioStream ShotgunShootSound;
		[Export] public AudioStream ShotgunReloadSound;
		[Export] public AudioStream PistolShootSound;
		[Export] public AudioStream PistolReloadSound;

		private Node3D _armature;
		private Node3D _fpPlayer;
		private bool _wasFpFiring = false;
		private bool _wasArFiring = false;
		private bool _fpAnimLocked = false;
		private bool _pistolsUseRight = false;
		[Export] public AnimationPlayer TpAnimPlayer;
		private AnimationPlayer _tpAnimPlayer;
		private string _lastTpAnim = "";
		private bool _isLocalReloading = false;

		[Export] public int Kills    { get; set; }
		[Export] public int Deaths   { get; set; }
		// Exposed for MultiplayerSynchronizer replication so leaderboard works on all clients.
		[Export] public int SyncedCurrency { get => currentCurrency; set => currentCurrency = value; }
		[Export] public int SyncedAtmDebt  { get => atmDebt;         set => atmDebt = value; }

		private float verticalVelocity = 0f;
		private int _lockedWeaponIndex = -1; // -1 = no lock
		private float cameraPitch = 0f;
		private const float MAX_PITCH = 89f;

		private Vector3 spawnPosition;
		private int atmDebt = 0;

		// Regen state
		private float timeSinceLastDamage = 0f;
		private bool regenActive = false;

		// Spectate / kill-cam state
		private string _lastKillerName = "";
		private Leaderboard _leaderboard;
		private Player _spectateTarget;

		public override void _Ready()
		{
			base._Ready();

			spawnPosition  = GlobalPosition;
			camera         = GetNode<Camera3D>(cameraPath);
			cameraHolder   = GetNode<Node3D>(cameraHolderPath);
			collisionShape = GetNode<CollisionShape3D>(collisionShapePath);
			// Grab the animated PlayerCharacter node instead of a raw MeshInstance3D
			playerCharacter = GetNodeOrNull<PlayerCharacter>("PlayerCharacter");
			_armature     = GetNodeOrNull<Node3D>("Armature");
			_fpPlayer = GetNodeOrNull<Node3D>("FirstPersonPlayer");
			if (_fpPlayer != null && FpAnimPlayer == null)
				FpAnimPlayer = _fpPlayer.GetNodeOrNull<AnimationPlayer>("FirstPersonAnimationPlayer")
					?? _fpPlayer.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
			_tpAnimPlayer = TpAnimPlayer
				?? GetNodeOrNull<AnimationPlayer>("Armature/ThirdPersonAnimationPlayer")
				?? GetNodeOrNull<AnimationPlayer>("ThirdPersonAnimationPlayer")
				?? GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

			// FirstPersonPlayer is a CharacterBody3D; its inner CollisionShape3D must not
			// participate in physics or it will interfere with the outer Player body.
			var fpCollision = _fpPlayer?.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
			if (fpCollision != null) fpCollision.Disabled = true;


			if (HasNode(weaponManagerPath))
			{
				wm = GetNode<Weapon.WeaponManager>(weaponManagerPath);
				wm.PlayerCamera = camera;
				wm.Setup();
				weaponManager = wm;
			}

			if (HasNode(hudPath))
			{
				hud = GetNode(hudPath) as UI.PlayerHud;
				if (hud != null)
				{
					hud.WeaponManager = wm;
					CurrencyChanged += hud.OnCurrencyChanged;
				}
			}

			if (HasNode(respawnScreenPath))
				respawnScreen = GetNode(respawnScreenPath) as UI.RespawnScreen;

			var whb = GetNodeOrNull<UI.WorldHealthBar>("WorldHealthBar");
			if (whb != null)
			{
				WorldHealthBar = whb;
				whb.Visible = false;
			}

			currentCurrency = Economy.CurrencyEconomy.INITIAL_SPAWN;
			wm?.SyncAmmoToAllWeapons(currentCurrency);
			CurrencyChanged += OnCurrencyChangedSync;

			hud?.OnCurrencyChanged(currentCurrency);
			hud?.OnHealthChanged(currentHealth, maxHealth);
			hud?.OnAtmDebtChanged(atmDebt);

			AddToGroup("Players");

			// Authority setup happens via ClaimAuthority RPC sent by the server.
			// Cache leaderboard reference now so Tab works even before ClaimAuthority fires.
			_leaderboard = GetNodeOrNull("Leaderboard") as Leaderboard;
			if (_leaderboard == null)
			{
				var lbScene = GD.Load<PackedScene>("res://Packed Scenes/Menu/Leaderboard.tscn");
				if (lbScene != null)
				{
					_leaderboard = lbScene.Instantiate<Leaderboard>();
					_leaderboard.Name = "Leaderboard";
					_leaderboard.Visible = false;
					AddChild(_leaderboard);
					GD.Print("[Leaderboard] Created dynamically.");
				}
			}
		}

		[Rpc(MultiplayerApi.RpcMode.Authority,
			CallLocal = true,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void ClaimAuthority(long ownerId)
		{
			// All peers run this so every copy of this player has the correct authority set.
			// This is required for MultiplayerSynchronizer to know who sends deltas.
			SetMultiplayerAuthority((int)ownerId);
			GD.Print($"[Player] ClaimAuthority: peer {ownerId} owns {Name} (I am {Multiplayer.GetUniqueId()})");
			if (IsMultiplayerAuthority())
				SetupLocalAuthority();
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void LockWeapon(int weaponIndex)
		{
			_lockedWeaponIndex = weaponIndex;
			// Switch weapon for everyone so correct weapon shows on TP body.
			wm?.SwitchWeapon(weaponIndex);
			// FP weapon only for local player.
			if (IsMultiplayerAuthority())
				FpWeaponManager?.SwitchWeapon(weaponIndex);
		}

		private void SetupLocalAuthority()
		{
			if (IsMultiplayerAuthority())
			{
				Input.MouseMode = Input.MouseModeEnum.Captured;
				camera.Current = true;
				if (_leaderboard == null) _leaderboard = GetNodeOrNull("Leaderboard") as Leaderboard;
				if (hud != null)
				{
					hud.Visible = true;
					hud.SetAsLocalInstance();
					hud.OwnerPlayer = this;
				}
				// If FP arms exist use those; otherwise show the 3rd-person weapon on the camera.
				if (_fpPlayer != null)
				{
					if (wm != null) wm.Visible = false;
				}
				else
				{
					if (wm != null) wm.Visible = true; // No FP arms — use 3P weapon on camera
				}
				if (_armature != null) _armature.Visible = false;
				if (_fpPlayer != null)
				{
					// Reparent into camera-local space so weapons follow the view without
					// rotating around the wrong pivot when pitching up/down.
					_fpPlayer.Reparent(camera, false);
					_fpPlayer.Position = new Vector3(0f, -1.5f, 0f);
					_fpPlayer.Rotation = Vector3.Zero;
					_fpPlayer.Visible  = true;
				}
				FpWeaponManager?.Setup();
				if (FpAnimPlayer != null)
					FpAnimPlayer.AnimationFinished += OnFpAnimFinished;
			}
			else
			{
				camera.Current = false;
				// Hide HUD on remote players and stop it processing.
				if (hud != null)
				{
					hud.Visible = false;
					hud.ProcessMode = ProcessModeEnum.Disabled;
				}
				// Hide respawn screen on remote players.
				if (respawnScreen != null)
				{
					respawnScreen.Visible = false;
					respawnScreen.ProcessMode = ProcessModeEnum.Disabled;
				}
				// Remote players: show third-person body and their TP weapon.
				// Hide camera but keep weapon manager visible so the weapon shows.
				camera.Current = false;
				if (_armature != null) _armature.Visible = true;
				if (_fpPlayer != null) _fpPlayer.Visible = false;
				// Always show this player's health bar above their head for other clients.
				if (WorldHealthBar != null)
				{
					WorldHealthBar.Visible = true;
					WorldHealthBar.SetPersistent(true);
					WorldHealthBar.ShowFor(currentHealth, maxHealth);
				}
				// Show wm so the correct weapon is visible on the remote player.
				// LockWeapon will have set the right weapon index via RPC.
				if (wm != null) wm.Visible = true;
			}
		}

		public void AddAtmDebt(int amount)
		{
			atmDebt += amount;
			ModifyCurrency(amount);
			hud?.OnAtmDebtChanged(atmDebt);
		}

		public int GetAtmDebt() => atmDebt;
		public int GetFinalScore() => currentCurrency - atmDebt;

		public void ApplyKillReward(int amount)
		{
			if (!Multiplayer.IsServer()) return;
			Kills++;
			Rpc(MethodName.SyncStats, Kills, Deaths);
			ModifyCurrency(amount);
			Rpc(nameof(SyncCurrencyToClient), currentCurrency);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void SyncCurrencyToClient(int newAmount)
		{
			currentCurrency = newAmount;
			if (IsMultiplayerAuthority())
			{
				EmitSignal(SignalName.CurrencyChanged, currentCurrency);
				wm?.SyncAmmoToAllWeapons(currentCurrency);
			}
		}

		// Shooter's client calls this — routes to server for authoritative damage.
		public override void TakeDamage(float damage, Character attacker = null, bool isHeadshot = false)
		{
			string killerName = attacker?.GetDisplayName() ?? "";
			string weaponKind = attacker?.GetCurrentWeaponKind().ToString() ?? "Other";
			if (Multiplayer.IsServer())
				ServerApplyDamage(damage, killerName, weaponKind, isHeadshot);
			else
				RpcId(1, MethodName.ServerApplyDamage, damage, killerName, weaponKind, isHeadshot);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void SyncStats(int kills, int deaths)
		{
			Kills  = kills;
			Deaths = deaths;
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void ServerApplyDamage(float damage, string killerName, string weaponKind, bool isHeadshot)
		{
			if (!Multiplayer.IsServer() || isDead) return;
			timeSinceLastDamage = 0f;
			regenActive = false;
			currentHealth = Mathf.Max(currentHealth - damage, 0f);
			Rpc(MethodName.SyncHealth, currentHealth);
			if (currentHealth <= 0f)
			{
				var elimType = isHeadshot
					? Economy.CurrencyEconomy.ElimType.Head
					: Economy.CurrencyEconomy.ElimType.Body;
				int reward = isHeadshot
					? Economy.CurrencyEconomy.HEAD_ELIM
					: Economy.CurrencyEconomy.BODY_ELIM;
				foreach (var node in GetTree().GetNodesInGroup("Players"))
					if (node is Player killer && killer != this && killer.GetDisplayName() == killerName)
					{
						killer.ApplyKillReward(reward);
						break;
					}
				Rpc(nameof(SyncKillFeed), killerName, weaponKind, GetDisplayName(), reward, isHeadshot);
				Rpc(MethodName.SyncDeath, spawnPosition);
			}
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void SyncKillFeed(string killerName, string weaponKind, string victimName, int rewardAmount, bool isHeadshot)
		{
			UI.PlayerHud.LocalInstance?.AddKillEntry(killerName, weaponKind, victimName, rewardAmount, isHeadshot);
			if (IsMultiplayerAuthority())
				_lastKillerName = killerName;
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void SyncHealth(float health)
		{
			currentHealth = health;
			timeSinceLastDamage = 0f;
			regenActive = false;
			if (IsMultiplayerAuthority())
			{
				hud?.OnHealthChanged(currentHealth, maxHealth);
				hud?.OnDamageTaken();
				TriggerCameraShake();
			}
			// Update the persistent world health bar on remote players.
			if (!IsMultiplayerAuthority())
				WorldHealthBar?.ShowFor(currentHealth, maxHealth);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void SyncDeath(Vector3 respawnPos)
		{
			if (Multiplayer.IsServer()) { Deaths++; Rpc(MethodName.SyncStats, Kills, Deaths); }
			isDead = true;
			SetPhysicsProcess(false);
			spawnPosition = respawnPos;
			_lastTpAnim = "Death";
			if (_tpAnimPlayer != null && _tpAnimPlayer.HasAnimation("Death"))
			{
				_tpAnimPlayer.GetAnimation("Death").LoopMode = Animation.LoopModeEnum.None;
				_tpAnimPlayer.Play("Death");
				_tpAnimPlayer.AnimationFinished += FreezeOnDeathFrame;
			}
			collisionShape.Disabled = true;
			if (playerCharacter?.MyMesh != null)
			{
				var mat = new StandardMaterial3D();
				mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
				mat.AlbedoColor = new Color(0.6f, 0.6f, 0.8f, 0.35f);
				playerCharacter.MyMesh.MaterialOverride = mat;
			}
			if (IsMultiplayerAuthority())
			{
				_spectateTarget = FindPlayerByDisplayName(_lastKillerName);
				hud?.ShowDeathUI();
				if (_fpPlayer != null)
					_fpPlayer.Visible = false;
				if (wm != null) wm.Visible = false;
				respawnScreen?.StartCountdown(respawnTime, DoRespawn, _lastKillerName);
			}
			if (Multiplayer.IsServer())
				GetTree().CreateTimer(respawnTime).Timeout += () => { if (IsInstanceValid(this)) Rpc(MethodName.SyncRespawnAll); };
		}

		private void FreezeOnDeathFrame(StringName animName)
		{
			if (animName != "Death") return;
			_tpAnimPlayer.AnimationFinished -= FreezeOnDeathFrame;
			// Seek to the very last frame then pause so the skeleton holds the death pose.
			if (_tpAnimPlayer.HasAnimation("Death"))
				_tpAnimPlayer.Seek((double)_tpAnimPlayer.GetAnimation("Death").Length, true);
			_tpAnimPlayer.Pause();
		}

		private void DoRespawn() => RpcId(1, MethodName.RequestRespawn);

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void RequestRespawn()
		{
			if (Multiplayer.IsServer()) Rpc(MethodName.SyncRespawnAll);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void SyncRespawnAll()
		{
			isDead = false;
			collisionShape.Disabled = false;
			currentHealth = maxHealth;
			verticalVelocity = 0f;
			timeSinceLastDamage = 0f;
			regenActive = false;
			GlobalPosition = spawnPosition;
			SetPhysicsProcess(true);
			// Restore player color — ApplyColor duplicates the surface material so the
			// outline next_pass is preserved on the override.
			var pc = GetNodeOrNull<PlayerCharacter>("PlayerCharacter");
			if (pc != null && pc.MyColor.A > 0)
				pc.ApplyColor(pc.MyColor);
			if (!IsMultiplayerAuthority())
				WorldHealthBar?.ShowFor(currentHealth, maxHealth);
			if (IsMultiplayerAuthority())
			{
				_spectateTarget = null;
				_lastKillerName = "";
				_lastTpAnim = "";
				cameraHolder.Position = new Vector3(0f, standHeight - 0.15f, 0f);
				cameraHolder.Rotation = Vector3.Zero;
				cameraPitch = 0f;
				if (_fpPlayer != null)
				{
					_fpPlayer.Visible = true;
					if (wm != null) wm.Visible = false;
				}
				else
				{
					if (wm != null) wm.Visible = true;
				}
				hud?.ShowAliveUI();
				hud?.OnHealthChanged(currentHealth, maxHealth);
				Input.MouseMode = Input.MouseModeEnum.Captured;
				SetProcessInput(true);
			}
		}

		protected override void OnHealthChangedInternal(float current, float max)
		{
			if (IsMultiplayerAuthority()) hud?.OnHealthChanged(current, max);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void ServerApplyUpgrade(int item, float speedBonus, float damageBonus, float healthBonus)
		{
			if (!Multiplayer.IsServer()) return;
			Rpc(MethodName.SyncUpgrade, item, speedBonus, damageBonus, healthBonus);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void SyncUpgrade(int item, float speedBonus, float damageBonus, float healthBonus)
		{
			switch (item)
			{
				case 0:
					moveSpeed += speedBonus;
					break;
				case 1:
					if (wm != null)
						foreach (var w in wm.GetWeapons())
							w.damagePerHit += damageBonus;
					break;
				case 2:
					maxHealth += healthBonus;
					currentHealth = Mathf.Min(currentHealth + healthBonus, maxHealth);
					OnHealthChangedInternal(currentHealth, maxHealth);
					break;
			}
		}

		private void OnCurrencyChangedSync(int newAmount)
		{
			wm?.SyncAmmoToAllWeapons(newAmount);
			// If running on server, push the new value to the owning client.
			if (Multiplayer.IsServer() && !IsMultiplayerAuthority())
				RpcId(GetMultiplayerAuthority(), MethodName.SyncCurrency, newAmount);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
			TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void SyncCurrency(int amount)
		{
			currentCurrency = amount;
			hud?.OnCurrencyChanged(currentCurrency);
			wm?.SyncAmmoToAllWeapons(currentCurrency);
		}

		public override void _Input(InputEvent @event)
		{
			if (!IsMultiplayerAuthority()) return;

			// Tab works even when dead
			if (@event is InputEventKey tab && tab.Keycode == Key.Tab)
			{
				GD.Print($"[Leaderboard] Tab {(tab.Pressed ? "pressed" : "released")}, leaderboard={((_leaderboard != null) ? "found" : "NULL")}, authority={IsMultiplayerAuthority()}");
				if (_leaderboard != null)
				{
					_leaderboard.Visible = tab.Pressed;
					if (tab.Pressed) _leaderboard.Refresh();
				}
				GetViewport().SetInputAsHandled();
				return;
			}

			if (isDead) return;

			if (@event is InputEventMouseMotion mouseMotion)
			{
				RotateY(Mathf.DegToRad(-mouseMotion.Relative.X * mouseSensitivity));
				cameraPitch -= mouseMotion.Relative.Y * mouseSensitivity;
				cameraPitch = Mathf.Clamp(cameraPitch, -MAX_PITCH, MAX_PITCH);
				cameraHolder.Rotation = new Vector3(Mathf.DegToRad(cameraPitch), 0f, 0f);
			}

			if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.IsEcho())
			{
				if (keyEvent.Keycode == Key.Escape)
					Input.MouseMode = Input.MouseModeEnum.Visible;

				if (keyEvent.Keycode == Key.R && wm != null && !_isLocalReloading)
					TryStartLocalReload();
			}
		}

		public override void _Process(double delta)
		{
			if (isDead)
			{
				if (IsMultiplayerAuthority()) UpdateSpectateCam(delta);
				return;
			}

			if (IsMultiplayerAuthority() && _isLocalReloading && FpAnimPlayer != null)
			{
				float len = (float)FpAnimPlayer.CurrentAnimationLength;
				float progress = len > 0f ? (float)(FpAnimPlayer.CurrentAnimationPosition / len) : 0f;
				hud?.OnReloadProgress(progress);
			}

			if (currentHealth >= maxHealth) return;

			float dt = (float)delta;
			timeSinceLastDamage += dt;

			if (timeSinceLastDamage >= regenDelay)
			{
				regenActive = true;
				currentHealth = Mathf.Min(currentHealth + regenRate * dt, maxHealth);
				OnHealthChangedInternal(currentHealth, maxHealth);
			}
		}

		public override void _PhysicsProcess(double delta)
		{
			if (!IsMultiplayerAuthority()) return;
			if (isDead) return;

			float dt = (float)delta;

			SetCrouch(Input.IsActionPressed("crouch"));

			Vector3 inputDir = Vector3.Zero;
			if (Input.IsActionPressed("move_forward"))  inputDir -= Transform.Basis.Z;
			if (Input.IsActionPressed("move_backward")) inputDir += Transform.Basis.Z;
			if (Input.IsActionPressed("move_left"))     inputDir -= Transform.Basis.X;
			if (Input.IsActionPressed("move_right"))    inputDir += Transform.Basis.X;

			inputDir = inputDir.Normalized();
			bool sprinting = Input.IsActionPressed("sprint") && !isCrouching;

			if (IsOnFloor())
			{
				verticalVelocity = -0.5f;
				if (Input.IsActionJustPressed("jump") && !isCrouching)
					verticalVelocity = jumpForce;
			}
			else
			{
				verticalVelocity -= gravity * dt;
			}

			float speed = moveSpeed
				* (sprinting ? sprintMultiplier : 1f)
				* (isCrouching ? crouchMultiplier : 1f);

			Velocity = new Vector3(inputDir.X * speed, verticalVelocity, inputDir.Z * speed);
			MoveAndSlide();

			if (wm == null) return;

			bool fireHeld    = Input.IsActionPressed("fire");
			bool firePressed = Input.IsActionJustPressed("fire");
			bool wantFire    = wm.CurrentWeaponHoldToFire() ? fireHeld : firePressed;

			// Shotgun reload can be cancelled by firing when there's ammo in the mag.
			if (wantFire && _isLocalReloading && wm.CanInterruptCurrentReload && wm.GetCurrentMag() > 0)
			{
				_isLocalReloading = false;
				_fpAnimLocked = false;
				wm.CancelReload();
				hud?.OnReloadComplete();
			}

			// Auto-reload when the mag hits zero.
			if (wm.GetCurrentMag() <= 0 && !_isLocalReloading)
			{
				TryStartLocalReload();
			}
			else if (wantFire)
			{
				bool isRifle = wm.GetCurrentWeaponKind() == Weapon.WeaponKind.Rifle;
				if (isRifle)
				{
					// AR: start looping sound on first frame of fire, stop when released.
					wm.FireCurrentWeapon(-camera.GlobalTransform.Basis.Z, this);
					if (!_wasArFiring)
						Rpc(MethodName.SyncShootSound, (int)Weapon.WeaponKind.Rifle);
					_wasArFiring = true;
				}
				else
				{
					// Shotgun/Pistol: play sound only on frames the weapon actually fires.
					if (wm.FireCurrentWeapon(-camera.GlobalTransform.Basis.Z, this))
						Rpc(MethodName.SyncShootSound, (int)wm.GetCurrentWeaponKind());
					_wasArFiring = false;
				}
			}
			else
			{
				if (_wasArFiring)
					Rpc(MethodName.SyncStopShootSound);
				_wasArFiring = false;
			}

			if (_lockedWeaponIndex < 0)
			{
				if (Input.IsActionJustPressed("weapon_1")) { CancelLocalReload(); wm.SwitchWeapon(0); FpWeaponManager?.SwitchWeapon(0); }
				if (Input.IsActionJustPressed("weapon_2")) { CancelLocalReload(); wm.SwitchWeapon(1); FpWeaponManager?.SwitchWeapon(1); }
				if (Input.IsActionJustPressed("weapon_3")) { CancelLocalReload(); wm.SwitchWeapon(2); FpWeaponManager?.SwitchWeapon(2); }
			}

			// Don't play the shoot animation when the mag is empty or reloading.
			UpdateFpAnim(wantFire && !_isLocalReloading && wm.GetCurrentMag() > 0);

			// Play TP anim locally; MultiplayerSynchronizer replicates current_animation to peers.
			bool tpMoving = new Vector2(Velocity.X, Velocity.Z).LengthSquared() > 0.1f;
			string tpAnim = GetTpAnimName(tpMoving);
			if (tpAnim != _lastTpAnim)
			{
				_lastTpAnim = tpAnim;
				Rpc(MethodName.SyncTpAnim, tpAnim);
			}
		}

		private void SetCrouch(bool crouch)
		{
			isCrouching = crouch;
			if (collisionShape.Shape is CapsuleShape3D capsule)
			{
				float targetHeight = crouch ? crouchHeight : standHeight;
				capsule.Height = targetHeight;
				collisionShape.Position = new Vector3(0f, targetHeight / 2f, 0f);
				cameraHolder.Position   = new Vector3(0f, targetHeight - 0.15f, 0f);
			}
		}

		public override string GetDisplayName() => playerCharacter?.PlayerName is { Length: > 0 } n ? n : Name;

		private Player FindPlayerByDisplayName(string displayName)
		{
			if (string.IsNullOrEmpty(displayName)) return null;
			foreach (var node in GetTree().GetNodesInGroup("Players"))
				if (node is Player p && p != this && p.GetDisplayName() == displayName)
					return p;
			return null;
		}

		private void TriggerCameraShake()
		{
			if (cameraHolder == null) return;
			var origin = cameraHolder.Position;
			var rng = new RandomNumberGenerator();
			rng.Randomize();
			var tween = cameraHolder.CreateTween();
			for (int i = 0; i < 4; i++)
			{
				var offset = new Vector3(rng.RandfRange(-0.06f, 0.06f), rng.RandfRange(-0.06f, 0.06f), 0f);
				tween.TweenProperty(cameraHolder, "position", origin + offset, 0.04);
			}
			tween.TweenProperty(cameraHolder, "position", origin, 0.04);
		}

		private void UpdateSpectateCam(double delta)
		{
			if (_spectateTarget == null || !IsInstanceValid(_spectateTarget)) return;
			var targetCenter = _spectateTarget.GlobalPosition + Vector3.Up * 1.4f;
			var behind = _spectateTarget.GlobalTransform.Basis.Z.Normalized() * 4f + Vector3.Up * 1.2f;
			cameraHolder.GlobalPosition = cameraHolder.GlobalPosition.Lerp(targetCenter + behind, (float)delta * 6f);
			cameraHolder.LookAt(targetCenter);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
			TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
		private void SyncShootSound(int weaponKind)
		{
			if (ShootAudioPlayer == null) return;
			ShootAudioPlayer.Stream = (Weapon.WeaponKind)weaponKind switch
			{
				Weapon.WeaponKind.Shotgun => ShotgunShootSound,
				Weapon.WeaponKind.Pistol  => PistolShootSound,
				_                         => RifleShootSound,
			};
			ShootAudioPlayer.Play();
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
			TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
		private void SyncStopShootSound()
		{
			ShootAudioPlayer?.Stop();
		}

		private void PlayReloadSound()
		{
			if (ShootAudioPlayer == null) return;
			ShootAudioPlayer.Stream = wm?.GetCurrentWeaponKind() switch
			{
				Weapon.WeaponKind.Shotgun => ShotgunReloadSound,
				Weapon.WeaponKind.Pistol  => PistolReloadSound,
				_                         => RifleReloadSound,
			};
			ShootAudioPlayer.Play();
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
			TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
		private void SyncTpAnim(string animName)
		{
			_lastTpAnim = animName;
			if (_tpAnimPlayer == null || !_tpAnimPlayer.HasAnimation(animName)) return;
			_tpAnimPlayer.Play(animName);
		}

		private string GetTpAnimName(bool moving) => wm?.GetCurrentWeaponKind() switch
		{
			Weapon.WeaponKind.Pistol  => moving ? "PistolRun"        : "PistolIdle",
			_                         => moving ? "RifleShotgunRun"  : "RifleShotgunIdle",
		};


		private string GetWeaponPrefix() => wm?.GetCurrentWeaponKind() switch
		{
			Weapon.WeaponKind.Shotgun => "Shotgun",
			Weapon.WeaponKind.Pistol  => "Pistols",
			_                         => "Rifle",
		};

		private void UpdateFpAnim(bool firing)
		{
			if (FpAnimPlayer == null) return;

			// Shotgun and pistols lock until their shoot animation completes.
			if (_fpAnimLocked) return;

			string prefix = GetWeaponPrefix();
			string target;

			if (firing)
			{
				if (prefix == "Pistols")
					target = _pistolsUseRight ? "PistolsShootRight" : "PistolsShootLeft";
				else
					target = $"{prefix}Shoot";

				// Lock out input for single-shot weapons so the anim plays fully.
				if (prefix == "Shotgun" || prefix == "Pistols")
					_fpAnimLocked = true;
			}
			else
			{
				bool moving = new Vector2(Velocity.X, Velocity.Z).LengthSquared() > 0.1f;
				target = moving ? $"{prefix}Run" : $"{prefix}Idle";
			}

			_wasFpFiring = firing;

			if (!FpAnimPlayer.HasAnimation(target)) return;
			if (FpAnimPlayer.CurrentAnimation == target && FpAnimPlayer.IsPlaying()) return;
			FpAnimPlayer.Play(target);
		}

		private void OnFpAnimFinished(StringName animName)
		{
			string anim = animName.ToString();

			if (anim.EndsWith("Reload"))
			{
				CompleteLocalReloadStep();
				return;
			}

			if (anim.StartsWith("Pistols"))
				_pistolsUseRight = !_pistolsUseRight;
			_fpAnimLocked = false;

			string prefix = GetWeaponPrefix();
			bool moving = new Vector2(Velocity.X, Velocity.Z).LengthSquared() > 0.1f;
			string next = moving ? $"{prefix}Run" : $"{prefix}Idle";
			if (FpAnimPlayer != null && FpAnimPlayer.HasAnimation(next))
				FpAnimPlayer.Play(next);
		}

		private void TryStartLocalReload()
		{
			if (wm == null || _isLocalReloading) return;
			if (!wm.StartReload()) return;
			_isLocalReloading = true;
			_fpAnimLocked = true;
			PlayReloadAnim();
			PlayReloadSound();
		}

		private void PlayReloadAnim()
		{
			string animName = $"{GetWeaponPrefix()}Reload";
			if (FpAnimPlayer != null && FpAnimPlayer.HasAnimation(animName))
				FpAnimPlayer.Play(animName);
			else
				CompleteLocalReloadStep(); // no animation defined — complete instantly
		}

		private void CompleteLocalReloadStep()
		{
			bool moreSteps = wm.CompleteReloadStep();
			if (moreSteps)
			{
				PlayReloadAnim();
				PlayReloadSound();
				return;
			}
			_isLocalReloading = false;
			_fpAnimLocked = false;
			hud?.OnReloadComplete();
			string prefix = GetWeaponPrefix();
			bool moving = new Vector2(Velocity.X, Velocity.Z).LengthSquared() > 0.1f;
			string next = moving ? $"{prefix}Run" : $"{prefix}Idle";
			if (FpAnimPlayer != null && FpAnimPlayer.HasAnimation(next))
				FpAnimPlayer.Play(next);
		}

		private void CancelLocalReload()
		{
			if (!_isLocalReloading) return;
			_isLocalReloading = false;
			_fpAnimLocked = false;
			wm?.CancelReload();
			hud?.OnReloadComplete();
		}

		public override void RequestAIDecision() { }
		public override void OnInputAction(string action) { }
	}
}
