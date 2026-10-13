using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace StargateAPI.Business.Data
{
    /// <summary>
    /// Current-state summary for a person with at least one assignment, maintained alongside the
    /// authoritative duty history. CareerEndDate is set only while currently retired.
    /// </summary>
    [Table("AstronautDetail")]
    public class AstronautDetail
    {
        public const int MaxRankLength = 100;
        public const int MaxDutyTitleLength = 200;

        // For EF Core materialization.
        private AstronautDetail()
        {
        }

        public Guid Id { get; private set; }

        public Guid PersonId { get; private set; }

        public string CurrentRank { get; private set; } = string.Empty;

        public string CurrentDutyTitle { get; private set; } = string.Empty;

        public DateOnly CurrentDutyStartDate { get; private set; }

        public DateOnly CareerStartDate { get; private set; }

        public DateOnly? CareerEndDate { get; private set; }

        public virtual Person Person { get; private set; } = null!;

        /// <summary>Creates the summary for a person's first (non-retired) assignment.</summary>
        internal static AstronautDetail StartCareer(Guid personId, string rank, string dutyTitle, DateOnly startDate) => new()
        {
            Id = Guid.CreateVersion7(),
            PersonId = personId,
            CurrentRank = rank,
            CurrentDutyTitle = dutyTitle,
            CurrentDutyStartDate = startDate,
            CareerStartDate = startDate,
            CareerEndDate = null
        };

        /// <summary>Replaces the current assignment. CareerStartDate is the first assignment and never changes.</summary>
        internal void ApplyCurrentDuty(string rank, string dutyTitle, DateOnly startDate, DateOnly? careerEndDate)
        {
            CurrentRank = rank;
            CurrentDutyTitle = dutyTitle;
            CurrentDutyStartDate = startDate;
            CareerEndDate = careerEndDate;
        }
    }

    public class AstronautDetailConfiguration : IEntityTypeConfiguration<AstronautDetail>
    {
        public void Configure(EntityTypeBuilder<AstronautDetail> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.CurrentRank).IsRequired().HasMaxLength(AstronautDetail.MaxRankLength);
            builder.Property(x => x.CurrentDutyTitle).IsRequired().HasMaxLength(AstronautDetail.MaxDutyTitleLength);

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_AstronautDetail_CurrentRank_NotBlank", "length(trim(\"CurrentRank\")) > 0");
                t.HasCheckConstraint("CK_AstronautDetail_CurrentDutyTitle_NotBlank", "length(trim(\"CurrentDutyTitle\")) > 0");
                t.HasCheckConstraint("CK_AstronautDetail_CareerEnd_AfterStart",
                    "\"CareerEndDate\" IS NULL OR \"CareerEndDate\" >= \"CareerStartDate\"");
            });
        }
    }
}
