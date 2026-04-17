/// <summary>
/// Static carrier for winner data — survives scene changes.
/// Populated by GameMaster before transitioning to the Podium scene.
/// </summary>
public static class WinnerData
{
	public static string Name   = "";
	public static Godot.Color Color = new Godot.Color(1, 1, 1, 1);
	public static int Kills  = 0;
	public static int Deaths = 0;
	public static int Score  = 0;

	public static float KDRatio => Deaths > 0 ? (float)Kills / Deaths : Kills;
}
