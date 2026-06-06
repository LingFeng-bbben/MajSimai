#if NET7_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MajSimai.Unmanaged;
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct UnmanagedSimaiFile
{
    public int titleLen;
    public int artistLen;
    public int finalDesignerLen;
    public float offset;
    public int chartsLen;
    public int commandsLen;

    public char* title;
    public char* artist;
    public char* finalDesigner;
    public UnmanagedSimaiChart* charts;
    public UnmanagedSimaiCommand* commands;

    public void Free()
    {
        Marshal.FreeHGlobal((nint)title);
        Marshal.FreeHGlobal((nint)artist);
        Marshal.FreeHGlobal((nint)finalDesigner);
        if(charts is not null)
        {
            for (var i = 0; i < chartsLen; i++)
            {
                (charts + i)->Free();
            }
            Marshal.FreeHGlobal((nint)charts);
            charts = null;
        }
        if (commands is not null)
        {
            for (var i = 0; i < commandsLen; i++)
            {
                (commands + i)->Free();
            }
            Marshal.FreeHGlobal((nint)commands);
            commands = null;
        }

        title = null; 
        artist = null;
        finalDesigner = null;
        titleLen = 0;
        artistLen = 0;
        finalDesignerLen = 0;
        chartsLen = 0;
        commandsLen = 0;
    }
}
#endif