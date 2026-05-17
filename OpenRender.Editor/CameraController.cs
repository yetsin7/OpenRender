using System;
using System.Numerics;
using OpenRender.Scene;
using OpenRender.Tools;

namespace OpenRender.Editor;

/// <summary>
/// Professional AAA Camera Controller with physics-based movement.
/// Inspired by Lumion and Unreal Engine.
/// </summary>
public class CameraController
{
    // Settings
    public float BaseMoveSpeed { get; set; } = 12.0f;
    public float BoostMultiplier { get; set; } = 4.0f;
    public float MouseSensitivity { get; set; } = 0.002f;
    public float ZoomSensitivity { get; set; } = 2.0f;
    public float Acceleration { get; set; } = 15.0f;
    public float Damping { get; set; } = 10.0f;

    // Internal Physics State
    private Vector3 _velocity = Vector3.Zero;
    private Vector3 _currentPos;
    private Vector3 _currentTarget;
    
    private float _yaw = -MathF.PI / 2;
    private float _pitch = 0;
    private float _orbitDistance = 10.0f;

    private bool _isInitialized = false;

    public void Update(CameraComponent camera, InputState input, EngineTime time)
    {
        if (!_isInitialized)
        {
            _currentPos = camera.Position;
            _currentTarget = camera.Target;
            _yaw = MathHelper.ToRadians(camera.Rotation.Y);
            _pitch = MathHelper.ToRadians(camera.Rotation.X);
            _orbitDistance = Vector3.Distance(_currentPos, _currentTarget);
            if (_orbitDistance < 0.1f) _orbitDistance = 10.0f;
            _isInitialized = true;
        }

        float dt = time.DeltaTime;
        if (dt <= 0) return;

        // --- Rotation (Mouse Look) ---
        if (input.IsRightMouseDown)
        {
            _yaw -= input.MouseDelta.X * MouseSensitivity;
            _pitch = Math.Clamp(_pitch - input.MouseDelta.Y * MouseSensitivity, -MathHelper.HalfPi + 0.01f, MathHelper.HalfPi - 0.01f);
        }

        Quaternion rotation = Quaternion.CreateFromYawPitchRoll(_yaw, _pitch, 0);
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        Vector3 right = Vector3.Transform(Vector3.UnitX, rotation);

        // --- Pan (Middle Mouse) ---
        if (input.IsMiddleMouseDown)
        {
            float panFactor = MathF.Max(1.0f, _orbitDistance) * 0.5f;
            _currentPos -= right * input.MouseDelta.X * MouseSensitivity * panFactor;
            _currentPos += Vector3.UnitY * input.MouseDelta.Y * MouseSensitivity * panFactor;
        }

        // --- Movement (WASDQE) ---
        Vector3 inputDir = Vector3.Zero;
        if (input.IsKeyDown("W")) inputDir += forward;
        if (input.IsKeyDown("S")) inputDir -= forward;
        if (input.IsKeyDown("A")) inputDir -= right;
        if (input.IsKeyDown("D")) inputDir += right;
        if (input.IsKeyDown("E") || input.IsKeyDown("Space")) inputDir += Vector3.UnitY;
        if (input.IsKeyDown("Q") || input.IsKeyDown("Control")) inputDir -= Vector3.UnitY;

        if (inputDir.LengthSquared() > 0)
            inputDir = Vector3.Normalize(inputDir);

        float currentSpeed = BaseMoveSpeed;
        if (input.IsShiftDown) currentSpeed *= BoostMultiplier;

        Vector3 targetVelocity = inputDir * currentSpeed;
        
        // Smooth interpolation of velocity
        _velocity = Vector3.Lerp(_velocity, targetVelocity, Acceleration * dt);
        
        // Apply friction when no input is provided for "inertia" feel
        if (inputDir.LengthSquared() < 0.001f)
            _velocity = Vector3.Lerp(_velocity, Vector3.Zero, Damping * dt);

        _currentPos += _velocity * dt;

        // --- Zoom (Scroll) ---
        if (MathF.Abs(input.MouseWheelDelta) > 0.001f)
        {
            float zoomAmount = input.MouseWheelDelta * ZoomSensitivity * (_orbitDistance * 0.1f + 1.0f);
            _currentPos += forward * zoomAmount;
            _orbitDistance = MathF.Max(0.1f, _orbitDistance - zoomAmount);
        }

        // Sync Target
        _currentTarget = _currentPos + forward * _orbitDistance;

        // Apply back to component
        camera.Position = _currentPos;
        camera.Target = _currentTarget;
        camera.Rotation = new Vector3(MathHelper.ToDegrees(_pitch), MathHelper.ToDegrees(_yaw), 0);
    }
}
