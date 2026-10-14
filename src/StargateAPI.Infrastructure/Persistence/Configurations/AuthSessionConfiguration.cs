using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StargateAPI.Domain;
using StargateAPI.Infrastructure.Identity;

namespace StargateAPI.Infrastructure.Persistence.Configurations
{
    public class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
    {
        public void Configure(EntityTypeBuilder<AuthSession> builder)
        {
            builder.ToTable("AuthSession");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();

            // Same UTC storage strategy as ProcessLog: INTEGER Unix milliseconds.
            builder.Property(x => x.CreatedAtUtc).HasConversion(
                v => v.ToUnixTimeMilliseconds(), v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            builder.Property(x => x.ExpiresAtUtc).HasConversion(
                v => v.ToUnixTimeMilliseconds(), v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            builder.Property(x => x.RevokedAtUtc).HasConversion(
                v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : (long?)null,
                v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null);

            builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(x => x.UserId);
            builder.HasIndex(x => x.ExpiresAtUtc);
        }
    }
}
