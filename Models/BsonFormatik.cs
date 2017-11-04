using Octagon.Formatik;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System;

namespace Octagon.Formatik.API
{
    public sealed class BsonFormatik : IComparable, IComparable<BsonFormatik>, IEquatable<BsonFormatik>
    {
        private Formatik formatik;

        public string Header
        {
            get
            {
                return formatik.Header;
            }
            set
            {
                formatik.Header = value;
            }
        }

        public string Footer
        {
            get
            {
                return formatik.Footer;
            }
            set
            {
                formatik.Footer = value;
            }
        }

        public IEnumerable<string> Separators
        {
            get
            {
                return formatik.Separators;
            }
            set
            {
                formatik.Separators = value;
            }
        }

        public int InputHash
        {
            get
            {
                return formatik.InputHash;
            }
            set
            {
                formatik.InputHash = value;
            }
        }

        public string Example
        {
            get
            {
                return formatik.Example;
            }
            set
            {
                formatik.Example = value;
            }
        }

        public int ExampleHash
        {
            get
            {
                return formatik.ExampleHash;
            }
            set
            {
                formatik.ExampleHash = value;
            }
        }        

        public int Hash
        {
            get
            {
                return formatik.Hash;
            }
            set
            {
                formatik.Hash = value;
            }
        }

        public IEnumerable<BsonToken> Tokens
        {
            get
            {
                return formatik.Tokens.Select(token => new BsonToken(token));
            }
            set
            {
                formatik.Tokens = value.Select(t => t.Token).ToArray();
            }
        }

        public string Version
        {
            get
            {
                return formatik.Version;
            }
            set
            {
                formatik.Version = value;
            }
        }

        public String InputFormat
        {
            get
            {
                return formatik.InputFormat.ToString();
            }
            set
            {
                formatik.InputFormat = (InputFormat)Enum.Parse(typeof(InputFormat), value);
            }
        }

        [BsonIgnore]
        [IgnoreDataMember]
        public Formatik Formatik { get { return formatik; }}

        public BsonFormatik() { 
            formatik = new Formatik();
        }

        public BsonFormatik(string input, string example, int maxInputRecords)
        {
            formatik = new Formatik(input, example) { MaxInputRecords = maxInputRecords };
        }

        #region IComparable
        public int CompareTo(object obj)
        {
            if (obj.GetType() != typeof(BsonFormatik))
                return -1;
                //throw new ArgumentException("obj must be of type BsonFormatik");

            return CompareTo((BsonFormatik)obj);
        }
        
        public int CompareTo(BsonFormatik other)
        {
            return this.formatik.CompareTo(other.formatik);
        }

        bool IEquatable<BsonFormatik>.Equals(BsonFormatik other)
        {
            return CompareTo(other) == 0;
        }
        #endregion

        public static Boolean operator ==(BsonFormatik a, BsonFormatik b)
        {
            return a.CompareTo(b) == 0;
        }
        
        public static Boolean operator !=(BsonFormatik a, BsonFormatik b)
        {
            return a.CompareTo(b) != 0;
        }

        public override int GetHashCode()
        {
            return formatik.GetHashCode();
        }

        public override Boolean Equals(Object obj)
        {
            return CompareTo(obj) == 0;
        }
    }
}