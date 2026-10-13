using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StargateAPI.Domain;

namespace StargateAPI.Infrastructure.Persistence.Configurations
{
    public class AstronautDetailConfiguration : IEntityTypeConfiguration<AstronautDetail>
    {
        public void Configure(EntityTypeBuilder<AstronautDetail> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.CurrentRank).IsRequired().HasMaxLength(AstronautDetail.MaxRankLength);
            builder.Property(x => x.CurrentDutyTitle).IsRequired().HasMaxLength(AstronautDetail.MaxDutyTitleLength);

            builder.ToTable("AstronautDetail", t =>
            {
                t.HasCheckConstraint("CK_AstronautDetail_CurrentRank_NotBlank", "length(trim(\"CurrentRank\")) > 0");
                t.HasCheckConstraint("CK_AstronautDetail_CurrentDutyTitle_NotBlank", "length(trim(\"CurrentDutyTitle\")) > 0");
                t.HasCheckConstraint("CK_AstronautDetail_CareerEnd_AfterStart",
                    "\"CareerEndDate\" IS NULL OR \"CareerEndDate\" >= \"CareerStartDate\"");
            });
        }
    }
}
