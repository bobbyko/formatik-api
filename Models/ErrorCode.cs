namespace Octagon.Formatik.API
{
    public enum ErrorCode
    {
        NoError = 0,
        InternalError = 1,
        EvaluationError = 2,
        ProcessingError = 3,
        UserNotFound = 4,
        MissingParameters = 5,
        InvalidInputCacheId = 6,
        InputCacheNotFound = 7,
        InvalidFormatId = 8
    }
}