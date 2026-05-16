using System.Numerics;

namespace OpenRender.Core.Scene;

public class Camera
{
    private float _fov = 60f;
    private float _nearPlane = 0.1f;
    private float _farPlane = 100000f;
    private float _aspectRatio = 16f / 9f;

    private Vector3 _position = new(0, 5, 10);
    private Vector3 _target = Vector3.Zero;

    private float _yaw = -90f;
    private float _pitch = 0f;

    private float _moveSpeed = 10f;
    private float _mouseSensitivity = 0.15f;

    private Vector3 _velocity = Vector3.Zero;
    private float _acceleration = 120f;
    private float _deceleration = 10f;
    private bool _isPerspective = true;

    public Vector3 Position
    {
        get => _position;
        set => _position = value;
    }

    public Vector3 Target
    {
        get => _target;
        set => _target = value;
    }

    public float Yaw
    {
        get => _yaw;
        set => _yaw = value;
    }

    public float Pitch
    {
        get => _pitch;
        set => _pitch = Math.Clamp(value, -89f, 89f);
    }

    public float MoveSpeed
    {
        get => _moveSpeed;
        set => _moveSpeed = Math.Max(0.1f, value);
    }

    public float OrbitDistance
    {
        get => Math.Max(0.1f, (_position - _target).Length());
        set
        {
            var clamped = Math.Clamp(value, 0.1f, Math.Max(10f, _farPlane * 0.5f));
            _position = _target - Forward * clamped;
            _moveSpeed = Math.Max(0.5f, clamped * 0.35f);
        }
    }

    public float FieldOfView
    {
        get => _fov;
        set => _fov = Math.Clamp(value, 10f, 120f);
    }

    public float NearPlane
    {
        get => _nearPlane;
        set => _nearPlane = Math.Max(0.01f, value);
    }

    public float FarPlane
    {
        get => _farPlane;
        set => _farPlane = Math.Max(_nearPlane + 1f, value);
    }

    public float AspectRatio
    {
        get => _aspectRatio;
        set => _aspectRatio = Math.Max(0.1f, value);
    }

    public bool IsPerspective
    {
        get => _isPerspective;
        set => _isPerspective = value;
    }

