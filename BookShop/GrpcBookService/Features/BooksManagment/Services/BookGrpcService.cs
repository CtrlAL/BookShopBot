using BookService.V1;
using Grpc.Core;

namespace GrpcBookService.Features.BooksManagment.Services
{
    public class BookGrpcService : BookCatalogService.BookCatalogServiceBase
    {
        public override Task<CreateBookResponse> CreateBook(
            CreateBookRequest request,
            ServerCallContext context)
        {
            var book = request.Book;
            var title = book.Title;
            var year = book.Year;

            return Task.FromResult(new CreateBookResponse
            {
                BookId = 1,
                CreatedBook = book
            });
        }
    }
}
