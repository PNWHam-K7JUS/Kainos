/*  KainosVersion.cs

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

namespace Thetis
{
    // Kainos's own version (major.minor.patch): the one place it is set. The title bar and About window show it with
    // the Thetis version it is based on, and the installer build reads it from this line for the installer's version
    // and file name. A release: new features raise the minor number (1.1.0), fixes the patch number (1.0.1).
    internal static class KainosVersion
    {
        public const string Number = "1.0.0";
    }
}
