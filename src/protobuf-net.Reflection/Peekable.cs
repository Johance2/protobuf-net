using System;
using System.Collections.Generic;

namespace ProtoBuf.Reflection
{
    internal sealed class Peekable<T> : IDisposable
    {
        public override string ToString()
        {
            return Peek(out T val) ? (val?.ToString() ?? "(null)") : "(EOF)";
        }
        private readonly IEnumerator<T> _iter;
        private T _peek, _prev;
        private bool _havePeek, _eof;
        private readonly List<string> _pendingLeadingComments = new List<string>();
        private readonly List<string> _pendingTrailingComments = new List<string>();
        public List<Error> Errors { get; }
        public Peekable(IEnumerable<T> sequence, List<Error> errors)
        {
            _iter = sequence.GetEnumerator();
            Errors = errors;
        }
        public T Previous => _prev;
        public bool Consume()
        {
            bool haveData = _havePeek || Peek(out T _);
            _prev = _peek;
            _havePeek = false;
            return haveData;
        }
        public bool Peek(out T next)
        {
            while (!_havePeek)
            {
                if (!_iter.MoveNext())
                {
                    _eof = true;
                    break;
                }

                var candidate = _iter.Current;
                if (IsTrivia(candidate, out var comment))
                {
                    if (comment != null)
                    {
                        _pendingLeadingComments.Add(comment);
                    }
                    continue;
                }

                _prev = _peek;
                _peek = candidate;
                _havePeek = true;
            }
            if (_eof)
            {
                next = default;
                return false;
            }
            next = _peek;
            return true;
        }
        public string[] TakeComments()
        {
            if (_pendingLeadingComments.Count == 0) return null;
            var result = _pendingLeadingComments.ToArray();
            _pendingLeadingComments.Clear();
            return result;
        }
        public string[] TakeTrailingComments()
        {
            if (_pendingTrailingComments.Count == 0) return null;
            var result = _pendingTrailingComments.ToArray();
            _pendingTrailingComments.Clear();
            return result;
        }
        private bool IsTrivia(T token, out string comment)
        {
            comment = null;
            if (token is Token t)
            {
                if (t.Type == TokenType.Comment)
                {
                    if (_prev is Token prev && prev.LineNumber == t.LineNumber)
                    {
                        _pendingTrailingComments.Add(t.Value);
                        return true;
                    }
                    comment = t.Value;
                    return true;
                }
                if (t.Type == TokenType.Whitespace)
                {
                    return true;
                }
            }
            return false;
        }
        public void Dispose() => _iter?.Dispose();
    }
}
