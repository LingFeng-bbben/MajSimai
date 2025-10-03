using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace MajSimai
{
    public class SimaiTimingPoint
    {
        public double Timing { get; set; } = 0;
        public float Bpm { get; set; } = -1;
        public float HSpeed { get; set; } = 1f;
        public float SVeloc { get; set; } = 1f;
        public string RawContent { get; } = string.Empty;
        public int RawTextPositionX { get; }
        public int RawTextPositionY { get; }
        public int RawTextPosition { get; }
        public SimaiNote[] Notes { get; set; } = Array.Empty<SimaiNote>();
        public bool IsEmpty => Notes.Length == 0;

        public SimaiTimingPoint(double timing, SimaiNote[]? notes, ReadOnlySpan<char> rawContent, int textPosX = 0, int textPosY = 0, float bpm = 0f,
            float hspeed = 1f, float sveloc = 1f, int rawTextPosition = 0)
        {
            Timing = timing;
            RawTextPositionX = textPosX;
            RawTextPositionY = textPosY;
            RawTextPosition = rawTextPosition;
            if(!rawContent.IsEmpty)
            {
                Span<char> rCSpan = stackalloc char[rawContent.Length];
                rawContent.Replace(rCSpan, '\n', ' ');
                var i2 = 0;
                for (var i = 0; i < rCSpan.Length; i++)
                {
                    var current = rCSpan[i];
                    if (char.IsWhiteSpace(current))
                    {
                        continue;
                    }
                    else
                    {
                        rCSpan[i2++] = current;
                    }
                }
                var newRaw = rCSpan.Slice(0, i2);
                if(newRaw != rawContent)
                {
                    RawContent = new string(rCSpan.Slice(0, i2));
                }
                else
                {
                    RawContent = rawContent.ToString();
                }
            }
            else
            {
                RawContent = string.Empty;
            }
            Bpm = bpm;
            HSpeed = hspeed;
            SVeloc = sveloc;
            if (notes != null)
            {
                Notes = notes;
            }
        }
#if NET7_0_OR_GREATER
        internal unsafe MajSimai.Unmanaged.UnmanagedSimaiTimingPoint ToUnmanaged()
        {
            var rawContentPtr = (char*)null;
            var noteArray = (MajSimai.Unmanaged.UnmanagedSimaiNote*)null;

            if (!string.IsNullOrEmpty(RawContent))
            {
                rawContentPtr = (char*)Marshal.StringToHGlobalAnsi(RawContent);
            }
            if (Notes.Length != 0)
            {
                noteArray = (MajSimai.Unmanaged.UnmanagedSimaiNote*)Marshal.AllocHGlobal(sizeof(MajSimai.Unmanaged.UnmanagedSimaiNote) * Notes.Length);
                for (var i = 0; i < Notes.Length; i++)
                {
                    *(noteArray + i) = Notes[i].ToUnmanaged();
                }
            }
            return new()
            {
                timing = Timing,
                bpm = Bpm,
                hSpeed = HSpeed,
                sVeloc = SVeloc,
                rawTextPositionX = RawTextPositionX,
                rawTextPositionY = RawTextPositionY,
                rawTextPosition = RawTextPosition,
                rawContent = rawContentPtr,
                rawContentLen = RawContent.Length,
                notes = noteArray,
                notesLen = Notes.Length
            };
        }
#endif
    }
}
