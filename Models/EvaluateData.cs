using System;
using System.ComponentModel;
using Newtonsoft.Json;

namespace Octagon.Formatik.API
{
    public sealed class EvaluateData
    {
        public string Name { get; set; }
        public string Input { get; set; }
        public string Example { get; set; }
        public string InputCacheId { get; set; }
        [DefaultValue(false)]
        public Boolean Temporary { get; set; }
    }
}