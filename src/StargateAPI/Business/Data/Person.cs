using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Domain;
using System.ComponentModel.DataAnnotations.Schema;

namespace StargateAPI.Business.Data
{
    /// <summary>
    /// A tracked person, identified by a stable application-generated UUIDv7. Name is the
    /// authoritative display name and is not unique; NormalizedName is its derived matching key.
    /// </summary>
    [Table("Person")]
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

        /// <summary>Concurrency token, replaced on every aggregate change. Not an ID or a timestamp.</summary>
        public Guid Version { get; private set; }

        public virtual AstronautDetail? AstronautDetail { get; set; }

        public virtual ICollection<AstronautDuty> AstronautDuties { get; set; } = new HashSet<AstronautDuty>();

        /// <summary>Creates a new identity. Callers validate input first; namesakes are allowed.</summary>
        public static Person Create(string name, string? firstName = null, string? middleName = null, string? lastName = null)
        {
            var person = new Person
            {
                Id = Guid.CreateVersion7(),
                Version = Guid.CreateVersion7()
            };
            person.SetNames(name, firstName, middleName, lastName);
            return person;
        }

        /// <summary>Marks an aggregate change so concurrent writers based on the old version conflict.</summary>
        public void Touch() => Version = Guid.CreateVersion7();

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

    public class PersonConfiguration : IEntityTypeConfiguration<Person>
    {
        public void Configure(EntityTypeBuilder<Person> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Name).IsRequired().HasMaxLength(NameNormalizer.MaxNameLength);
            builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(NameNormalizer.MaxNormalizedLength);
            builder.Property(x => x.FirstName).HasMaxLength(Person.MaxNamePartLength);
            builder.Property(x => x.MiddleName).HasMaxLength(Person.MaxNamePartLength);
            builder.Property(x => x.LastName).HasMaxLength(Person.MaxNamePartLength);
            builder.Property(x => x.Version).IsConcurrencyToken();

            // Supports name lookup and the deterministic (NormalizedName, Id) directory ordering.
            builder.HasIndex(x => new { x.NormalizedName, x.Id });

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Person_Name_NotBlank", "length(trim(\"Name\")) > 0");
                t.HasCheckConstraint("CK_Person_NormalizedName_NotBlank", "length(trim(\"NormalizedName\")) > 0");
            });

            builder.HasOne(z => z.AstronautDetail).WithOne(z => z.Person).HasForeignKey<AstronautDetail>(z => z.PersonId);
            builder.HasMany(z => z.AstronautDuties).WithOne(z => z.Person).HasForeignKey(z => z.PersonId);
        }
    }
}
