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

        public Guid Id { get; set; } = Guid.CreateVersion7();

        public Guid PersonId { get; set; }

        public string CurrentRank { get; set; } = string.Empty;

        public string CurrentDutyTitle { get; set; } = string.Empty;

        public DateOnly CurrentDutyStartDate { get; set; }

        public DateOnly CareerStartDate { get; set; }

        public DateOnly? CareerEndDate { get; set; }

        public virtual Person Person { get; set; } = null!;
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
