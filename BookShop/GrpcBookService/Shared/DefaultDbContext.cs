using Microsoft.EntityFrameworkCore;
using GrpcBookService.Features.BooksManagment.Domain;

namespace GrpcBookService.Shared
{
    public class DefaultDbContext : DbContext
    {
        public DbSet<Book> Users { get; set; }

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
