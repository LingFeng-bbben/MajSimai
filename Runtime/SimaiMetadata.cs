using MajSimai;
using MajSimai.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MajSimai
{
    public readonly struct SimaiMetadata
    {
        public string Title { get; }
        public string Artist { get; }
        public string FinalDesigner { get; }
        public float Offset { get; }
        public ReadOnlySpan<string> Designers
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _designers;
            }
        }
        public ReadOnlySpan<string> Levels
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _levels;
            }
        }
        public ReadOnlySpan<string> Fumens
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _fumens;
            }
        }
        public ReadOnlySpan<SimaiCommand> Commands
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _commands;
            }
        }
        public string Hash { get; }

        readonly string[] _designers;
        readonly string[] _levels;
        readonly string[] _fumens;
        readonly SimaiCommand[] _commands;

        public SimaiMetadata(string title, string artist, float offset, 
                             IEnumerable<string>? designers,
                             IEnumerable<string>? levels,
                             IEnumerable<string>? fumens,
                             IEnumerable<SimaiCommand>? commands,
                             string hash,
                             string finalDesigner = "")
        {
            if(hash is null)
            {
                throw new ArgumentNullException(nameof(hash));
            }
            if(hash.Length == 0)
            {
                throw new ArgumentException(nameof(hash));
            }
            Title = title;
            Artist = artist;
            FinalDesigner = finalDesigner;
            Offset = offset;
            _designers = new string[7];
            _levels = new string[7];
            _fumens = new string[7];
            _commands = Array.Empty<SimaiCommand>();
            Hash = hash;

            var i = 0;
            foreach (var d in designers ?? Array.Empty<string>())
            {
                _designers[i++] = d;
                if(i == 7)
                {
                    break;
                }
            }
            i = 0;
            foreach (var l in levels ?? Array.Empty<string>())
            {
                _levels[i++] = l;
                if (i == 7)
                {
                    break;
                }
            }
            i = 0;
            foreach (var f in fumens ?? Array.Empty<string>())
            {
                _fumens[i++] = f;
                if (i == 7)
                {
                    break;
                }
            }
            i = 0;

            var buffer = ArrayPool<SimaiCommand>.Shared.Rent(16);
            try
            {
                foreach (var c in commands ?? Array.Empty<SimaiCommand>())
                {
                    BufferHelper.EnsureBufferLength(i + 1, ref buffer);
                    buffer[i++] = c;
                }
                if (i != 0)
                {
                    _commands = new SimaiCommand[i];
                    buffer.AsSpan(0, i).CopyTo(_commands);
                }
            }
            finally
            {
                ArrayPool<SimaiCommand>.Shared.Return(buffer);
            }
        }
        public SimaiMetadata(string title, string artist, float offset, 
                             ReadOnlySpan<string> designers,
                             ReadOnlySpan<string> levels,
                             ReadOnlySpan<string> fumens,
                             ReadOnlySpan<SimaiCommand> commands,
                             string hash,
                             string finalDesigner = "")
        {
            if (hash is null)
            {
                throw new ArgumentNullException(nameof(hash));
            }
            if (hash.Length == 0)
            {
                throw new ArgumentException(nameof(hash));
            }
            Title = title;
            Artist = artist;
            FinalDesigner = finalDesigner;
            Offset = offset;
            _designers = new string[7];
            _levels = new string[7];
            _fumens = new string[7];
            _commands = Array.Empty<SimaiCommand>();
            Hash = hash;

            if(designers.Length > 7)
            {
                designers = designers.Slice(0, 7);
            }
            if (levels.Length > 7)
            {
                levels = levels.Slice(0, 7);
            }
            if (fumens.Length > 7)
            {
                fumens = fumens.Slice(0, 7);
            }
            designers.CopyTo(_designers);
            levels.CopyTo(_levels);
            fumens.CopyTo(_fumens);

            if (commands.Length != 0)
            {
                _commands = new SimaiCommand[commands.Length];
                commands.CopyTo(_commands);
            }
        }
    }
}
