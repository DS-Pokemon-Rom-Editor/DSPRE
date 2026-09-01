using System;

namespace DSPRE.ROMFiles
{
    /// <summary>The part of the day the games divide the clock into (system/timezone.h).</summary>
    public enum FieldTimeZone
    {
        Morning = 0,
        Noon = 1,
        Evening = 2,
        Night = 3,
        Midnight = 4,
    }

    /// <summary>What time of day it is, and which animation that picks. </summary>
    public static class FieldTimeOfDay
    {
        /// <summary>
        /// The hour of the day each part covers, straight from GF_RTC_ConvertHourToTimeZone: midnight until
        /// 4, morning until 10, noon until 17, evening until 20, then night.
        /// </summary>
        private static readonly FieldTimeZone[] ByHour =
        {
            FieldTimeZone.Midnight, FieldTimeZone.Midnight, FieldTimeZone.Midnight, FieldTimeZone.Midnight,   // 00:00-03:59
            FieldTimeZone.Morning, FieldTimeZone.Morning, FieldTimeZone.Morning,
            FieldTimeZone.Morning, FieldTimeZone.Morning, FieldTimeZone.Morning,                          // 04:00-09:59
            FieldTimeZone.Noon, FieldTimeZone.Noon, FieldTimeZone.Noon, FieldTimeZone.Noon,
            FieldTimeZone.Noon, FieldTimeZone.Noon, FieldTimeZone.Noon,                                   // 10:00-16:59
            FieldTimeZone.Evening, FieldTimeZone.Evening, FieldTimeZone.Evening,                          // 17:00-19:59
            FieldTimeZone.Night, FieldTimeZone.Night, FieldTimeZone.Night, FieldTimeZone.Night,                // 20:00-23:59
        };

        /// <summary>Which of a model's four animations each part of the day shows. </summary>
        private static readonly int[] AnimationForZone = { 0, 1, 2, 3, 3 };

        /// <summary>The part of the day an hour falls in.</summary>
        public static FieldTimeZone ZoneForHour(int hour)
        {
            if (hour < 0 || hour > 23) hour = ((hour % 24) + 24) % 24;
            return ByHour[hour];
        }

        /// <summary>Which of a model's animations plays at this hour.</summary>
        public static int AnimationIndexForHour(int hour) => AnimationForZone[(int)ZoneForHour(hour)];

        /// <summary>Which of a model's animations plays in this part of the day.</summary>
        public static int AnimationIndexForZone(FieldTimeZone zone) => AnimationForZone[(int)zone];

        /// <summary>The part of the day it is right now by the computer's clock.</summary>
        public static FieldTimeZone Now => ZoneForHour(DateTime.Now.Hour);

        /// <summary>A readable name, for a picker to show.</summary>
        /// <summary>
        /// Whether the games count this as night, which decides which of a header's two music numbers
        /// plays. GF_RTC_IsNightTime in pm_rtc.c:434 counts the small hours and the night, nothing else.
        /// </summary>
        public static bool IsNight(FieldTimeZone zone) =>
            zone == FieldTimeZone.Night || zone == FieldTimeZone.Midnight;

        public static string Name(FieldTimeZone zone)
        {
            switch (zone)
            {
                case FieldTimeZone.Morning: return "Morning";
                case FieldTimeZone.Noon: return "Day";
                case FieldTimeZone.Evening: return "Evening";
                case FieldTimeZone.Night: return "Night";
                default: return "Small hours";
            }
        }

        /// <summary>The hours a part of the day covers, for a picker to describe itself.</summary>
        public static string Hours(FieldTimeZone zone)
        {
            switch (zone)
            {
                case FieldTimeZone.Morning: return "04:00 to 09:59";
                case FieldTimeZone.Noon: return "10:00 to 16:59";
                case FieldTimeZone.Evening: return "17:00 to 19:59";
                case FieldTimeZone.Night: return "20:00 to 23:59";
                default: return "00:00 to 03:59";
            }
        }
    }
}
