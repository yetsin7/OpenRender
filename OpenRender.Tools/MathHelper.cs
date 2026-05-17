using System;

namespace OpenRender.Tools;

public static class MathHelper
{
    public const float Pi = MathF.PI;
    public const float HalfPi = MathF.PI / 2.0f;
    public const float TwoPi = MathF.PI * 2.0f;

    public static float ToRadians(float degrees) => degrees * (Pi / 180.0f);
    public static float ToDegrees(float radians) => radians * (180.0f / Pi);

    public static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0, 1);
}
