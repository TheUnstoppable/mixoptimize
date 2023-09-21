/*
 *  MixOptimize - C&C Renegade map and mod package optimizer
 *  Copyright (C) 2023 Unstoppable
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

public static class LevelDataManipulator
{
    public static int ReplaceLevelData(MixPackageClass mixPackage, string find, string replace)
    {
        int ret = 0;
        foreach (var file in mixPackage.Files.Where(x =>
                     x.FileName.EndsWith(".lsd", StringComparison.InvariantCultureIgnoreCase) ||
                     x.FileName.EndsWith(".ldd", StringComparison.InvariantCultureIgnoreCase) ||
                     x.FileName.EndsWith(".ddb", StringComparison.InvariantCultureIgnoreCase) ||
                     x.FileName.EndsWith(".tdb", StringComparison.InvariantCultureIgnoreCase)))
        {
            ret += Replace(file.Data, find, replace);
        }

        return ret;
    }

    public static int Replace(byte[] data, string find, string replace)
    {
        int count = 0;
        int length = find.Length;

        var findlwr = find.ToLowerInvariant();

        if (find.Length != replace.Length)
        {
            return 0;
        }

        for (int i = 0; i < data.Length; ++i)
        {
            if (data[i] == findlwr[0])
            {
                if (i + length <= data.Length)
                {
                    bool match = true;
                    for (int j = 0; j < length; ++j)
                    {
                        if (char.ToLower((char)data[i + j], CultureInfo.InvariantCulture) != findlwr[j])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        Array.Copy(Encoding.Default.GetBytes(replace), 0, data, i, replace.Length);
                        count++;
                    }
                }
            }
        }

        for (int i = 0; i < data.Length; ++i)
        {
            if (data[i] == findlwr[0])
            {
                if (i + (length * 2) <= data.Length)
                {
                    bool match = true;
                    for (int j = 0; j < length; ++j)
                    {
                        if (char.ToLower((char)data[i + (j * 2)], CultureInfo.InvariantCulture) != findlwr[j])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        Array.Copy(Encoding.BigEndianUnicode.GetBytes(replace), 0, data, i, replace.Length * 2);
                        count++;
                    }
                }
            }
        }

        return count;
    }
}