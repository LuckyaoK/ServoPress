using Microsoft.EntityFrameworkCore;
using ServoPress.Database.Entities;
using System.IO;


namespace ServoPress.Database
{
    public class AppDbContext : DbContext
    {
        // 数据集，对应数据库中的表
        public DbSet<ProductionRecord> ProductionRecords { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
          
            string dbFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DB");
            if (!Directory.Exists(dbFolder))
            {
                Directory.CreateDirectory(dbFolder);
            }

            string dbPath = Path.Combine(dbFolder, "ServoPress.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}
