using System.Text.RegularExpressions;

namespace LogCollectorApp.Helpers;

public static class IpMaskHelper
{
    private static readonly Regex IpMaskRegex = new(@"^(\d{1,3}|\*)\.(\d{1,3}|\*)\.(\d{1,3}|\*)\.(\d{1,3}|\*)$", RegexOptions.Compiled);

    public static bool IsValidIpMask(string mask)
    {
        if (string.IsNullOrWhiteSpace(mask) || !IpMaskRegex.IsMatch(mask)) 
            return false;

        mask = mask.Trim();

        if (!IpMaskRegex.IsMatch(mask))
            return false;

        return mask
            .Split('.')
            .All(part => part == "*" || int.TryParse(part, out int number) && number is >= 0 and <= 255);
    }

    public static string ConvertToSqlLikePattern(string mask) => string.IsNullOrWhiteSpace(mask) ? "%" : mask.Replace("*", "%");
}
