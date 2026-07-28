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

public struct ImageAnalysisResult : IAnalysisResult
{
    public bool NeedsAction => NeedsConversion || NeedsResizing;
    public bool NeedsConversion = false;
    public bool NeedsResizing = false;
    public Size OldSize = new Size(0,0);
    public Size NewSize = new Size(0,0);

    public ImageAnalysisResult()
    {

    }
}

public static class ImageAnalyzer
{
    private static bool AnalyzeDimensions(ref uint width, ref uint height)
    {
        bool ret = false;

        if (width != height) // Image is not square, we need to convert it to a square first.
        {
            double ratio = (double)width / (double)height;
            if (ratio > 1) // Width is bigger than height, we need to make height equal to width.
            {
                height = width;
            }
            else // Height is bigger than width, we need to make width equal to height.
            {
                width = height;
            }

            ret = true;
        }

        var exp = Math.Log2(width);
        if (exp % 1 != 0) // Not a power of two, round up to the next power of two.
        {
            exp = (int)Math.Ceiling(exp);
            ret = true;
        }

        if (exp > MixOptimize.Settings.MaxExponent) // Too big, resize to the maximum allowed size.
        {
            exp = MixOptimize.Settings.MaxExponent;
            ret = true;
        }

        if (ret)
        {
            width = height = (uint)Math.Pow(2, exp);
        }

        return ret;
    }

    public static ImageAnalysisResult AnalyzeDDS(byte[] ddsBytes)
    {
        var dds = new MagickImage(ddsBytes, MagickFormat.Dds);
        uint origWidth = dds.Width, origHeight = dds.Height;

        if (!MixOptimize.Settings.SkipTextureResize)
        {
            uint width = origWidth, height = origHeight;
            var result = new ImageAnalysisResult
            {
                NeedsResizing = AnalyzeDimensions(ref width, ref height),
                OldSize = new Size(origWidth, origHeight)
            };

            if (result.NeedsResizing)
            {
                result.NewSize = new Size(width, height);
            }

            return result;
        }
        else
        {
            return new ImageAnalysisResult()
            {
                OldSize = new Size(origWidth, origHeight)
            };
        }
    }

    public static ImageAnalysisResult AnalyzeTGA(byte[] tgaBytes)
    {
        var tga = new MagickImage(tgaBytes, MagickFormat.Tga);
        uint origWidth = tga.Width, origHeight = tga.Height;

        if (!MixOptimize.Settings.SkipTextureResize)
        {
            uint width = origWidth, height = origHeight;
            var result = new ImageAnalysisResult
            {
                NeedsConversion = !MixOptimize.Settings.SkipTextureConversion,
                NeedsResizing = AnalyzeDimensions(ref width, ref height),
                OldSize = new Size(origWidth, origHeight)
            };

            if (result.NeedsResizing)
            {
                result.NewSize = new Size(width, height);
            }

            return result;
        }
        else
        {
            return new ImageAnalysisResult()
            {
                NeedsConversion = !MixOptimize.Settings.SkipTextureConversion,
                OldSize = new Size(origWidth, origHeight)
            };
        }
    }

    public static byte[] ApplyDDS(byte[] ddsBytes, ImageAnalysisResult analysis)
    {
        var dds = new MagickImage(ddsBytes, MagickFormat.Dds);

        if (analysis.NeedsResizing)
        {
            dds.Resize(new MagickGeometry(analysis.NewSize.Width, analysis.NewSize.Height)
            {
                IgnoreAspectRatio = true
            });
        }

        return dds.ToByteArray(MagickFormat.Dds);
    }

    public static byte[] ApplyTGA(byte[] tgaBytes, ImageAnalysisResult analysis)
    {
        var tga = new MagickImage(tgaBytes, MagickFormat.Tga);

        if (analysis.NeedsResizing)
        {
            tga.Resize(new MagickGeometry(analysis.NewSize.Width, analysis.NewSize.Height)
            {
                IgnoreAspectRatio = true
            });
        }

        return tga.ToByteArray(analysis.NeedsConversion ? MagickFormat.Dds : MagickFormat.Tga);
    }
}
