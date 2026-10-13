
namespace StargateAPI.Domain
{
    /// <summary>
    /// A tracked person, identified by a stable application-generated UUIDv7. Name is the
    /// authoritative display name and is not unique; NormalizedName is its derived matching key.
    /// </summary>
    public class Person
    {
        public const int MaxNamePartLength = 100;

        // For EF Core materialization.
        private Person()
        {
        }

        public Guid Id { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public string NormalizedName { get; private set; } = string.Empty;

        public string? FirstName { get; private set; }

        public string? MiddleName { get; private set; }

        public string? LastName { get; private set; }

        /// <summary>
        /// Concurrency token, replaced on every aggregate change. Deliberately a random (v4) Guid:
        /// it is not an ID, and a v7 value would disclose when the person last changed.
        /// </summary>
        public Guid Version { get; private set; }

        public virtual AstronautDetail? AstronautDetail { get; private set; }

        public virtual ICollection<AstronautDuty> AstronautDuties { get; private set; } = new HashSet<AstronautDuty>();

        /// <summary>Creates a new identity. Callers validate input first; namesakes are allowed.</summary>
        public static Person Create(string name, string? firstName = null, string? middleName = null, string? lastName = null)
        {
            var person = new Person
            {
                Id = Guid.CreateVersion7(),
                Version = Guid.NewGuid()
            };
            person.SetNames(name, firstName, middleName, lastName);
            return person;
        }

        /// <summary>Marks an aggregate change so concurrent writers based on the old version conflict.</summary>
        public void Touch() => Version = Guid.NewGuid();

        /// <summary>
        /// Replaces all name fields (PUT semantics: omitted parts become null). Returns false, and
        /// leaves the version unchanged, when nothing actually changes.
        /// </summary>
        public bool ReplaceNames(string name, string? firstName, string? middleName, string? lastName)
        {
            if (Name == name.Trim()
                && FirstName == NullIfBlank(firstName)
                && MiddleName == NullIfBlank(middleName)
                && LastName == NullIfBlank(lastName))
            {
                return false;
            }

            SetNames(name, firstName, middleName, lastName);
            Touch();
            return true;
        }

        /// <summary>
        /// Legacy name-only rename. Clears the structured parts when the display name changes so stale
        /// parts cannot misrepresent the person; an unchanged name is a no-op that preserves them.
        /// </summary>
        public bool ChangeDisplayName(string name)
        {
            if (Name == name.Trim())
            {
                return false;
            }

            SetNames(name, null, null, null);
            Touch();
            return true;
        }

        private void SetNames(string name, string? firstName, string? middleName, string? lastName)
        {
            Name = name.Trim();
            NormalizedName = NameNormalizer.Normalize(Name);
            FirstName = NullIfBlank(firstName);
            MiddleName = NullIfBlank(middleName);
            LastName = NullIfBlank(lastName);
        }

        private static string? NullIfBlank(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
