using Microsoft.EntityFrameworkCore;
using BookCatalogService.Features.BooksManagement.Domain;

namespace BookCatalogService.Shared
{
    public class DefaultDbContext : DbContext
    {
        public DbSet<Book> Books { get; set; }

        protected DefaultDbContext()
        {
        }

        public DefaultDbContext(DbContextOptions<DefaultDbContext> options)
        : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
        }
    }
}
