using GrpcBookService.Features.BooksManagment.Domain;
using Microsoft.EntityFrameworkCore;

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
