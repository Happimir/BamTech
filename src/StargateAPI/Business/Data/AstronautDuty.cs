using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace StargateAPI.Business.Data
{
    /// <summary>One assignment in a person's history. The open duty (no end date) is the current one.</summary>
    [Table("AstronautDuty")]
    public class AstronautDuty
    {
        // For EF Core materialization.
        private AstronautDuty()
        {
        }

        public Guid Id { get; private set; }

        public Guid PersonId { get; private set; }

        public string Rank { get; private set; } = string.Empty;

        public string DutyTitle { get; private set; } = string.Empty;

        public DateOnly DutyStartDate { get; private set; }

        public DateOnly? DutyEndDate { get; private set; }

        public virtual Person Person { get; private set; } = null!;

        /// <summary>Creates a new open (current) duty with an application-generated UUIDv7.</summary>
        internal static AstronautDuty Start(Guid personId, string rank, string dutyTitle, DateOnly startDate) => new()
        {
            Id = Guid.CreateVersion7(),
            PersonId = personId,
            Rank = rank,
            DutyTitle = dutyTitle,
            DutyStartDate = startDate,
            DutyEndDate = null
        };

        /// <summary>Closing a duty is the only change ever made to an existing duty (rule 5).</summary>
        internal void Close(DateOnly endDate) => DutyEndDate = endDate;
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
