using System.Text;

namespace StargateAPI.Domain
{
    /// <summary>
    /// Derives the matching key for person names. This is a search rule, not a uniqueness rule:
    /// several people may share a key. Display spelling is stored separately and never altered.
    /// </summary>
    public static class NameNormalizer
    {
        public const int MaxNameLength = 300;
        public const int MaxNormalizedLength = 1200;

        /// <summary>
        /// The stored display spelling: trimmed and NFC-normalized, case and punctuation untouched.
        /// Canonically equivalent input (a precomposed "é" or "e" plus a combining accent) is then
        /// the same stored value, so re-sending a name is never mistaken for a rename.
        /// Throws <see cref="ArgumentException"/> for invalid UTF-16; validate input first.
        /// </summary>
        public static string Canonical(string value) => value.Trim().Normalize(NormalizationForm.FormC);

        /// <summary>
        /// Trim, NFC, invariant uppercase, NFC again. Accents and internal whitespace are kept:
        /// "José" and "Jose" are different names, as are "Mary Ann" and "Mary  Ann".
        /// </summary>
        public static string Normalize(string name) =>
            name.Trim()
                .Normalize(NormalizationForm.FormC)
                .ToUpperInvariant()
                .Normalize(NormalizationForm.FormC);

        /// <summary>
        /// Validates a name used for lookup or storage and returns its key. Returns false with a
        /// safe message for blank, oversized, or unnormalizable input.
        /// </summary>
        public static bool TryNormalize(string? name, out string normalized, out string? error)
        {
            normalized = string.Empty;

            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Name is required.";
                return false;
            }

            var trimmed = name.Trim();
            if (trimmed.Length > MaxNameLength)
            {
                error = $"Name must be at most {MaxNameLength} characters.";
                return false;
            }

            try
            {
                normalized = Normalize(trimmed);
            }
            catch (ArgumentException)
            {
                // Invalid UTF-16 (e.g. a lone surrogate) cannot be normalized.
                error = "Name contains invalid characters.";
                return false;
            }

            if (normalized.Length > MaxNormalizedLength)
            {
                error = "Name is too long once normalized.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
