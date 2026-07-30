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

public static class Extensions
{
    extension(Directory)
    {
        public static void MoveAll(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (var file in Directory.EnumerateFiles(source))
            {
                var target = Path.Combine(destination, Path.GetFileName(file));
                File.Move(file, target, overwrite: true);
            }

            foreach (var directory in Directory.EnumerateDirectories(source))
            {
                var target = Path.Combine(destination, Path.GetFileName(directory));

                if (Directory.Exists(target))
                {
                    MoveAll(directory, target);
                    Directory.Delete(directory);
                }
                else
                {
                    Directory.Move(directory, target);
                }
            }
        }
    }
    
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

    public static async Task<byte[]> ReadAsByteArrayAsync(this Stream str)
    {
        using (var ms = new MemoryStream())
        {
            await str.CopyToAsync(ms);
            return ms.ToArray();
        }
    }
}