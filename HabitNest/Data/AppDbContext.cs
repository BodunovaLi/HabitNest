using System;
using System.Collections.Generic;
using System.Text;

using HabitNest.Models;
using Microsoft.EntityFrameworkCore;

namespace HabitNest.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }

        public DbSet<Habit> Habits { get; set; }

        public DbSet<HabitCompletion> HabitCompletions { get; set; }

        protected override void OnConfiguring(
            DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(
                "Data Source=habitnest.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HabitCompletion>()
                .HasOne<Habit>()
                .WithMany(h => h.Completions)
                .HasForeignKey(c => c.HabitId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<HabitCompletion>()
                .HasIndex(c => new { c.HabitId, c.CompletionDate })
                .IsUnique();
        }


    }
}