    public Vector3 Forward
    {
        get
        {
            float yawRad = MathF.PI / 180f * _yaw;
            float pitchRad = MathF.PI / 180f * _pitch;

            return Vector3.Normalize(new Vector3(
                MathF.Cos(pitchRad) * MathF.Cos(yawRad),
                MathF.Sin(pitchRad),
                MathF.Cos(pitchRad) * MathF.Sin(yawRad)
            ));
        }
    }

    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));
    public Vector3 Up => Vector3.Normalize(Vector3.Cross(Right, Forward));

    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.CreateLookAt(_position, _position + Forward, Vector3.UnitY);
    }

    public Matrix4x4 GetProjectionMatrix()
    {
        if (!_isPerspective)
        {
            float orthoHeight = Math.Max(2f, OrbitDistance * 0.85f);
            float orthoWidth = orthoHeight * _aspectRatio;
            return Matrix4x4.CreateOrthographic(
                orthoWidth,
                orthoHeight,
                _nearPlane,
                _farPlane);
        }

        return Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 180f * _fov,
            _aspectRatio,
            _nearPlane,
            _farPlane);
    }

    public void Update(Vector3 moveDir, float deltaTime, float speedMul = 1.0f)
    {
        if (moveDir.LengthSquared() > 0)
            _velocity += moveDir * _acceleration * deltaTime;

        _position += _velocity * (_moveSpeed * speedMul) * deltaTime;

        _velocity *= MathF.Pow(1.0f - _deceleration * deltaTime, 1.0f / 60.0f);

        if (_velocity.Length() < 0.01f)
            _velocity = Vector3.Zero;
    }

    public void LookAround(float deltaX, float deltaY)
    {
        _isPerspective = true;
        Rotate(deltaX, deltaY);
    }

    public void Orbit(float deltaX, float deltaY)
    {
        _isPerspective = true;
        float orbitSensitivity = _mouseSensitivity * 0.7f;
        _yaw += deltaX * orbitSensitivity;
        _pitch += -deltaY * orbitSensitivity;
        _pitch = Math.Clamp(_pitch, -89f, 89f);

        // Keep camera at same distance from target when orbiting
        float distance = (_position - _target).Length();
        _position = _target - Forward * distance;
    }

    public void Pan(float deltaX, float deltaY)
    {
        float distance = (_position - _target).Length();
        float panSpeed = distance * 0.002f;

        Vector3 move = -Right * deltaX * panSpeed + Up * deltaY * panSpeed;
        _position += move;
        _target += move;
    }

    public void Rotate(float deltaX, float deltaY)
    {
        _yaw += deltaX * _mouseSensitivity;
        _pitch += -deltaY * _mouseSensitivity;
        _pitch = Math.Clamp(_pitch, -89f, 89f);
    }

    public void Zoom(float delta)
    {
        float distance = OrbitDistance;
        float zoomAmount = Math.Max(0.1f, distance * 0.15f) * delta;
        OrbitDistance = Math.Clamp(distance - zoomAmount, 0.25f, Math.Max(25f, _farPlane * 0.5f));
    }

    public void Reset()
    {
        _position = new Vector3(0, 5, 10);
        _target = Vector3.Zero;
        _yaw = -90f;
        _pitch = 0f;
        _moveSpeed = 10f;
        _velocity = Vector3.Zero;
        _isPerspective = true;
    }

    public void FrameBoundingBox(Vector3 min, Vector3 max)
    {
        var center = (min + max) * 0.5f;
        var size = max - min;
        var maxDim = MathF.Max(size.X, MathF.Max(size.Y, size.Z));

        _target = center;
        _yaw = -135f;
        _pitch = -30f;
        _isPerspective = true;

        float distance = Math.Max(1f, maxDim * 1.5f);
        _position = _target - Forward * distance;

        NearPlane = Math.Max(0.1f, maxDim * 0.0001f);
        FarPlane = Math.Max(1000f, maxDim * 10f);
        
        _moveSpeed = Math.Max(1f, distance * 0.5f);
    }

    public void FramePhotoShot(Vector3 min, Vector3 max)
    {
        var center = (min + max) * 0.5f;
        var size = max - min;
        var maxDim = MathF.Max(size.X, MathF.Max(size.Y, size.Z));

        _target = center + new Vector3(size.X * 0.04f, size.Y * 0.12f, 0f);
        _yaw = -122f;
        _pitch = -18f;
        _fov = 42f;
        _isPerspective = true;

        float distance = Math.Max(2f, maxDim * 1.1f);
        _position = _target - Forward * distance + Right * (maxDim * 0.08f);

        NearPlane = Math.Max(0.1f, maxDim * 0.0001f);
        FarPlane = Math.Max(1000f, maxDim * 10f);
        _moveSpeed = Math.Max(1f, distance * 0.45f);
    }

    public void SetView(string viewType)
    {
        switch (viewType.ToLowerInvariant())
        {
            case "front":
                _yaw = 0f;
                _pitch = 0f;
                _isPerspective = false;
                break;

            case "back":
                _yaw = 180f;
                _pitch = 0f;
                _isPerspective = false;
                break;

            case "left":
                _yaw = 90f;
                _pitch = 0f;
                _isPerspective = false;
                break;

            case "right":
                _yaw = -90f;
                _pitch = 0f;
                _isPerspective = false;
                break;

            case "top":
                _yaw = 0f;
                _pitch = 89f;
                _isPerspective = false;
                break;

            case "bottom":
                _yaw = 0f;
                _pitch = -89f;
                _isPerspective = false;
                break;

            case "isometric":
            case "3d":
                _yaw = -135f;
                _pitch = -30f;
                _isPerspective = true;
                break;
        }
    }

    public void SetViewAndFrame(string viewType, Vector3 min, Vector3 max)
    {
        SetView(viewType);

        var center = (min + max) * 0.5f;
        var size = max - min;
        var maxDim = MathF.Max(size.X, MathF.Max(size.Y, size.Z));

        float distance = Math.Max(1f, maxDim * 1.5f);

        _target = center;
        _position = center - Forward * distance;

        NearPlane = Math.Max(0.1f, maxDim * 0.0001f);
        FarPlane = Math.Max(1000f, maxDim * 10f);
        
        _moveSpeed = Math.Max(1f, distance * 0.5f);
    }
}
