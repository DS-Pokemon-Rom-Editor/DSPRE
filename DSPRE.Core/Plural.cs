namespace DSPRE
{
    /// <summary>Counts in user text: "1 map", "3 maps".</summary>
    public static class Plural
    {
        public static string Of(int count, string one, string many = null) => $"{count} {(count == 1 ? one : many ?? one + "s")}";
    }
}
