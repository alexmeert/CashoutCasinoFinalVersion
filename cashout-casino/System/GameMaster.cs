using Godot;
using CashoutCasino.UI;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class GameMaster : Node
{
	[Export] public bool GameStarted;
	[Export] public bool GameFinished;
	[Export] public MultiplayerSpawner NpmSpawner;
	[Export] public MultiplayerSpawner LevelSpawner;
	[Export] public Label _serverStatus;
	[Export] public Label _playersReady;
	[Export] public Button _hostButton;
	[Export] public Button _clientButton;
	[Export] public CanvasLayer _lobbyCanvasLayer;

	// LevelSpawner spawnable scenes (match the order in the Godot inspector):
	// 0 = Character.tscn
	// 1 = Level.tscn
	private const int LevelSceneIndex     = 1;
	private const int CharacterSceneIndex = 0;

	private readonly Node3D[] _spawnPoints = new Node3D[4];
	private CountdownTimer _roundTimer;

	private bool _gameCycleStarted;
	private ColorRect _lobbyBackground;

	public override void _Ready()
	{
		base._Ready();
		GD.Print("[GameMaster] _Ready called");
		GD.Print($"[GameMaster] GenericCore.Instance is {(GenericCore.Instance != null ? "set" : "NULL")}");
		if (GenericCore.Instance != null)
			GD.Print($"[GameMaster] IsServer: {GenericCore.Instance.IsServer}");
		_lobbyBackground = GetParent().GetNodeOrNull<ColorRect>("ColorRect");
	}

	// LobbyStreamlined._Process() forces all CanvasItem children of GenericCore visible
	// every frame (including the full-screen black ColorRect and the status labels).
	// This counteracts that after the game starts so the 3D level is visible.
	public override void _Process(double delta)
	{
		base._Process(delta);

		// Check if we need to start the game cycle (must happen BEFORE GameStarted check)
		if (GenericCore.Instance != null && GenericCore.Instance.IsServer && !_gameCycleStarted)
		{
			GD.Print("[GameMaster] Starting game cycle from _Process");
			_gameCycleStarted = true;
			GameCycle();
		}

		if (!GameStarted) return;
		if (_lobbyBackground != null) _lobbyBackground.Visible = false;
		if (_serverStatus != null)   _serverStatus.Visible   = false;
		if (_playersReady != null)   _playersReady.Visible   = false;
	}
	//testing commits comment
	public void OnServerStarted()
	{
		// Now handled in _Process
	}

	public void OnClientConnected() { }

	// Game cycle loop (server only)
	public async void GameCycle()
	{
		if (!GenericCore.Instance.IsServer) return;

		GD.Print("[GameMaster] Waiting for players...");
		_serverStatus.Text = "Lobby open";

		while (!GameStarted)
		{
			var npms = GetTree().GetNodesInGroup("NPM");
			int connected = npms.Count;
			int ready = 0;

			foreach (var node in npms)
			{
				if (node is UserNpm npm && npm.IsReady)
					ready++;
			}

			_playersReady.Text = connected < 1
				? "Waiting on player..."
				: $"{ready}/{connected} players ready.";

			if (connected >= 1 && ready == connected)
			{
				GameStarted = true;
				GD.Print("[GameMaster] All players ready, starting game!");
				_playersReady.Text = "Starting!";
				_serverStatus.Text = "Starting game!";
				_playersReady.Visible = false;
			}

			await ToSignal(GetTree().CreateTimer(2.5f), SceneTreeTimer.SignalName.Timeout);
		}

		Rpc(MethodName.ReceiveGameStart);
	}

	// Runs on ALL peers when the game starts
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void ReceiveGameStart()
	{
		GD.Print("[GameMaster] GAMESTART received!");
		GameStarted = true;

		foreach (var node in GetTree().GetNodesInGroup("NPM"))
		{
			if (node is UserNpm npm)
				npm.Visible = false;
		}

		if (_serverStatus != null) _serverStatus.Visible = false;
		if (_playersReady != null) _playersReady.Visible = false;
		if (_lobbyBackground != null) _lobbyBackground.Visible = false;

		if (GenericCore.Instance.IsServer)
			SpawnLevelAndCharacters();

		if (_clientButton != null) _clientButton.Visible = false;
		if (_hostButton != null) _hostButton.Visible = false;
		if (_lobbyCanvasLayer != null) _lobbyCanvasLayer.Visible = false;
	}

	// Spawns the level, finds spawn points, then spawns all player characters
	private async void SpawnLevelAndCharacters()
	{
		GD.Print("[GameMaster] Spawning level...");

		var levelNode = ((NetworkCore)LevelSpawner).NetCreateObject(LevelSceneIndex, Vector3.Zero, Quaternion.Identity, 1L);
		if (levelNode == null)
		{
			GD.PrintErr("[GameMaster] Level spawn failed!");
			return;
		}

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		// Hook up the round timer — only server drives it; clients just display it.
		_roundTimer = levelNode.GetNodeOrNull<CountdownTimer>("CountdownTimer");
		if (_roundTimer != null && GenericCore.Instance.IsServer)
			_roundTimer.TimerCompleted += OnRoundTimerCompleted;

		// Find all spawn points anywhere in the level hierarchy
		_spawnPoints[0] = levelNode.FindChild("P1Start") as Node3D;
		_spawnPoints[1] = levelNode.FindChild("P2Start") as Node3D;
		_spawnPoints[2] = levelNode.FindChild("P3Start") as Node3D;
		_spawnPoints[3] = levelNode.FindChild("P4Start") as Node3D;

		for (int i = 0; i < 4; i++)
		{
			if (_spawnPoints[i] == null)
				GD.PrintErr($"[GameMaster] P{i + 1}Start not found in level scene!");
			else
				GD.Print($"[GameMaster] P{i + 1}Start found at {_spawnPoints[i].Position}");
		}

		await SpawnCharacters();
	}

	// Spawns one character per connected player
	private async Task SpawnCharacters()
	{
		var npms = GetTree().GetNodesInGroup("NPM");

		// Sort by OwnerId so player order matches spawn point order consistently
		var sortedNpms = new List<UserNpm>();
		foreach (var node in npms)
			if (node is UserNpm n) sortedNpms.Add(n);
		sortedNpms.Sort((a, b) => a.MyNetID.OwnerId.CompareTo(b.MyNetID.OwnerId));

		GD.Print($"[GameMaster] Spawning {sortedNpms.Count} character(s)...");

		for (int i = 0; i < sortedNpms.Count; i++)
		{
			var npm = sortedNpms[i];
			long ownerId = npm.MyNetID.OwnerId;
			Vector3 spawnPos = (i < _spawnPoints.Length && _spawnPoints[i] != null)
				? _spawnPoints[i].Position
				: Vector3.Zero;

			GD.Print($"[GameMaster] --- Player {i + 1} Spawn ---");
			GD.Print($"[GameMaster]   owner       = {ownerId}");
			GD.Print($"[GameMaster]   name        = '{npm.PlayerName}'");
			GD.Print($"[GameMaster]   color       = {npm.MyColor}");
			GD.Print($"[GameMaster]   weaponClass = {npm.WeaponClassSelection}");
			GD.Print($"[GameMaster]   spawnPos    = {spawnPos}");

			var characterNode = ((NetworkCore)LevelSpawner).NetCreateObject(CharacterSceneIndex, spawnPos, Quaternion.Identity, ownerId);
			if (characterNode == null)
			{
				GD.PrintErr($"[GameMaster] Character spawn failed for player {i + 1}!");
				continue;
			}

			// Wait a frame so the MultiplayerSpawner flushes this spawn to all peers
			// before RPCs are sent to the now-replicated node.
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

			// Broadcast authority to ALL peers so every copy sets the correct authority.
			// This makes MultiplayerSynchronizer delta sync work on all clients.
			if (characterNode.HasMethod("ClaimAuthority"))
				characterNode.Rpc("ClaimAuthority", ownerId);

			var pc = characterNode.GetNodeOrNull<PlayerCharacter>("PlayerCharacter");
			if (pc != null)
			{
				GD.Print($"[GameMaster] Initializing character for player {i + 1}");
				pc.InitializeCharacter(npm.PlayerName, npm.MyColor, spawnPos);
				// Lock the player to their selected weapon class.
				characterNode.Rpc("LockWeapon", npm.WeaponClassSelection);
			}
			else
			{
				GD.PrintErr($"[GameMaster] PlayerCharacter node not found for player {i + 1}!");
			}
		}
	}

	private void OnRoundTimerCompleted()
	{
		if (!GenericCore.Instance.IsServer) return;
		GD.Print("[GameMaster] Round over — transitioning to podium.");
		GameFinished = true;

		// Find the winner (highest final score) and broadcast their data to all peers.
		CashoutCasino.Character.Player winner = null;
		foreach (var node in GetTree().GetNodesInGroup("Player"))
			if (node is CashoutCasino.Character.Player p)
				if (winner == null || p.GetFinalScore() > winner.GetFinalScore())
					winner = p;

		if (winner != null)
		{
			var pc = winner.GetNodeOrNull<PlayerCharacter>("PlayerCharacter");
			Rpc(MethodName.ChangeToEndScene,
				winner.GetDisplayName(),
				pc?.MyColor ?? new Color(1, 1, 1, 1),
				winner.Kills,
				winner.Deaths,
				winner.GetFinalScore());
		}
		else
			Rpc(MethodName.ChangeToEndScene, "Unknown", new Color(1,1,1,1), 0, 0, 0);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ChangeToEndScene(string winnerName, Color winnerColor, int kills, int deaths, int score)
	{
		WinnerData.Name   = winnerName;
		WinnerData.Color  = winnerColor;
		WinnerData.Kills  = kills;
		WinnerData.Deaths = deaths;
		WinnerData.Score  = score;
		GetTree().ChangeSceneToFile("res://LevelScenes/Podium.tscn");
	}

	private static void ListChildren(Node parent, string indent)
	{
		foreach (var child in parent.GetChildren())
		{
			GD.Print($"{indent}{child.Name} ({child.GetType().Name})");
			ListChildren(child, indent + "  ");
		}
	}
}
