using Godot;

/// Holds the player's name, color, and position — synced from server to all clients.
public partial class PlayerCharacter : Node3D
{
	[Export] public string PlayerName = "";
	[Export] public MeshInstance3D MyMesh;
	[Export] public NetID MyNetID;
	[Export] public Label3D NameLabel;
	public Color MyColor;

	public override void _Ready()
	{
		base._Ready();
	}

	// Called by GameMaster on the server after spawning.
	// Passes position, name, and color down to all clients.
	public void InitializeCharacter(string playerName, Color color, Vector3 spawnPos)
	{
		GD.Print($"[PlayerCharacter] InitializeCharacter called — IsServer={GenericCore.Instance.IsServer} name='{playerName}' color={color}");
		if (!GenericCore.Instance.IsServer) return;

		Rpc(MethodName.ApplyCharacterData, playerName, color, spawnPos);
	}

	// Runs on ALL (server + clients)
	[Rpc(MultiplayerApi.RpcMode.AnyPeer,
		 CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void ApplyCharacterData(string playerName, Color color, Vector3 spawnPos)
	{
		GD.Print($"[PlayerCharacter] ApplyCharacterData — MyMesh={MyMesh} NameLabel={NameLabel} name='{playerName}' color={color}");

		PlayerName = playerName;

		// Set the position on the root Node3D of the character scene
		var root = GetParent<Node3D>();
		if (root != null)
			root.Position = spawnPos;
		else
			GD.PrintErr("[PlayerCharacter] GetParent<Node3D>() is null — character scene root is not a Node3D!");

		// Apply the color to the existing capsule mesh by overriding its material
		if (MyMesh != null)
			ApplyColor(color);
		else
			GD.PrintErr("[PlayerCharacter] MyMesh is null — wire the MeshInstance3D export in the Godot inspector!");

		if (NameLabel != null)
			NameLabel.Text = playerName;
		else
			GD.PrintErr("[PlayerCharacter] NameLabel is null — wire the Label3D export in the Godot inspector!");
	}

	// Duplicates the mesh's original surface material so next_pass (outline shader) is preserved.
	public void ApplyColor(Color color)
	{
		if (MyMesh == null) return;
		MyColor = color;

		var baseMat = MyMesh.Mesh?.SurfaceGetMaterial(0) as StandardMaterial3D;
		StandardMaterial3D mat = baseMat != null
			? (StandardMaterial3D)baseMat.Duplicate()
			: new StandardMaterial3D();
		mat.AlbedoColor = color;
		MyMesh.MaterialOverride = mat;
	}
}
