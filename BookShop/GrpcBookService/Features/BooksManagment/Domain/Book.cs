namespace GrpcBookService.Features.BooksManagment.Domain
{
    public class Book
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? PublishingHouse { get; set; }
        public int? Year { get; set; }
        public string? ISBN { get; set; }
        public string? Language { get; set; }
        public double Confidence { get; set; }
    }
}
