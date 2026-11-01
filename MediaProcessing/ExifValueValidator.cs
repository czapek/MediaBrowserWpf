using System;
using System.Globalization;

namespace MediaProcessing
{
    public static class ExifValueValidator
    {
        public static bool IsInvalidValue(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
                return true;

            return value.IndexOf("NaN", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Infinity", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("2147483648", StringComparison.Ordinal) >= 0;
        }

        public static bool TryParseGpsCoordinate(string value, bool isLatitude, string reference, out double result)
        {
            result = 0;

            if (IsInvalidValue(value))
                return false;

            string[] parts = value.Split('"');
            if (parts.Length < 2)
                return false;

            string[] minuteSecond = parts[1].Split('\'');
            if (minuteSecond.Length < 2)
                return false;

            double degrees;
            double minutes;
            double seconds;

            if (!TryParseComponent(parts[0], out degrees)
                || !TryParseComponent(minuteSecond[0], out minutes)
                || !TryParseComponent(minuteSecond[1], out seconds))
                return false;

            double maxDegrees = isLatitude ? 90.0 : 180.0;

            if (degrees < 0 || degrees > maxDegrees
                || minutes < 0 || minutes >= 60
                || seconds < 0 || seconds >= 60)
                return false;

            result = degrees + minutes / 60.0 + seconds / 3600.0;

            if (result > maxDegrees)
                return false;

            if (!String.IsNullOrWhiteSpace(reference))
            {
                char hemisphere = Char.ToUpperInvariant(reference.Trim()[0]);

                if ((isLatitude && hemisphere == 'S') || (!isLatitude && hemisphere == 'W'))
                    result = -result;
            }

            return true;
        }

        private static bool TryParseComponent(string value, out double result)
        {
            value = value.Trim();

            if (value.Length == 0)
            {
                result = 0;
                return false;
            }

            if (Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                return true;

            if (Double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result))
                return true;

            return Double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }
    }
}
