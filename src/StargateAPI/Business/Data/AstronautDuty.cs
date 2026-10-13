using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace StargateAPI.Business.Data
{
    /// <summary>One assignment in a person's history. The open duty (no end date) is the current one.</summary>
    [Table("AstronautDuty")]
    public class AstronautDuty
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public Guid PersonId { get; set; }

        public string Rank { get; set; } = string.Empty;

        public string DutyTitle { get; set; } = string.Empty;

        public DateOnly DutyStartDate { get; set; }

        public DateOnly? DutyEndDate { get; set; }

        public virtual Person Person { get; set; } = null!;
    }

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

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_AstronautDuty_Rank_NotBlank", "length(trim(\"Rank\")) > 0");
                t.HasCheckConstraint("CK_AstronautDuty_DutyTitle_NotBlank", "length(trim(\"DutyTitle\")) > 0");
                t.HasCheckConstraint("CK_AstronautDuty_End_AfterStart",
                    "\"DutyEndDate\" IS NULL OR \"DutyEndDate\" >= \"DutyStartDate\"");
            });
        }
    }
}
