using System;
using System.Runtime.Serialization;

namespace  Octagon.Formatik.API
{
    public class InputUpload: APIResponse
    {
        public string InputCacheId { get; set; }

        public string Input { get; set; }

        public Boolean Truncated { get; set; }

        public string InputFormat { get; set; }

        public int Size { get; set; }

        public int Records { get; set; }

        

        public new static InputUpload GetError(ErrorCode code, string error)
        {
            return new InputUpload() {
                Status = "ERROR",
                Error = error,
                ErrorCode = code.ToString()
            };
        }
    }
}