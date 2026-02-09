using Book.Recognition.V1;
using BookService.V1;
using DeepSeek.Implementations;
using Grpc.Core;
using GrpcBookRecognitionService.Features.Mappers;

namespace GrpcBookRecognitionService.Features.BookRecognition.Services
{
    public class BookRecognitionGrpcService : BookRecognitionService.BookRecognitionServiceBase
    {
        private readonly DeepSeekClient _deepSeekClient;
        private readonly BookCatalogService.BookCatalogServiceClient _bookCatalogServiceClient;

        public BookRecognitionGrpcService(DeepSeekClient deepSeekClient, BookCatalogService.BookCatalogServiceClient bookCatalogServiceClient)
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
                    Isbn = result.Payload.Title,
                    Author = result.Payload.Title,
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
