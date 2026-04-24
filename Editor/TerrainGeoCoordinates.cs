using System;
using UnityEngine;

[Serializable]
public enum LatitudeHemisphere
{
    North = 1,
    South = -1
}

[Serializable]
public enum LongitudeHemisphere
{
    East = 1,
    West = -1
}

[Serializable]
public struct LatitudeDdm
{
    public LatitudeHemisphere hemisphere;
    public int degrees;
    public int minutes;
    public int tenthsOfMinutes;

    public double ToDecimalDegrees()
    {
        Validate(90);
        var value = degrees + ((minutes + (tenthsOfMinutes / 10.0)) / 60.0);
        return (int)hemisphere * value;
    }

    public static LatitudeDdm Create(LatitudeHemisphere hemisphere, int degrees, int minutes, int tenthsOfMinutes)
    {
        return new LatitudeDdm
        {
            hemisphere = hemisphere,
            degrees = degrees,
            minutes = minutes,
            tenthsOfMinutes = tenthsOfMinutes
        };
    }

    public static LatitudeDdm FromDecimalDegrees(double decimalDegrees)
    {
        var hemisphere = decimalDegrees >= 0d ? LatitudeHemisphere.North : LatitudeHemisphere.South;
        var absolute = Math.Abs(decimalDegrees);
        var degrees = (int)Math.Floor(absolute);
        var totalMinutes = (absolute - degrees) * 60d;
        var roundedTenths = (int)Math.Round(totalMinutes * 10d, MidpointRounding.AwayFromZero);
        var minutes = roundedTenths / 10;
        var tenths = roundedTenths % 10;

        if (minutes >= 60)
        {
            minutes -= 60;
            degrees += 1;
        }

        degrees = Mathf.Clamp(degrees, 0, 90);
        return Create(hemisphere, degrees, minutes, tenths);
    }

    private void Validate(int maxDegrees)
    {
        if (degrees < 0 || degrees > maxDegrees)
        {
            throw new InvalidOperationException($"Latitude degrees must be between 0 and {maxDegrees}.");
        }

        if (minutes < 0 || minutes > 59)
        {
            throw new InvalidOperationException("Latitude minutes must be between 0 and 59.");
        }

        if (tenthsOfMinutes < 0 || tenthsOfMinutes > 9)
        {
            throw new InvalidOperationException("Latitude tenths of minutes must be between 0 and 9.");
        }
    }
}

[Serializable]
public struct LongitudeDdm
{
    public LongitudeHemisphere hemisphere;
    public int degrees;
    public int minutes;
    public int tenthsOfMinutes;

    public double ToDecimalDegrees()
    {
        Validate(180);
        var value = degrees + ((minutes + (tenthsOfMinutes / 10.0)) / 60.0);
        return (int)hemisphere * value;
    }

    public static LongitudeDdm Create(LongitudeHemisphere hemisphere, int degrees, int minutes, int tenthsOfMinutes)
    {
        return new LongitudeDdm
        {
            hemisphere = hemisphere,
            degrees = degrees,
            minutes = minutes,
            tenthsOfMinutes = tenthsOfMinutes
        };
    }

    public static LongitudeDdm FromDecimalDegrees(double decimalDegrees)
    {
        var hemisphere = decimalDegrees >= 0d ? LongitudeHemisphere.East : LongitudeHemisphere.West;
        var absolute = Math.Abs(decimalDegrees);
        var degrees = (int)Math.Floor(absolute);
        var totalMinutes = (absolute - degrees) * 60d;
        var roundedTenths = (int)Math.Round(totalMinutes * 10d, MidpointRounding.AwayFromZero);
        var minutes = roundedTenths / 10;
        var tenths = roundedTenths % 10;

        if (minutes >= 60)
        {
            minutes -= 60;
            degrees += 1;
        }

        degrees = Mathf.Clamp(degrees, 0, 180);
        return Create(hemisphere, degrees, minutes, tenths);
    }

    private void Validate(int maxDegrees)
    {
        if (degrees < 0 || degrees > maxDegrees)
        {
            throw new InvalidOperationException($"Longitude degrees must be between 0 and {maxDegrees}.");
        }

        if (minutes < 0 || minutes > 59)
        {
            throw new InvalidOperationException("Longitude minutes must be between 0 and 59.");
        }

        if (tenthsOfMinutes < 0 || tenthsOfMinutes > 9)
        {
            throw new InvalidOperationException("Longitude tenths of minutes must be between 0 and 9.");
        }
    }
}
