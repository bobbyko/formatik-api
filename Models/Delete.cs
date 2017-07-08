namespace Octagon.Formatik.API
{
    public class Delete : APIResponse
    {
        public string FormatId { get; set; }
        public bool Deleted { get; set; }

        public new static Delete GetError(ErrorCode code, string error)
        {
            return new Delete() {
                Status = "ERROR",
                Error = error,
                ErrorCode = code.ToString()
            };
        }        
    }}