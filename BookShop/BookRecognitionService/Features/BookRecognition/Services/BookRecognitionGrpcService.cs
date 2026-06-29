using Book.Recognition.V1;
using BookService.V1;
using BookAi.Abstractions;
using Grpc.Core;
using BookRecognitionService.Features.Mappers;

namespace BookRecognitionService.Features.BookRecognition.Services
{
    public class BookRecognitionGrpcService : global::Book.Recognition.V1.BookRecognitionService.BookRecognitionServiceBase
    {
        private readonly IAiBookRecognitionService _deepSeekClient;
        private readonly global::BookService.V1.BookCatalogService.BookCatalogServiceClient _bookCatalogServiceClient;

        public BookRecognitionGrpcService(IAiBookRecognitionService deepSeekClient, global::BookService.V1.BookCatalogService.BookCatalogServiceClient bookCatalogServiceClient)
        {
            _deepSeekClient = deepSeekClient;
            _bookCatalogServiceClient = bookCatalogServiceClient;
        }

        public override async Task<RecognizeBookResponse> RecognizeBook(
            RecognizeBookRequest request,
            ServerCallContext context)
        {
            var result = await _deepSeekClient.AnalyzeBookImageAsync(request.ImageData.ToByteArray());

            if (!result.Success || result.Payload == null)
            {
                return BookMapper.CreateFailedResponse(string.Empty);
            }

            var createBookRequest = new CreateBookRequest
            {
                Book = new BookService.V1.Book
                {
                    Title = result.Payload.Title,
                    Isbn = result.Payload.ISBN,
                    Author = result.Payload.Author,
                    Confidence = result.Payload.Confidence,
                    Language = result.Payload.Language,
                    PublishingHouse = result.Payload.PublishingHouse,
                    Year = result.Payload.Year,
                }
            };

            var createBookResult = await _bookCatalogServiceClient.CreateBookAsync(createBookRequest);

            return BookMapper.MapToRecognizeBookResponse(createBookResult);
        }

        public override async Task<RecognizeBooksResponse> RecognizeBooks(
            RecognizeBooksRequest request,
            ServerCallContext context)
        {
            var responses = await Task.WhenAll(request.Images.Select(async item => await _deepSeekClient.AnalyzeBookImageAsync(item.ImageData.ToByteArray())));

            var createResults = new List<CreateBookResponse>();
            foreach (var response in responses)
            {
                var createBookRequest = new CreateBookRequest
                {
                };

                var createBookResponse = await _bookCatalogServiceClient.CreateBookAsync(createBookRequest);
                createResults.Add(createBookResponse);
            }

            return BookMapper.MapToRecognizeBooksResponse(createResults);
        }
    }
}
