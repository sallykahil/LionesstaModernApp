 
using LionessstaAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata;

namespace LionessstaAPI.Data
{
    public class AppDbContext : DbContext
    { 
            public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
            {
            }

            public DbSet<ProductImage> ProductImages{ get; set; }
        }
    } 
