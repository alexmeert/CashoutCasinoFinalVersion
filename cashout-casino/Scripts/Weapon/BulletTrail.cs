using Godot;

namespace CashoutCasino.Weapon
{
	public partial class BulletTrail : Node3D
	{
		public Color TrailColor = new Color(1f, 0.95f, 0.6f, 1f);

		// Visible segment length in metres — shorter = more "bullet-like"
		private const float SegmentLen = 2.5f;
		// Time for the head to travel the full distance
		private const float TravelTime = 0.055f;
		// Seconds to fade out after the head reaches the target
		private const float FadeTime = 0.04f;

		private MeshInstance3D _meshInstance;
		private StandardMaterial3D _material;
		private CylinderMesh _cylinder;

		private Vector3 _from;
		private Vector3 _dir;
		private float _totalDist;
		private float _speed;
		private float _elapsed;

		public void Init(Vector3 startWorld, Vector3 endWorld)
		{
			_from      = startWorld;
			_totalDist = startWorld.DistanceTo(endWorld);
			_dir       = _totalDist > 0.001f ? (endWorld - startWorld).Normalized() : Vector3.Forward;
			_speed     = _totalDist / Mathf.Max(TravelTime, 0.001f);
		}

		public override void _Ready()
		{
			_material = new StandardMaterial3D
			{
				ShadingMode              = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Transparency             = BaseMaterial3D.TransparencyEnum.Alpha,
				AlbedoColor              = TrailColor,
				EmissionEnabled          = true,
				Emission                 = TrailColor,
				EmissionEnergyMultiplier = 2f,
				CullMode                 = BaseMaterial3D.CullModeEnum.Disabled,
			};

			_cylinder = new CylinderMesh
			{
				TopRadius      = 0.012f,
				BottomRadius   = 0.012f,
				Height         = 0.05f,
				RadialSegments = 5,
				Material       = _material,
			};

			_meshInstance = new MeshInstance3D { Mesh = _cylinder };
			AddChild(_meshInstance);

			ApplySegmentTransform(0f);
		}

		public override void _Process(double delta)
		{
			_elapsed += (float)delta;

			float headDist = Mathf.Min(_elapsed * _speed, _totalDist);
			float arrivedAt = _totalDist / _speed;

			float alpha = _elapsed > arrivedAt
				? Mathf.Clamp(1f - (_elapsed - arrivedAt) / FadeTime, 0f, 1f)
				: 1f;

			_material.AlbedoColor = new Color(TrailColor.R, TrailColor.G, TrailColor.B, alpha);
			_material.Emission    = new Color(TrailColor.R, TrailColor.G, TrailColor.B, alpha);

			if (alpha <= 0f)
			{
				QueueFree();
				return;
			}

			ApplySegmentTransform(_elapsed);
		}

		private void ApplySegmentTransform(float t)
		{
			float headDist = Mathf.Min(t * _speed, _totalDist);
			float tailDist = Mathf.Max(0f, headDist - SegmentLen);
			float segLen   = Mathf.Max(headDist - tailDist, 0.05f);

			Vector3 mid = _from + _dir * ((headDist + tailDist) * 0.5f);

			// Build a basis with Y-axis along the travel direction
			Vector3 arbitrary = Mathf.Abs(_dir.Dot(Vector3.Forward)) < 0.99f ? Vector3.Forward : Vector3.Right;
			Vector3 axisX = _dir.Cross(arbitrary).Normalized();
			Vector3 axisZ = axisX.Cross(_dir).Normalized();

			GlobalTransform = new Transform3D(new Basis(axisX, _dir, axisZ), mid);
			_cylinder.Height = segLen;
		}
	}
}
