using Godot;
using Richman.Core;

namespace Richman.GodotPresentation
{
    public partial class GodotDiceView : Node3D
    {
        private readonly MeshInstance3D[] _dice = new MeshInstance3D[2];

        public void Build()
        {
            var tray = new MeshInstance3D { Name = "DiceTray", Mesh = new BoxMesh { Size = new Vector3(3.2f, 0.18f, 2.2f) }, Position = new Vector3(0f, 0.45f, 0f), MaterialOverride = Material(new Color("#3d5266")) };
            AddChild(tray);
            for (var i = 0; i < _dice.Length; i++)
            {
                _dice[i] = CreateDie(i == 0 ? new Vector3(-0.75f, 1.0f, 0f) : new Vector3(0.75f, 1.0f, 0f));
                AddChild(_dice[i]);
            }
        }

        public async System.Threading.Tasks.Task Roll(DiceResult result)
        {
            for (var i = 0; i < _dice.Length; i++)
            {
                var die = _dice[i];
                var value = i == 0 ? result.First : result.Second;
                var spin = die.RotationDegrees + new Vector3(720f, 900f, 540f);
                var tween = CreateTween().SetParallel();
                tween.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                tween.TweenProperty(die, "rotation_degrees", spin, 0.9f);
                tween.TweenProperty(die, "position:y", 1.85f, 0.45f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
                tween.Chain().TweenProperty(die, "position:y", 1.0f, 0.45f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
                await ToSignal(tween, Tween.SignalName.Finished);
                die.RotationDegrees = FaceRotation(value);
            }
            await ToSignal(GetTree().CreateTimer(0.55f), SceneTreeTimer.SignalName.Timeout);
        }

        private static MeshInstance3D CreateDie(Vector3 position)
        {
            var die = new MeshInstance3D { Name = "Die", Position = position, Mesh = new BoxMesh { Size = new Vector3(0.95f, 0.95f, 0.95f) }, MaterialOverride = Material(Colors.White) };
            AddFacePips(die, Vector3.Up, 1);
            AddFacePips(die, Vector3.Down, 6);
            AddFacePips(die, Vector3.Forward, 2);
            AddFacePips(die, Vector3.Back, 5);
            AddFacePips(die, Vector3.Right, 3);
            AddFacePips(die, Vector3.Left, 4);
            return die;
        }

        private static void AddFacePips(Node3D die, Vector3 normal, int value)
        {
            var spots = PipSpots(value);
            for (var i = 0; i < spots.Length; i++)
            {
                var pip = new MeshInstance3D { Name = "Pip_" + value + "_" + i, Mesh = new SphereMesh { Radius = 0.085f, Height = 0.17f, RadialSegments = 10, Rings = 6 }, MaterialOverride = Material(new Color("#243447")) };
                pip.Position = FacePosition(normal, spots[i]);
                die.AddChild(pip);
            }
        }

        private static Vector3 FacePosition(Vector3 normal, Vector2 spot)
        {
            if (normal == Vector3.Up || normal == Vector3.Down) return new Vector3(spot.X, normal.Y * 0.51f, spot.Y);
            if (normal == Vector3.Forward || normal == Vector3.Back) return new Vector3(spot.X, spot.Y, normal.Z * 0.51f);
            return new Vector3(normal.X * 0.51f, spot.Y, spot.X);
        }

        private static Vector2[] PipSpots(int value)
        {
            var a = new Vector2(-0.2f, -0.2f); var b = new Vector2(0.2f, 0.2f); var c = new Vector2(-0.2f, 0.2f); var d = new Vector2(0.2f, -0.2f); var center = Vector2.Zero;
            switch (value)
            {
                case 1: return new[] { center };
                case 2: return new[] { a, b };
                case 3: return new[] { a, center, b };
                case 4: return new[] { a, c, d, b };
                case 5: return new[] { a, c, center, d, b };
                default: return new[] { a, c, new Vector2(-0.2f, 0f), new Vector2(0.2f, 0f), d, b };
            }
        }

        private static Vector3 FaceRotation(int value)
        {
            switch (value)
            {
                case 2: return new Vector3(90f, 0f, 0f);
                case 3: return new Vector3(0f, 0f, 90f);
                case 4: return new Vector3(0f, 0f, -90f);
                case 5: return new Vector3(-90f, 0f, 0f);
                case 6: return new Vector3(180f, 0f, 0f);
                default: return Vector3.Zero;
            }
        }

        private static StandardMaterial3D Material(Color color) { return new StandardMaterial3D { AlbedoColor = color, Roughness = 0.45f }; }
    }
}
