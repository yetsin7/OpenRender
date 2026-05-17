using System;
using System.Numerics;

namespace OpenRender.Tools;

public struct Ray
{
    public Vector3 Origin;
    public Vector3 Direction;

    public Ray(Vector3 origin, Vector3 direction)
    {
        Origin = origin;
        Direction = Vector3.Normalize(direction);
    }

    public float? Intersects(BoundingBox box)
    {
        float tMin = (box.Min.X - Origin.X) / Direction.X;
        float tMax = (box.Max.X - Origin.X) / Direction.X;

        if (tMin > tMax) (tMin, tMax) = (tMax, tMin);

        float tyMin = (box.Min.Y - Origin.Y) / Direction.Y;
        float tyMax = (box.Max.Y - Origin.Y) / Direction.Y;

        if (tyMin > tyMax) (tyMin, tyMax) = (tyMax, tyMin);

        if ((tMin > tyMax) || (tyMin > tMax)) return null;

        if (tyMin > tMin) tMin = tyMin;
        if (tyMax < tMax) tMax = tyMax;

        float tzMin = (box.Min.Z - Origin.Z) / Direction.Z;
        float tzMax = (box.Max.Z - Origin.Z) / Direction.Z;

        if (tzMin > tzMax) (tzMin, tzMax) = (tzMax, tzMin);

        if ((tMin > tzMax) || (tzMin > tMax)) return null;

        if (tzMin > tMin) tMin = tzMin;
        if (tzMax < tMax) tMax = tzMax;

        return tMin;
    }
}

public struct BoundingBox
{
    public Vector3 Min;
    public Vector3 Max;

    public BoundingBox(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public static BoundingBox FromPoints(IEnumerable<Vector3> points)
    {
        Vector3 min = new Vector3(float.MaxValue);
        Vector3 max = new Vector3(float.MinValue);
        foreach (var p in points)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return new BoundingBox(min, max);
    }
}
