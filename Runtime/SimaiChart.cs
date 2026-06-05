using MajSimai.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
namespace MajSimai
{
    public class SimaiChart
    {
        public string Level { get; }
        public string Designer { get; }
        public bool IsEmpty
        {
            get
            {
                return _isEmpty;
            }
        }
        public string Fumen
        {
            get
            {
                return _fumen;
            }
        }
        public ReadOnlySpan<SimaiTimingPoint> NoteTimings
        {
            get
            {
                return _noteTimings;
            }
        }
        public ReadOnlySpan<SimaiTimingPoint> CommaTimings
        {
            get
            {
                return _commaTimings;
            }
        }
        public readonly static SimaiChart Empty = new SimaiChart();


        readonly bool _isEmpty;
        readonly string _fumen;
        readonly SimaiTimingPoint[] _noteTimings;
        readonly SimaiTimingPoint[] _commaTimings;
        public SimaiChart(string level, string designer, string fumen,
                          ReadOnlySpan<SimaiTimingPoint> noteTimings): this(level, designer, fumen, noteTimings, ReadOnlySpan<SimaiTimingPoint>.Empty)
        { 
        }
        public SimaiChart(string level, string designer, string fumen,
                          ReadOnlySpan<SimaiTimingPoint> noteTimings,
                          ReadOnlySpan<SimaiTimingPoint> commaTimings)
        {
            if(level is null)
            {
                throw new ArgumentNullException(nameof(level));
            }
            if (designer is null)
            {
                throw new ArgumentNullException(nameof(designer));
            }
            Level = level;
            Designer = designer;
            _noteTimings = Array.Empty<SimaiTimingPoint>();
            _commaTimings = Array.Empty<SimaiTimingPoint>();
            var i = 0;
            var buffer = ArrayPool<SimaiTimingPoint>.Shared.Rent(16);
            
            try
            {
                foreach (var n in noteTimings)
                {
                    if (n is null)
                    {
                        throw new ArgumentException(nameof(noteTimings));
                    }
                    BufferHelper.EnsureBufferLength(i + 1, ref buffer);
                    buffer[i++] = n;
                }
                if(i != 0)
                {
                    if(string.IsNullOrEmpty(fumen))
                    {
                        throw new ArgumentException(nameof(fumen));
                    }
                    _noteTimings = new SimaiTimingPoint[i];
                    buffer.AsSpan(0, i).CopyTo(_noteTimings);
                }
                i = 0;

                foreach (var n in commaTimings)
                {
                    if(n is null)
                    {
                        throw new ArgumentException(nameof(commaTimings));
                    }
                    BufferHelper.EnsureBufferLength(i + 1, ref buffer);
                    buffer[i++] = n;
                }
                if (i != 0)
                {
                    _commaTimings = new SimaiTimingPoint[i];
                    buffer.AsSpan(0, i).CopyTo(_commaTimings);
                }
            }
            finally
            {
                ArrayPool<SimaiTimingPoint>.Shared.Return(buffer);
            }
            _fumen = fumen;
            _isEmpty = _noteTimings.Length == 0;
        }
        SimaiChart()
        {
            Level = string.Empty;
            Designer = string.Empty;
            _fumen = string.Empty;
            _noteTimings = Array.Empty<SimaiTimingPoint>();
            _commaTimings = Array.Empty<SimaiTimingPoint>();
            _isEmpty = true;
        }

#if NET7_0_OR_GREATER
        internal unsafe MajSimai.Unmanaged.UnmanagedSimaiChart ToUnmanaged()
        {
            var levelPtr = (char*)null;
            var designerPtr = (char*)null;
            var fumenPtr = (char*)null;
            var noteTimingArray = (MajSimai.Unmanaged.UnmanagedSimaiTimingPoint*)null;
            var commaTimingArray = (MajSimai.Unmanaged.UnmanagedSimaiTimingPoint*)null;

            if (!string.IsNullOrEmpty(Level))
            {
                levelPtr = (char*)Marshal.StringToHGlobalAnsi(Level);
            }
            if (!string.IsNullOrEmpty(Designer))
            {
                designerPtr = (char*)Marshal.StringToHGlobalAnsi(Designer);
            }
            if (!string.IsNullOrEmpty(Fumen))
            {
                fumenPtr = (char*)Marshal.StringToHGlobalAnsi(Fumen);
            }
            if(!NoteTimings.IsEmpty)
            {
                noteTimingArray = (MajSimai.Unmanaged.UnmanagedSimaiTimingPoint*)Marshal.AllocHGlobal(sizeof(MajSimai.Unmanaged.UnmanagedSimaiTimingPoint) * NoteTimings.Length);
                for (var i = 0; i < NoteTimings.Length; i++)
                {
                    *(noteTimingArray + i) = NoteTimings[i].ToUnmanaged();
                }
            }
            if (!CommaTimings.IsEmpty)
            {
                commaTimingArray = (MajSimai.Unmanaged.UnmanagedSimaiTimingPoint*)Marshal.AllocHGlobal(sizeof(MajSimai.Unmanaged.UnmanagedSimaiTimingPoint) * CommaTimings.Length);
                for (var i = 0; i < CommaTimings.Length; i++)
                {
                    *(commaTimingArray + i) = CommaTimings[i].ToUnmanaged();
                }
            }
            return new()
            {
                level = levelPtr,
                levelLen = Level.Length,

                designer = designerPtr,
                designerLen = Designer.Length,

                isEmpty = IsEmpty,

                fumen = fumenPtr,
                fumenLen = Fumen.Length,

                noteTimings = noteTimingArray,
                noteTimingsLen = NoteTimings.Length,

                commaTimings = commaTimingArray,
                commaTimingsLen = CommaTimings.Length
            };
        }
#endif
    }
}
