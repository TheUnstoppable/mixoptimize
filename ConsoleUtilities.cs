/*
 *  MixOptimize - C&C Renegade map and mod package optimizer
 *  Copyright (C) 2026 Unstoppable
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

namespace mixoptimize;

public static class ConsoleUtilities
{
    extension(AnsiConsole)
    {
        public static void WarningLine(string format, params object[]? args)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{string.Format(format, args!)}[/]");
        }
        
        public static void ErrorLine(string format, params object[]? args)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{string.Format(format, args!)}[/]");
        }
    }
}