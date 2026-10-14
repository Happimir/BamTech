using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StargateAPI.Domain;

namespace StargateAPI.Infrastructure.Persistence.Configurations
{
    public class AstronautDutyConfiguration : IEntityTypeConfiguration<AstronautDuty>
    {
        public void Configure(EntityTypeBuilder<AstronautDuty> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Rank).IsRequired().HasMaxLength(AstronautDetail.MaxRankLength);
            builder.Property(x => x.DutyTitle).IsRequired().HasMaxLength(AstronautDetail.MaxDutyTitleLength);

            // Strictly increasing start dates per person, and at most one current duty per person.
            builder.HasIndex(x => new { x.PersonId, x.DutyStartDate }).IsUnique();
            builder.HasIndex(x => x.PersonId)
                .IsUnique()
                .HasFilter("\"DutyEndDate\" IS NULL")
                .HasDatabaseName("IX_AstronautDuty_PersonId_Open");

            builder.ToTable("AstronautDuty", t =>
            {
                t.HasCheckConstraint("CK_AstronautDuty_Rank_NotBlank", "length(trim(\"Rank\")) > 0");
                t.HasCheckConstraint("CK_AstronautDuty_DutyTitle_NotBlank", "length(trim(\"DutyTitle\")) > 0");
                t.HasCheckConstraint("CK_AstronautDuty_End_AfterStart",
                    "\"DutyEndDate\" IS NULL OR \"DutyEndDate\" >= \"DutyStartDate\"");
            });
        }
    }
}
