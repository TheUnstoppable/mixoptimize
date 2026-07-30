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

global using System;
global using System.Collections.Generic;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Globalization;
global using System.Linq;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Threading.Tasks;
global using ImageMagick;
global using MixLibrary;
global using SharpCompress.Common;
global using SharpCompress.Readers;
global using Spectre.Console;
global using Spectre.Console.Cli;
global using Spectre.Console.Cli.Help;
global using Spectre.Console.Rendering;
global using Size = mixoptimize.Size;