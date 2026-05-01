using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GauntletCI.Watchtower.Data;

public class WatchtowerDbContextFactory : IDesignTimeDbContextFactory<WatchtowerDbContext>
{
    public WatchtowerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WatchtowerDbContext>();
        
        var dbPath = "watchtower.db";
        if (args.Length > 0)
        {
            dbPath = args[0];
        }
        
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
        
        return new WatchtowerDbContext(optionsBuilder.Options);
    }
}
