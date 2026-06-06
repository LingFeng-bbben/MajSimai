#if NET7_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MajSimai.Unmanaged;
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct UnmanagedSimaiTimingPoint
{
    public double timing;
    public float bpm;
    public float hSpeed;
    public float sVeloc;
    public int rawTextPositionX;
    public int rawTextPositionY;
    public int rawTextPosition;
    public int notesLen;
    public int rawContentLen;
    public bool isEmpty;

    public UnmanagedSimaiNote* notes;
    public char* rawContent;

    public void Free()
    {
        Marshal.FreeHGlobal((nint)rawContent);
        if(notes is not null)
        {
            for (var i = 0; i < notesLen; i++)
            {
                (notes + i)->Free();
            }
            Marshal.FreeHGlobal((nint)notes);
            notes = null;
        }
        rawContent = null;
        rawContentLen = 0;
        notesLen = 0;
    }
}
#endif