using Godot;
using System;

public partial class NetworkedCharacter : CharacterBody2D
{
	[Export] public AnimatedSprite2D myAnimation;
	[Export] public NetID myId;
	[Export] public PackedScene bullet;
	[Export] public int speed = 200;
	[Export] public Vector2 SyncedVelocity

	
	{
		get => Velocity;
		set => Velocity = value;
	}
	[Export] public bool canShoot = true;
	[Export] public float timer = 5;
	
	public override void _Ready()
	{
		base._Ready();
		
	}
	
	public override void _Process(double delta)
	{	
		base._Process(delta);
		if(!myId.IsNetworkReady)
		{
			return;
		}
		
		if(!canShoot)
		{
			timer -= (float)delta;
			// update the UI with whatever the cooldown visual is
		}
		else
		{
			timer = 5;
		}
		
		if(GenericCore.Instance.IsServer)
		{
			if(timer<=0)
			{
				canShoot = true; // state variable I synchronize
				// only the  server controls canShoot
			}
			//if making an AI/NPC this is where all the code would go for movement/interactions
			
			//necessary for Godot
			MoveAndSlide();
		}
		if(myId.IsLocal)
		{
			// ONLY if the player owns the character
			Vector2 myInputAxis = new Vector2(
				Input.GetAxis("ui_left", "ui_right"),
				Input.GetAxis("ui_up", "ui_down")
				);
			Rpc("MoveMe", myInputAxis);
			//GD.Print(GetViewport().GetCamera2D());
			GetViewport().GetCamera2D().Position = GetViewport().GetCamera2D().Position;
			//GetViewport().GetCamera2D().
			
			if(Input.IsActionJustPressed("Fire") && canShoot)
			{
				// RPC call shoot
				RpcId(1, "Fire"); // 1 is the Server
			}
		}
		if(!GenericCore.Instance.IsServer)
		{
			// for if you want code to run on all clients
			if(SyncedVelocity.Length()>.15f)
			{
				if(Math.Abs(SyncedVelocity.X) > MathF.Abs(SyncedVelocity.Y))
				{
					if(SyncedVelocity.X>0)
					{
						// Go Right
						myAnimation.Play("RIGHT");
					}
					else
					{
						// Go left
						myAnimation.Play("LEFT");
					}
				}
				else
				{
					if(SyncedVelocity.Y < 0)
					{
						// Go Up
						myAnimation.Play("UP");
					}
					else
					{
						// Go Down
						myAnimation.Play("DOWN");
					}
				}
			}
		}
	}
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void Fire()
	{
		// I should be on the server
		if(GenericCore.Instance.IsServer)
		{
			var t = ((NetworkCore)GenericCore.Instance.MainNetworkCore).NetCreateObject
				(2,
				new Vector3(this.GlobalPosition.X, GlobalPosition.Y, 0),
				Quaternion.Identity,
				myId.OwnerId // OwnerId is only needed for if its like a troop being spawned, otherwise things like bullets are owned by the server
				);
			((RigidBody2D)t).LinearVelocity = new Vector2(0, -100);
		}
	}
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void MoveMe(Vector2 dir)
	{
		if(GenericCore.Instance.IsServer)
		{
			SyncedVelocity = dir.Normalized() * speed;
		}
	}
	
	
}
