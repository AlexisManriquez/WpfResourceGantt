using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WpfResourceGantt.ProjectManagement.Models;

namespace WpfResourceGantt.ProjectManagement.Services
{
    public enum DependencyType { FS, SS, FF, SF }

    public class DependencyInfo
    {
        public string PredecessorId { get; set; }
        public DependencyType Type { get; set; } = DependencyType.FS;
        public int LagDays { get; set; }
    }

    public static class PredecessorParser
    {
        // Matches an optional dependency type suffix followed by an optional lag at the END of a token.
        // The ID is everything before this suffix. This avoids excluding letters from IDs.
        private static readonly Regex SuffixRegex = new Regex(
            @"(?<type>FS|SS|FF|SF)(?<lag>[+-]\d+(?:d|w)?)?$",
            RegexOptions.IgnoreCase);

        public static List<DependencyInfo> Parse(string input)
        {
            var results = new List<DependencyInfo>();
            if (string.IsNullOrWhiteSpace(input)) return results;

            var parts = input.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var token = part.Trim();
                if (string.IsNullOrEmpty(token)) continue;

                var info = new DependencyInfo();
                var suffixMatch = SuffixRegex.Match(token);

                if (suffixMatch.Success && suffixMatch.Index > 0)
                {
                    // ID is everything before the suffix
                    info.PredecessorId = token.Substring(0, suffixMatch.Index).Trim();
                    info.Type = ParseType(suffixMatch.Groups["type"].Value);
                    info.LagDays = ParseLag(suffixMatch.Groups["lag"].Value);
                }
                else
                {
                    // No suffix found — entire token is the ID, default FS
                    info.PredecessorId = token;
                    info.Type = DependencyType.FS;
                    info.LagDays = 0;
                }

                if (!string.IsNullOrEmpty(info.PredecessorId))
                    results.Add(info);
            }
            return results;
        }

        private static DependencyType ParseType(string typeStr)
        {
            if (string.IsNullOrWhiteSpace(typeStr)) return DependencyType.FS;
            if (Enum.TryParse<DependencyType>(typeStr, true, out var result)) return result;
            return DependencyType.FS;
        }

        private static int ParseLag(string lagStr)
        {
            if (string.IsNullOrWhiteSpace(lagStr)) return 0;

            var numericPart = new string(lagStr.Where(c => char.IsDigit(c) || c == '-' || c == '+').ToArray());
            if (int.TryParse(numericPart, out int days))
            {
                if (lagStr.EndsWith("w", StringComparison.OrdinalIgnoreCase))
                    return days * 5; // Assuming 5-day work week
                return days;
            }
            return 0;
        }
    }
}
