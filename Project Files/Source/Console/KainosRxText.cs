/*  KainosRxText.cs

This file is part of Kainos, a program that implements a Software-Defined Radio.
Kainos is based on Thetis : https://github.com/ramdor/Thetis

Copyright (C) 2026 Justin Cron K7JUS

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Thetis
{
    // The received text in the CW and RTTY windows. New text follows along only while you're at the bottom; scrolled
    // up to read something, the view and your selection stay put while text keeps arriving (GitHub #30: when the window
    // filled, every new character jumped it back to the end). Old text is trimmed from the top past maxChars.
    // "At the bottom" is judged from the view itself (is the last character showing?), not the scroll bar: the rich
    // edit control doesn't always move the thumb when it scrolls to the caret, so the bar could sit at the top while the
    // view was at the end, and the next character then read as "scrolled up" and stopped following (GitHub #64).
    internal static class KainosRxText
    {
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_SETREDRAW = 0x000B, WM_VSCROLL = 0x0115, SB_BOTTOM = 7;
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE, EM_LINESCROLL = 0x00B6;

        internal static void Append(RichTextBox rx, string text, Color color, int maxChars = 60000, int trimChars = 20000)
        {
            bool follow = atBottom(rx);
            int selStart = rx.SelectionStart, selLen = rx.SelectionLength;
            int first = follow ? 0 : firstVisibleLine(rx);
            if (!follow) SendMessage(rx.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                rx.SelectionStart = rx.TextLength;
                rx.SelectionLength = 0;
                rx.SelectionColor = color;
                rx.AppendText(text);
                if (rx.TextLength > maxChars)
                {
                    int cut = rx.Text.IndexOf('\n', trimChars);         // at a line's end, so the top line stays whole
                    trimChars = cut < 0 ? trimChars : cut + 1;
                    int trimmedLines = rx.GetLineFromCharIndex(trimChars);
                    rx.Select(0, trimChars);
                    rx.ReadOnly = false;
                    rx.SelectedText = "";
                    rx.ReadOnly = true;
                    selStart -= trimChars;
                    first = Math.Max(0, first - trimmedLines);
                }
                if (follow)
                {
                    rx.SelectionStart = rx.TextLength;
                    SendMessage(rx.Handle, WM_VSCROLL, (IntPtr)SB_BOTTOM, IntPtr.Zero);     // moves the view and the thumb
                }
                else
                {
                    if (selStart >= 0) rx.Select(selStart, selLen);
                    else rx.Select(0, 0);
                    int scroll = first - firstVisibleLine(rx);
                    if (scroll != 0) SendMessage(rx.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)scroll);
                }
            }
            finally
            {
                if (!follow)
                {
                    SendMessage(rx.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                    rx.Invalidate();
                }
            }
        }

        private static int firstVisibleLine(RichTextBox rx) { return (int)SendMessage(rx.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero); }

        private static bool atBottom(RichTextBox rx)
        {
            if (!rx.IsHandleCreated || rx.TextLength == 0) return true;
            // the last character's top inside the box (a part-shown last line still counts as the bottom)
            return rx.GetPositionFromCharIndex(rx.TextLength - 1).Y < rx.ClientSize.Height;
        }
    }
}
