using Godot;
using System;

public partial class UserNpm : Control
{
	[Export] public string PlayerName;
	[Export] public int ColorSelection;
	[Export] public int WeaponClassSelection;
	
	[Export] public TextEdit MyName;
	[Export] public ColorRect MyNPMBackground;
	[Export] OptionButton ColorOptionButton;
	[Export] OptionButton WeaponClassOptionButton;
	[Export] Label ReadyLabel;
	[Export] CheckBox ReadyCheckBox;
	
	[Export] public NetID MyNetID;
	
	[Export] public bool IsReady;
	[Export] public Color MyColor;
	
	public override void _Ready()
	{
		AddToGroup("NPM");
		base._Ready();
		SlowStart();
	}
	
	public async void SlowStart()
	{
		//Wait in case the NetworkID didn't get set yet
		await ToSignal(GetTree().CreateTimer(0.2f), SceneTreeTimer.SignalName.Timeout);
		IsReady = false;
		
		// Set default color to white
		MyColor = new Color(1, 1, 1, 1); // White
		ColorOptionButton.Selected = -1;
		WeaponClassOptionButton.Selected = -1;
		
		if(!MyNetID.IsLocal){
			//A player has just hidden their ItemsList and made they're name uneditable
			MyName.Editable = false;
			ColorOptionButton.Disabled = true;
			WeaponClassOptionButton.Disabled = true;
		}
	}
	
	public override void _Process(double delta)
	{
		base._Process(delta);
		
		if(!MyNetID.IsLocal)
		{
			
			// Only update remote player's UI from synced data
			MyName.Text = PlayerName;
			MyNPMBackground.Color = MyColor;
			ColorOptionButton.Selected = ColorSelection;
			WeaponClassOptionButton.Selected = WeaponClassSelection;
			ReadyCheckBox.ButtonPressed = IsReady;
		}
		else
		{
			ReadyCheckBox.Disabled = !CanBeReady();
		}
	}

	//Ask the server to change the color
	public void OnColorChange(int n)
	{
		GD.Print($"OnColorChange called! n={n}");
		//Only the local player should be asking the player to change their color
		//Prevents the local player from controlling other players' colors
		//Solves the issue of why player can control all PCs in a scene
		if(MyNetID.IsLocal){
			Rpc(MethodName.ColorChangeRPC, n);
		}
	}
	
	//Change the color of a player
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal=false, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)]
	public void ColorChangeRPC(int n){
		GD.Print($"ColorChangeRPC called with n={n}");
		GD.Print($"IsServer: {GenericCore.Instance.IsServer}");
		
		if(GenericCore.Instance.IsServer)
		{
			//Why not use Multiplayer.IsServer? Because that'll work if it's not connected to anything
			ColorSelection = n;
			switch(n){
				case 0: //Red
					MyNPMBackground.Color = new Color(1,0,0,1);
					break;
				case 1: //Green
					MyNPMBackground.Color = new Color(0,1,0,1);
					break;
				case 2: //Blue
					MyNPMBackground.Color = new Color(0,0,1,1);
					break;
				case 3: // Yellow
					MyNPMBackground.Color = new Color(1,1,0,1);
					break;
				case 4: // Purple
					MyNPMBackground.Color = new Color(1,0,1,1);
					break;
				case 5: // Orange
					MyNPMBackground.Color = new Color(1,0.5f,0,1);
					break;
				default:
					MyNPMBackground.Color = new Color(1,1,1,1);
					break;
			}
			MyColor = MyNPMBackground.Color;
			GD.Print($"Server set MyColor to: {MyColor}");
			//Should just synchronize across all players
			//Don't forget to synchronize the color rect!
		}
	}
	
	public void OnWeaponClassChange(int n)
	{
		if(MyNetID.IsLocal)
		{
			GD.Print($"Remote player updating UI - Weapon Class value is: {WeaponClassSelection}");
			Rpc(MethodName.WeaponClassChangeRPC, n);
		}
	}
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal=false, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)]
	public void WeaponClassChangeRPC(int n){
		if(GenericCore.Instance.IsServer)
		{
			GD.Print($"Server setting Weapon Class from {WeaponClassSelection} to {n}");
			WeaponClassSelection = n;
			GD.Print($"Weapon Class is now: {WeaponClassSelection}");
		}
	}
	
	//Ask the server to change our name
	public void OnNameChange()
	{
		if(MyNetID.IsLocal){
			Rpc(MethodName.NameChangeRPC, MyName.Text);
		}
	}
	
	//Change the name of a client
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal=false, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)]
	public void NameChangeRPC(string Text){
		if(GenericCore.Instance.IsServer){
			PlayerName = Text;
			MyName.Text = Text;
		}
	}
	
	//Ask the server to change ready
	public void OnIsReady(bool Change)
	{
		if(MyNetID.IsLocal)
		{
			Rpc(MethodName.IsReadyChange, Change);
		}
	}
	
	//Set the client to be ready
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal=false, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)]
	public void IsReadyChange(bool Change)
	{
		if(!GenericCore.Instance.IsServer)
		{
			return;
		}
		IsReady = Change;
	}
	
	// Returns true if the player has selected a valid name, color, and sprite
	private bool CanBeReady()
	{
		bool hasName = !string.IsNullOrWhiteSpace(MyName.Text);
		bool hasColor = ColorOptionButton.Selected != -1; 
		bool hasSprite = WeaponClassOptionButton.Selected != -1;

		return hasName && hasColor && hasSprite;
	}
}
