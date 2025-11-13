using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RoadSideRescue.Models;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text.Json;

namespace RoadSideRescue.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Agent> Agents { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<Message> Messages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var photosComparer = new ValueComparer<List<string>>(
            (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
            c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c == null ? new List<string>() : c.ToList()
            );


            modelBuilder.Entity<Request>()
            .Property(r => r.Photos)
            .HasConversion(
            v => JsonSerializer.Serialize(v ?? new List<string>(), (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
            )
              .Metadata.SetValueComparer(photosComparer);
        }
    }
}