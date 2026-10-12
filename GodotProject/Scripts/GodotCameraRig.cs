using Godot;

namespace Richman.GodotPresentation
{
    public partial class GodotCameraRig : Node3D
    {
        private Camera3D _camera;
        private Vector3 _focusPoint;
        private float _yaw;
        private float _pitch = 48f;
        private float _distance = 20f;

        public override void _Ready()
        {
            _camera = new Camera3D { Current = true, Fov = 42f };
            AddChild(_camera);
            _focusPoint = Vector3.Zero;
            UpdateCamera();
        }

        public void Focus(Vector3 point, float distance)
        {
            _focusPoint = point;
            _distance = Mathf.Clamp(distance, 8f, 28f);
        }

        public void FocusOverview()
        {
            Focus(Vector3.Zero, 20f);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseMotion motion && (motion.ButtonMask & MouseButtonMask.Right) != 0)
            {
                _yaw -= motion.Relative.X * 0.35f;
                _pitch = Mathf.Clamp(_pitch + motion.Relative.Y * 0.25f, 25f, 75f);
            }
            else if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
            {
                if (mouseButton.ButtonIndex == MouseButton.WheelUp) _distance = Mathf.Max(8f, _distance - 1f);
                if (mouseButton.ButtonIndex == MouseButton.WheelDown) _distance = Mathf.Min(28f, _distance + 1f);
            }
        }

        public override void _Process(double delta)
        {
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            if (_camera == null) return;
            var rotation = new Basis(Vector3.Up, Mathf.DegToRad(_yaw)) * new Basis(Vector3.Right, Mathf.DegToRad(-_pitch));
            var desired = _focusPoint + rotation * new Vector3(0f, 0f, _distance);
            _camera.GlobalPosition = _camera.GlobalPosition.Lerp(desired, 0.12f);
            _camera.LookAt(_focusPoint, Vector3.Up);
        }
    }
}
