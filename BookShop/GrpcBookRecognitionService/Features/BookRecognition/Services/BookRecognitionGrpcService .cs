using Grpc.Core;
using Book.Recognition.V1;

namespace GrpcBookRecognitionService.Features.BookRecognition.Services
{
    public class BookRecognitionGrpcService : BookRecognitionService.BookRecognitionServiceBase
    {
        public override async Task<RecognizeBookResponse> RecognizeBook(
            RecognizeBookRequest request,
            ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        public override async Task<RecognizeBooksResponse> RecognizeBooks(
            RecognizeBooksRequest request,
            ServerCallContext context)
        {
            throw new NotImplementedException();
        }
    }
}
