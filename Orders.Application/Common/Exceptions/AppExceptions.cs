namespace Orders.Application.Common.Exceptions
{
    public class NotFoundException : Exception
    {
        public NotFoundException(string message = "Data not found") : base(message) { }
    }

    public class BadRequestException : Exception
    {
        public BadRequestException(string message) : base(message) { }
    }

    public class ForbiddenException : Exception
    {
        public ForbiddenException(string message = "Forbidden") : base(message) { }
    }

    public class ConflictException : Exception
    {
        public ConflictException(string message = "A concurrency conflict occurred.") : base(message) { }
    }

    public class UnprocessableEntityException : Exception
    {
        public UnprocessableEntityException(string message = "The request could not be processed.") : base(message) { }
    }

    public class ValidationException : Exception
    {
        public IDictionary<string, string[]> Errors { get; }

        public ValidationException(IDictionary<string, string[]> errors) 
            : base("One or more validation failures have occurred.")
        {
            Errors = errors;
        }
    }
}
