using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
namespace MajSimai
{
    public class SimaiFile
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string FinalDesigner { get; set; }
        public float Offset { get; set; }
        public SimaiChart[] Charts
        {
            get
            {
                return _charts;
            }
        }
        public string Hash { get; set; } = string.Empty;
        public IList<SimaiCommand> Commands
        {
            get
            {
                return _commands;
            }
        }

        readonly SimaiChart[] _charts = new SimaiChart[7];
        readonly List<SimaiCommand> _commands = new List<SimaiCommand>();

        public SimaiFile(string title, 
                         string artist, 
                         float offset,
                         string hash,
                         IEnumerable<SimaiChart>? levels, 
                         IEnumerable<SimaiCommand>? commands,
                         string finalDesigner = "")
        {
            Title = title ?? string.Empty;
            Artist = artist ?? string.Empty;
            FinalDesigner = finalDesigner ?? string.Empty;
            Offset = offset;
            Hash = hash ?? string.Empty;

            var i = 0;
            Array.Fill(_charts, SimaiChart.Empty);
            foreach (var c in levels ?? Array.Empty<SimaiChart>())
            {
                _charts[i++] = c;
                if(i == 7)
                {
                    break;
                }
            }
            i = 0;
            _commands.AddRange(commands ?? Array.Empty<SimaiCommand>());
        }
        public static SimaiFile Empty(string title, string artist)
        {
            var emptyCharts = new SimaiChart[7]
            {
                SimaiChart.Empty,
                SimaiChart.Empty,
                SimaiChart.Empty,
                SimaiChart.Empty,
                SimaiChart.Empty,
                SimaiChart.Empty,
                SimaiChart.Empty,
            };
            var emptyFumens = new string[7]
            {
                string.Empty, 
                string.Empty,
                string.Empty, 
                string.Empty,
                string.Empty, 
                string.Empty, 
                string.Empty
            };

            return new SimaiFile(title, artist, 0, string.Empty, emptyCharts, Array.Empty<SimaiCommand>());
        }
#if NET7_0_OR_GREATER
        internal unsafe MajSimai.Unmanaged.UnmanagedSimaiFile ToUnmanaged()
        {
            var titlePtr = (char*)null;
            var artistPtr = (char*)null;
            var finalDesignerPtr = (char*)null;
            var chartArray = (MajSimai.Unmanaged.UnmanagedSimaiChart*)Marshal.AllocHGlobal(sizeof(MajSimai.Unmanaged.UnmanagedSimaiChart) * 7);
            var commandArray = (MajSimai.Unmanaged.UnmanagedSimaiCommand*)null;

            if (!string.IsNullOrEmpty(Title))
            {
                titlePtr = (char*)Marshal.StringToHGlobalAnsi(Title);
            }
            if (!string.IsNullOrEmpty(Artist))
            {
                artistPtr = (char*)Marshal.StringToHGlobalAnsi(Artist);
            }
            if (!string.IsNullOrEmpty(FinalDesigner))
            {
                finalDesignerPtr = (char*)Marshal.StringToHGlobalAnsi(FinalDesigner);
            }
            for (var i = 0; i < 7; i++)
            {
                *(chartArray + i) = _charts[i].ToUnmanaged();
            }
            if(_commands.Count != 0)
            {
                commandArray = (MajSimai.Unmanaged.UnmanagedSimaiCommand*)Marshal.AllocHGlobal(sizeof(MajSimai.Unmanaged.UnmanagedSimaiCommand) * _commands.Count);
                for (var i = 0; i < _commands.Count; i++)
                {
                    *(commandArray + i) = _commands[i].ToUnmanaged();
                }
            }

            return new()
            {
                title = titlePtr,
                titleLen = Title.Length,

                artist = artistPtr,
                artistLen = Artist.Length,

                finalDesigner = finalDesignerPtr,
                finalDesignerLen = FinalDesigner.Length,

                offset = Offset,

                charts = chartArray,
                chartsLen = Charts.Length,

                commands = commandArray,
                commandsLen = _commands.Count,
            };
        }
#endif
    }
}
