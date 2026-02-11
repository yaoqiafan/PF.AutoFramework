using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace PF.UI.Shared.Drawing;

public static class GeometryHelper
{
    public static void ApplyTransform(this IList<Point> points, GeneralTransform transform)
    {
        for (var i = 0; i < points.Count; i++) points[i] = transform.Transform(points[i]);
    }

    public static Rect Bounds(this Size size)
    {
        return new(0.0, 0.0, size.Width, size.Height);
    }

    public static Point Center(this Rect rect)
    {
        return new(rect.X + rect.Width / 2.0, rect.Y + rect.Height / 2.0);
    }

    public static Vector Subtract(this Point lhs, Point rhs)
    {
        return new(lhs.X - rhs.X, lhs.Y - rhs.Y);
    }

    public static Thickness Subtract(this Rect lhs, Rect rhs)
    {
        return new(rhs.Left - lhs.Left,
            rhs.Top - lhs.Top, lhs.Right - rhs.Right,
            lhs.Bottom - rhs.Bottom);
    }

    public static Point Lerp(Point pointA, Point pointB, double alpha)
    {
        return new(
            MathHelper.Lerp(pointA.X, pointB.X, alpha),
            MathHelper.Lerp(pointA.Y, pointB.Y, alpha));
    }

    public static Vector Lerp(Vector vectorA, Vector vectorB, double alpha)
    {
        return new(MathHelper.Lerp(vectorA.X, vectorB.X, alpha),
            MathHelper.Lerp(vectorA.Y, vectorB.Y, alpha));
    }

    public static double Distance(Point lhs, Point rhs)
    {
        var num = lhs.X - rhs.X;
        var num2 = lhs.Y - rhs.Y;
        return Math.Sqrt(num * num + num2 * num2);
    }

    public static Rect Resize(this Rect rect, double ratio)
    {
        return rect.Resize(ratio, ratio);
    }

    public static Rect Resize(this Rect rect, double ratioX, double ratioY)
    {
        var point = rect.Center();
        var width = rect.Width * ratioX;
        var height = rect.Height * ratioY;
        return new Rect(point.X - width / 2.0, point.Y - height / 2.0, width, height);
    }

    public static Rect GetStretchBound(Rect logicalBound, Stretch stretch, Size aspectRatio)
    {
        if (stretch == Stretch.None) stretch = Stretch.Fill;
        if (stretch == Stretch.Fill || !aspectRatio.HasValidArea()) return logicalBound;
        var point = logicalBound.Center();
        if (stretch == Stretch.Uniform)
        {
            if (aspectRatio.Width * logicalBound.Height < logicalBound.Width * aspectRatio.Height)
                logicalBound.Width = logicalBound.Height * aspectRatio.Width / aspectRatio.Height;
            else
                logicalBound.Height = logicalBound.Width * aspectRatio.Height / aspectRatio.Width;
        }
        else if (stretch == Stretch.UniformToFill)
        {
            if (aspectRatio.Width * logicalBound.Height < logicalBound.Width * aspectRatio.Height)
                logicalBound.Height = logicalBound.Width * aspectRatio.Height / aspectRatio.Width;
            else
                logicalBound.Width = logicalBound.Height * aspectRatio.Width / aspectRatio.Height;
        }

        return new Rect(point.X - logicalBound.Width / 2.0, point.Y - logicalBound.Height / 2.0,
            logicalBound.Width, logicalBound.Height);
    }

    public static Point GetArcPoint(double degree)
    {
        var a = degree * 3.1415926535897931 / 180.0;
        return new Point(0.5 + 0.5 * Math.Sin(a), 0.5 - 0.5 * Math.Cos(a));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Point GetArcPoint(double degree, Rect bound)
    {
        var arcPoint = GetArcPoint(degree);
        return RelativeToAbsolutePoint(bound, arcPoint);
    }

    public static Point RelativeToAbsolutePoint(Rect bound, Point relative)
    {
        return new(bound.X + relative.X * bound.Width, bound.Y + relative.Y * bound.Height);
    }

    public static double SquaredDistance(Point lhs, Point rhs)
    {
        var num = lhs.X - rhs.X;
        var num2 = lhs.Y - rhs.Y;
        return num * num + num2 * num2;
    }

    public static Point Midpoint(Point lhs, Point rhs)
    {
        return new((lhs.X + rhs.X) / 2.0, (lhs.Y + rhs.Y) / 2.0);
    }

    public static bool HasValidArea(this Size size)
    {
        return size.Width > 0.0 && size.Height > 0.0 && !double.IsInfinity(size.Width) &&
               !double.IsInfinity(size.Height);
    }

    public static Rect Inflate(Rect rect, double offset)
    {
        return Inflate(rect, new Thickness(offset));
    }

    public static Rect Inflate(Rect rect, Thickness thickness)
    {
        var width = rect.Width + thickness.Left + thickness.Right;
        var height = rect.Height + thickness.Top + thickness.Bottom;
        var x = rect.X - thickness.Left;
        if (width < 0.0)
        {
            x += width / 2.0;
            width = 0.0;
        }

        var y = rect.Y - thickness.Top;
        if (height < 0.0)
        {
            y += height / 2.0;
            height = 0.0;
        }

        return new Rect(x, y, width, height);
    }

    public static double Dot(Point lhs, Point rhs)
    {
        return lhs.X * rhs.X + lhs.Y * rhs.Y;
    }

    public static double Dot(Vector lhs, Vector rhs)
    {
        return lhs.X * rhs.X + lhs.Y * rhs.Y;
    }

    public static Point Plus(this Point lhs, Point rhs)
    {
        return new(lhs.X + rhs.X, lhs.Y + rhs.Y);
    }

    public static Point Minus(this Point lhs, Point rhs)
    {
        return new(lhs.X - rhs.X, lhs.Y - rhs.Y);
    }

    public static Vector Normal(Point lhs, Point rhs)
    {
        return new Vector(lhs.Y - rhs.Y, rhs.X - lhs.X).Normalized();
    }

    public static Vector Normalized(this Vector vector)
    {
        var vector2 = new Vector(vector.X, vector.Y);
        var length = vector2.Length;
        if (MathHelper.IsVerySmall(length)) return new Vector(0.0, 1.0);
        return vector2 / length;
    }

    public static double Determinant(Point lhs, Point rhs)
    {
        return lhs.X * rhs.Y - lhs.Y * rhs.X;
    }

    public static bool EnsureSegmentType<T>(out T result, IList<PathSegment> list, int index,
        Func<T> factory) where T : PathSegment
    {
        result = list[index] as T;
        if (result == null)
        {
            list[index] = result = factory();
            return true;
        }

        return false;
    }

    public static bool EnsureGeometryType<T>(out T result, ref Geometry value, Func<T> factory)
        where T : Geometry
    {
        result = value as T;
        if (result == null)
        {
            value = result = factory();
            return true;
        }

        return false;
    }
}
