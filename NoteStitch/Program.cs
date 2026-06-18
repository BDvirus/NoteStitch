// NoteStitch — Stitch multiple Notepad windows into one document.
// Copyright (C) 2026 Dvirus
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace NoteStitch;

static class Program
{
    internal static readonly uint WM_NOTESTITCH_ACTIVATE =
        RegisterWindowMessage("WM_NOTESTITCH_ACTIVATE_2024");
    internal static readonly uint WM_NOTESTITCH_AUTOSAVE =
        RegisterWindowMessage("WM_NOTESTITCH_AUTOSAVE_2024");

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private const int HWND_BROADCAST = 0xFFFF;

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2602")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.STAThreadAttribute]
    static void Main(string[] args)
    {
        // Single-instance guard
        using var mutex = new System.Threading.Mutex(true, "NoteStitch_SingleInstance", out bool isNew);
        if (!isNew)
        {
            bool autoSave = args.Contains("/autosave", StringComparer.OrdinalIgnoreCase);
            PostMessage((IntPtr)HWND_BROADCAST,
                autoSave ? WM_NOTESTITCH_AUTOSAVE : WM_NOTESTITCH_ACTIVATE,
                IntPtr.Zero, IntPtr.Zero);
            return;
        }


        global::WinRT.ComWrappersSupport.InitializeComWrappers();
        global::Microsoft.UI.Xaml.Application.Start((p) => {
            var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            new App(args);
        });

        //// Bootstrap Windows App SDK for unpackaged apps
        //Bootstrap.Initialize(0x00010008);

        //global::WinRT.ComWrappersSupport.InitializeComWrappers();
        //Application.Start(p =>
        //{
        //    var context = DispatcherQueueController.CreateOnCurrentThread();
        //    new App(args);
        //});
    }
}
