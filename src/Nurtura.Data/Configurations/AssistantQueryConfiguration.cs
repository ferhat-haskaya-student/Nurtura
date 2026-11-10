using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class AssistantQueryConfiguration : IEntityTypeConfiguration<AssistantQuery>
{
    public void Configure(EntityTypeBuilder<AssistantQuery> builder)
    {
        builder.Property(q => q.SuggestedTopic)
            .HasConversion<string>()
            .HasMaxLength(30);
    }
}
