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

global using System;
global using System.Collections.Generic;
global using System.ComponentModel;
global using System.Globalization;
global using System.Linq;
global using System.Text;
global using System.Threading.Tasks;
global using ImageMagick;
global using MixLibrary;
global using NAudio.Lame;
global using NAudio.Wave;
global using NLayer.NAudioSupport;
global using Spectre.Console;
global using Spectre.Console.Cli;
global using Size = mixoptimize.Size;