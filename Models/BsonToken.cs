using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson.Serialization.Attributes;

namespace Octagon.Formatik.API
{
    public sealed class BsonToken
    {
        internal Token Token;

        public string InputSelector
        {
            get
            {
                return Token.InputSelector;
            }
            set
            {
                Token.InputSelector = value;
            }
        }

        public int InputIndex
        {
            get
            {
                return Token.InputIndex;
            }
            set
            {
                Token.InputIndex = value;
            }
        }

        public string OutputSelector
        {
            get
            {
                return Token.OutputSelector;
            }
            set
            {
                Token.OutputSelector = value;
            }
        }

        public string Prefix
        {
            get
            {
                return Token.Prefix;
            }
            set
            {
                Token.Prefix = value;
            }
        }

        public string Suffix
        {
            get
            {
                return Token.Suffix;
            }
            set
            {
                Token.Suffix = value;
            }
        }

        public BsonToken()
        {
            Token = new Token();
        }

        public BsonToken(Token token)
        {
            Token = token;
        }

        public BsonToken(IInputRecord sampleRecord, string sampleValue, string inputSelector, int inputIndex)
        {
            Token = new Token(sampleRecord, sampleValue, inputSelector, inputIndex);
        }

        public BsonToken(IEnumerable<TokenValue> sampleValues, string inputSelector, int inputIndex)
        {
            Token = new Token(sampleValues, inputSelector, inputIndex);
        }
    }
}