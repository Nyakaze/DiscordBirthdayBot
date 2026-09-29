using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SadBot.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SadBotDbContext>
{
    public SadBotDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SadBotDbContext>()
            .UseNpgsql("Host=localhost;Database=sadbot;Username=sadbot;Password=sadbot")
            .Options);
}
