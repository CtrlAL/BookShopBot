using Book.Recognition.V1;
using BookService.V1;

namespace GrpcBookRecognitionService.Features.Mappers
{
    public static class BookMapper
    {
        public static RecognizeBookResponse CreateFailedResponse(string errorMessage)
        {
            return new RecognizeBookResponse
            {
                Recognized = false,
                BookCreatedInCatalog = false,
                Message = errorMessage
            };
        }

        public static RecognizeBookResponse MapToRecognizeBookResponse(
        CreateBookResponse catalogResponse,
        BookInfo recognizedBookInfo = null)
        {
            var response = new RecognizeBookResponse
            {
                Recognized = recognizedBookInfo != null,
                Message = recognizedBookInfo != null
                    ? "Book recognized successfully"
                    : "Book not recognized"
            };

            if (catalogResponse != null && catalogResponse.BookId != 0)
            {
                response.BookCreatedInCatalog = true;
                response.CatalogBookId = catalogResponse.BookId.ToString();

                if (catalogResponse.CreatedBook != null)
                {
                    response.BookInfo = MapToBookInfo(catalogResponse.CreatedBook);
                    response.Message = "Book created in catalog";
                }
                else if (recognizedBookInfo != null)
                {
                    response.BookInfo = recognizedBookInfo;
                    response.Message = "Book recognized and created in catalog";
                }
            }
            else if (recognizedBookInfo != null)
            {
                response.BookCreatedInCatalog = false;
                response.BookInfo = recognizedBookInfo;
                response.Message = "Book recognized but not created in catalog";
            }
            else
            {
                response.BookCreatedInCatalog = false;
                response.Message = "Book not recognized";
            }

            return response;
        }


        public static BookRecognitionResult MapToRecognitionResult(
            CreateBookResponse catalogResponse,
            int imageIndex,
            BookInfo recognizedBookInfo)
        {
            var result = new BookRecognitionResult
            {
                ImageIndex = imageIndex,
                Recognized = recognizedBookInfo != null,
                BookCreatedInCatalog = catalogResponse != null && catalogResponse.BookId != 0,
                Message = "Success"
            };

            if (catalogResponse != null && catalogResponse.BookId != 0)
            {
                result.CatalogBookId = catalogResponse.BookId.ToString();

                if (catalogResponse.CreatedBook != null)
                {
                    result.BookInfo = MapToBookInfo(catalogResponse.CreatedBook);
                }
                else if (recognizedBookInfo != null)
                {
                    result.BookInfo = recognizedBookInfo;
                }

                result.Message = $"Book created with ID: {catalogResponse.BookId}";
            }
            else if (recognizedBookInfo != null)
            {
                result.BookInfo = recognizedBookInfo;
                result.Message = "Book recognized but not created in catalog";
            }
            else
            {
                result.Message = "Book not recognized";
            }

            return result;
        }

        public static BookInfo MapToBookInfo(BookService.V1.Book book)
        {
            var bookInfo = new BookInfo
            {
                Title = book.Title,
                Authors = { book.Author },
                Publisher = book.PublishingHouse,
                PublicationYear = book.Year.Value,
                Isbn = book.Isbn,
                Language = book.Language,
            };

            return bookInfo;
        }

        public static BookService.V1.Book MapFromBookInfo(BookInfo bookInfo)
        {
            var book = new BookService.V1.Book
            {
                Title = bookInfo.Title,
                Author = bookInfo.Authors.Count > 0 ? bookInfo.Authors[0] : "",
                Confidence = 1.0,
                PublishingHouse = bookInfo.Publisher,
                Year = bookInfo.PublicationYear,
                Isbn = bookInfo.Isbn,
                Language = bookInfo.Language,
            };

            return book;
        }

        public static RecognizeBooksResponse MapToRecognizeBooksResponse(
            List<CreateBookResponse> catalogResponses)
        {
            var recognitionResults = new List<BookRecognitionResult>();

            for (int i = 0; i < catalogResponses.Count; i++)
            {
                var catalogResponse = catalogResponses[i];
                var result = MapToBookRecognitionResult(catalogResponse, i);
                recognitionResults.Add(result);
            }

            return new RecognizeBooksResponse
            {
                Results = { recognitionResults },
                TotalImages = recognitionResults.Count,
                SuccessfullyRecognized = recognitionResults.Count(r => r.Recognized),
                BooksCreatedInCatalog = recognitionResults.Count(r => r.BookCreatedInCatalog)
            };
        }

        private static BookRecognitionResult MapToBookRecognitionResult(
            CreateBookResponse catalogResponse,
            int index)
        {
            var result = new BookRecognitionResult
            {
                ImageIndex = index
            };

            if (catalogResponse?.BookId > 0)
            {
                result.Recognized = true;
                result.BookCreatedInCatalog = true;
                result.CatalogBookId = catalogResponse.BookId.ToString();

                if (catalogResponse.CreatedBook != null)
                {
                    result.BookInfo = MapBookToBookInfo(catalogResponse.CreatedBook);
                    result.Message = $"Book created successfully (ID: {catalogResponse.BookId})";
                }
                else
                {
                    result.BookInfo = new BookInfo
                    {
                        Title = "Book recognized",
                        Authors = { "Unknown" }
                    };
                    result.Message = $"Book recognized and created (ID: {catalogResponse.BookId})";
                }
            }
            else if (catalogResponse != null && catalogResponse.BookId == 0)
            {
                result.Recognized = true;
                result.BookCreatedInCatalog = false;
                result.Message = "Book recognized but not created in catalog";

                if (catalogResponse.CreatedBook != null)
                {
                    result.BookInfo = MapBookToBookInfo(catalogResponse.CreatedBook);
                }
            }
            else
            {
                result.Recognized = false;
                result.BookCreatedInCatalog = false;
                result.Message = catalogResponse == null
                    ? "Book not recognized"
                    : $"Error: Invalid response";
            }

            return result;
        }

        private static BookInfo MapBookToBookInfo(BookService.V1.Book book)
        {
            var bookInfo = new BookInfo
            {
                Title = book.Title ?? string.Empty,
                Authors = { book.Author ?? string.Empty }
            };

            if (book.PublishingHouse != null && !string.IsNullOrEmpty(book.PublishingHouse))
            {
                bookInfo.Publisher = book.PublishingHouse;
            }

            if (book.Year != null && book.Year.Value != 0)
            {
                bookInfo.PublicationYear = book.Year.Value;
            }

            if (book.Isbn != null && !string.IsNullOrEmpty(book.Isbn))
            {
                bookInfo.Isbn = book.Isbn;
            }

            if (book.Language != null && !string.IsNullOrEmpty(book.Language))
            {
                bookInfo.Language = book.Language;
            }

            return bookInfo;
        }
    }
}