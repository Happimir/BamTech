using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StargateAPI.Domain;

namespace StargateAPI.Infrastructure.Persistence.Configurations
{
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

            builder.ToTable("Person", t =>
            {
                t.HasCheckConstraint("CK_Person_Name_NotBlank", "length(trim(\"Name\")) > 0");
                t.HasCheckConstraint("CK_Person_NormalizedName_NotBlank", "length(trim(\"NormalizedName\")) > 0");
            });

            builder.HasOne(z => z.AstronautDetail).WithOne(z => z.Person).HasForeignKey<AstronautDetail>(z => z.PersonId);
            builder.HasMany(z => z.AstronautDuties).WithOne(z => z.Person).HasForeignKey(z => z.PersonId);
        }
    }
}
