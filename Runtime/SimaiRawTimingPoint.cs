using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#nullable enable
namespace MajSimai
{
    internal readonly struct SimaiRawTimingPoint
    {
        public double Timing { get; }
        public float Bpm { get; }
        public float HSpeed { get; }
        public float SVeloc { get; }
        public string RawContent { get; }
        public int RawTextPositionX { get; }
        public int RawTextPositionY { get; }

        public SimaiRawTimingPoint(double timing, ReadOnlySpan<char> rawContent, int textPosX = 0, int textPosY = 0, float bpm = 0f,
            float hspeed = 1f, float sveloc = 1f)
        {
            Timing = timing;
            RawTextPositionX = textPosX;
            RawTextPositionY = textPosY;
            if (!rawContent.IsEmpty)
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
                if (newRaw != rawContent)
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
        }
        public SimaiTimingPoint Parse()
        {
            var notes = SimaiNoteParser.GetNotes(Timing, Bpm, RawContent);

            return new SimaiTimingPoint(Timing, notes, RawContent, RawTextPositionX, RawTextPositionY, Bpm, HSpeed, SVeloc);
        }
        public Task<SimaiTimingPoint> ParseAsync()
        {
            return Task.Run(Parse);
        }
    }
}
