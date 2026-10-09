using System;

namespace Team8.Core
{
    public enum Condition
    {
        Joystick,
        Teleport,
        Trail
    }

    public static class ConditionNames
    {
        // lowercase names used in every CSV
        public static string ToCsv(Condition c)
        {
            switch (c)
            {
                case Condition.Joystick: return "joystick";
                case Condition.Teleport: return "teleport";
                case Condition.Trail: return "trail";
                default: throw new ArgumentOutOfRangeException(nameof(c));
            }
        }

        // what the operator panel shows
        public static string ToLabel(Condition c)
        {
            switch (c)
            {
                case Condition.Joystick: return "Joystick";
                case Condition.Teleport: return "Teleport";
                case Condition.Trail: return "Teleport + Trail";
                default: throw new ArgumentOutOfRangeException(nameof(c));
            }
        }
    }
}
