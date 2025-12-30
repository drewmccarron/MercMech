using UnityEngine;

namespace MercMech.Common
{
    public static class InputUtils
    {
        public const float MoveDeadzone = 0.2f;

        public static int AxisToDir(float axis) => AxisToDir(axis, MoveDeadzone);

        public static int AxisToDir(float axis, float deadzone)
        {
            if (axis > deadzone) return 1;
            if (axis < -deadzone) return -1;
            return 0;
        }
    }
}